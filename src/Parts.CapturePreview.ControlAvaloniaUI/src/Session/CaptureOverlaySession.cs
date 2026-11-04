using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Contract;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.Ui.AvaloniaUI.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;

/// <summary>選択キャプチャセッション（背景キャプチャ → オーバーレイ表示 → 結果取得）を管理する。</summary>
/// <remarks>
/// 内部実装であり、公開 API は <see cref="CaptureSessionControl"/> を使用する。<br/>
/// ユーザーの選択操作（ScreenSelect / WindowSelect / AreaSelect）の開始〜完了を一区切りのセッションとして完結させる。<br/>
/// プラットフォーム抽象（<see cref="ICaptureService"/> / <see cref="IWindowService"/> / <see cref="IElementFinder"/>）は注入され、<br/>
/// 内部で生成しない（DIP）。 状態は保持せず、<see cref="CaptureOverlayWindow"/> が選択 UI を担う。<br/>
/// <br/>
/// <b>表示の順序（チラつき対策）</b>:<br/>
/// 1. オーバーレイは<b>画面外</b>に置いた状態で表示し、撮影画像を合成した 2 フレーム後に<br/>
///    <b>大きさを変えずに位置だけ</b>画面内へ移す（画面内で合成すると、合成前の 1〜2 フレームが下地の面として見える）。<br/>
/// 2. オーナーの退避は、オーバーレイが画面内に出た後に行う（退避の瞬間を見せない）。<br/>
/// 3. 完了時は「画像変更 → オーナーの復帰 → 描画の完了待ち → オーバーレイを閉じる」の順にする<br/>
///    （逆順だと、オーバーレイが消えた後にデスクトップや旧画像が一瞬見える）。<br/>
/// 他の UI 版（MewUI / WinForms / WinUI3）も同じ順序である。
/// </remarks>
internal sealed class CaptureOverlaySession(
    IWindowService windowService,
    ICaptureService captureService,
    IElementFinder elementFinder,
    CaptureType captureType,
    CaptureSettings settings,
    double scaling,
    Action<CaptureResult?>? onCaptured = null,
    ILogger? logger = null)
{
    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger ?? NullLogger.Instance;

    /// <summary>表示中の選択オーバーレイウィンドウ（キャンセル用）。</summary>
    private CaptureOverlayWindow? _window;

    /// <summary>オーバーレイを表示するまでに待つ描画フレーム数。</summary>
    /// <remarks>
    /// 撮影画像は設定の次のフレームで合成される（1 フレームでは未合成の面が出る）。<br/>
    /// 2 フレーム目（画像が合成されたフレーム）で画面内へ移す（MewUI 版と同じ値）。
    /// </remarks>
    private const int RevealFrameCount = 2;

    /// <summary>画面内へ移してからオーナーを退避するまでに待つ描画フレーム数。</summary>
    /// <remarks>移した直後に退避すると、退避の瞬間のデスクトップが 1 フレーム見える。</remarks>
    private const int OwnerOffScreenFrameCount = 1;

    /// <summary>オーナーの画像が描画されるまでに待つ描画フレーム数。</summary>
    /// <remarks>画像の差し替えは次のフレームで合成される（待たずに閉じると、旧画像が 1〜2 フレーム見える）。</remarks>
    private const int OwnerPaintFrameCount = 2;

    /// <summary>描画フレームを待つ上限（1 フレーム分）。</summary>
    /// <remarks>
    /// フレームが来ない環境（最小化・ヘッドレステスト等）で待ち続けないための上限である。<br/>
    /// 通常のフレーム間隔（60 Hz で約 17 ms）より十分に長く取る。
    /// </remarks>
    private static readonly TimeSpan s_frameTimeout = TimeSpan.FromMilliseconds(60);

    /// <summary>キャプチャセッションを開始する。 UI スレッドから呼び出すこと。</summary>
    /// <param name="owner">オーナーウィンドウ（キャプチャ除外設定・所有ウィンドウとして使用）。</param>
    /// <returns>キャプチャ結果。 キャンセル時は 0x0 の空の結果。</returns>
    /// <remarks>
    /// 背景キャプチャは UI スレッドをブロックしないようバックグラウンドで実行する。<br/>
    /// 画面キャプチャは DWM 応答不能時に長時間ブロックする可能性があるため、タイムアウト付きで実行する。<br/>
    /// </remarks>
    public async Task<CaptureResult?> RunAsync(Window owner)
    {
        // 結果の単一確定（最初の 1 件だけを採用する）はコアの完了通知が単一所有する
        var completion = new CaptureCompletion<CaptureResult?>();
        _log.LogCaptureStart(captureType);

        // オーナーの退避とキャプチャ除外の設定・解除は CaptureOwnerScope（コア）が単一所有する
        // （手順: 除外の設定 → 画面外へ退避 → 撮影 → 配置の復帰 → 除外の解除）
        // ネイティブハンドルが無い場合（ヘッドレステスト等）は既定値（0）を渡す（除外は設定されない）
        var hideOwner = settings.HideOwnerWindow!.Value;
        var ownerHandle = owner.TryGetPlatformHandle() is { } handle
            ? handle.ToEsPlatformHandle()
            : default;
        using var ownerScope = CaptureOwnerScope.Enter(
            windowService, ownerHandle, new OwnerWindowAdapter(owner), hideOwner);

        // 背景キャプチャをバックグラウンドで開始する（完了を待たずにオーバーレイを表示する = 選択可能までの高速化）。
        // 全モード共通で取得し、確定時にこの Bitmap から切り出す（確定時の再キャプチャなしで高速化）。
        // キャプチャ完了前に確定された場合は CaptureOverlayWindow.ExtractClip が CaptureArea でフォールバックする。
        // 注: origin は out パラメータのためタスク完了後にしか有効にならない（遅延評価で渡す）。
        var origin = default(EsPointInt);
        var captureTask = Task.Run(() => captureService.CaptureVirtualScreen(out origin));

        // オーバーレイウィンドウを生成する（完了は通知だけを受け、閉じるのは本メソッドが行う。
        // 閉じる順序を本メソッドが単一所有することで「画像変更 → 復帰 → 閉じる」を保証する）
        _window = new CaptureOverlayWindow(
            windowService, captureService, elementFinder,
            captureType, settings, scaling,
            // キャンセル（null）は 0x0 の空結果として扱う（規則はコアの CaptureResult が定める）
            (bitmap, bounds) => completion.TrySetResult(CaptureResult.From(bitmap, bounds)),
            _log);

        // **画面外に置いた状態で表示し、撮影画像を合成してから画面内へ移す**
        // （画面内で表示すると、画像が合成されるまでの 1〜2 フレームに下地やデスクトップが見える）。
        // 大きさは変えずに位置だけ動かすのは、大きさを変えると再レイアウトが走り、
        // その瞬間にウィンドウの下地が 1 フレーム見えるためである（WinUI 3 版と同じ方式）。
        var window = _window;
        var targetPosition = window.Position;
        window.Position = new PixelPoint(
            CaptureTiming.OwnerOffScreenCoord, CaptureTiming.OwnerOffScreenCoord);

        // 背景キャプチャの完了をウィンドウへ反映する継続を登録する。
        // ウィンドウはローカル変数で渡す（背景スレッドがフィールドを読むと、キャプチャが即完了した場合に
        // 生成前の null を読んで背景を設定しないまま Bitmap を破棄する競合が起きる）
        SetupVirtualScreenContinuation(
            window, captureTask, () => origin.ToAvaloniaPixelPoint(), targetPosition, ownerScope, completion);

        window.Show(owner);

        try
        {
            var result = await completion.Task;
            // 完了の記録（確定は 2 件・キャンセルは 1 件）はコアが単一所有する
            _log.LogCaptureFinished(result);
            // キャプチャ完了イベントを発行する（同期実行）。
            // フロー: APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）
            onCaptured?.Invoke(result);
            // オーナーを元の配置へ戻す（復帰はオーバーレイが画面を覆っている間に行うため、
            // 復帰の瞬間にデスクトップが見えない）
            ownerScope.Dispose();
            // 復帰したアプリが新しい画像を描き終えてからオーバーレイを閉じる
            // （閉じてから描くと、アプリが再表示された後に画像が遅れて出る）
            if (result?.Image is not null)
            {
                await WaitForFramesAsync(owner, OwnerPaintFrameCount).ConfigureAwait(true);
            }
            return result;
        }
        finally
        {
            _window = null;
            // 例外経路でも復帰させる（二重呼び出しは無害）
            ownerScope.Dispose();
            // オーナーの復帰後に閉じる（閉じた瞬間に、新しい画像を表示したアプリが見える）
            window.Close();
        }
    }

    /// <summary>描画フレームの経過を待つ（待ち時間には上限を設ける）。</summary>
    /// <param name="topLevel">対象のトップレベル（ウィンドウ）。</param>
    /// <param name="frameCount">待つフレーム数。</param>
    /// <returns>待機が完了したときに完了するタスク。</returns>
    /// <remarks>
    /// 画像の差し替えは次の描画フレームで合成されるため、合成を待たずに次の操作（オーバーレイを閉じる等）を<br/>
    /// 行うと、旧画像や空の面が 1〜2 フレーム見える。 フレームが来ない環境（最小化・ヘッドレステスト等）では<br/>
    /// <see cref="s_frameTimeout"/> で打ち切る（待ち続けて操作を止めない）。
    /// </remarks>
    private static async Task WaitForFramesAsync(TopLevel topLevel, int frameCount)
    {
        for (var i = 0; i < frameCount; i++)
        {
            var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            topLevel.RequestAnimationFrame(_ => frame.TrySetResult());
            var completed = await Task.WhenAny(frame.Task, Task.Delay(s_frameTimeout)).ConfigureAwait(true);
            if (!ReferenceEquals(completed, frame.Task))
            {
                // フレームが来ない（以降も来ない可能性が高い）ため打ち切る
                return;
            }
        }
    }

    /// <summary>オーナーウィンドウ（Avalonia）を撮影時に画面外へ退避するポート実装。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <remarks>
    /// 非表示（<c>Hide</c>）は復帰時の再作成でちらつくため使わず、<c>Opacity = 0</c> と画面外配置で退避する。<br/>
    /// 退避前の位置と不透明度を保存し、<see cref="RestorePlacement"/> で復元する。
    /// </remarks>
    private sealed class OwnerWindowAdapter(Window window) : ICaptureOwnerWindow
    {
        /// <summary>退避前の位置（未退避の場合は null）。</summary>
        private PixelPoint? _savedPosition;

        /// <summary>退避前の不透明度。</summary>
        private double _savedOpacity = 1.0;

        /// <inheritdoc/>
        public bool IsVisible => window.IsVisible;

        /// <inheritdoc/>
        public void HideOffScreen()
        {
            _savedPosition = window.Position;
            _savedOpacity = window.Opacity;

            // 透明化してから画面外（仮想スクリーンの外）へ退避する
            window.Opacity = 0;
            window.Position = new PixelPoint(
                CaptureTiming.OwnerOffScreenCoord, CaptureTiming.OwnerOffScreenCoord);
        }

        /// <inheritdoc/>
        public void RestorePlacement()
        {
            if (_savedPosition is not { } position)
            {
                return;
            }

            window.Position = position;
            window.Opacity = _savedOpacity;
            _savedPosition = null;
        }
    }

    /// <summary>背景キャプチャ完了後に、オーバーレイウィンドウへ仮想スクリーン Bitmap を設定する継続処理を登録する。</summary>
    /// <param name="window">背景を設定するオーバーレイウィンドウ（生成済みのインスタンス）。</param>
    /// <param name="captureTask">仮想スクリーンキャプチャの Task。</param>
    /// <param name="originProvider">仮想スクリーンの原点（タスク完了後に評価される遅延プロバイダ）。</param>
    /// <param name="targetPosition">オーバーレイを表示する位置（画面外で合成した後に移す先）。</param>
    /// <param name="ownerScope">オーナーの退避と復帰のスコープ（退避の実行時期を本継続が決める）。</param>
    /// <param name="completion">セッションの完了通知（完了後は表示の操作をしない）。</param>
    /// <remarks>
    /// <b>タイムアウトと未使用結果の破棄はコアの <see cref="CaptureVirtualScreen.AwaitAsync"/> が単一所有する</b><br/>
    /// （間に合わない場合は設定せず、確定時の <c>CaptureArea</c> フォールバックに委ねる）。<br/>
    /// 完了時は UI スレッドで <see cref="CaptureOverlayWindow.SetVirtualScreen"/> を呼んで背景を設定する（Bitmap 操作は UI スレッド）。<br/>
    /// ウィンドウが既に閉じられた（キャンセル等）場合はウィンドウ側が Bitmap を破棄する。<br/>
    /// <b>表示の順序</b>: ウィンドウは画面外に置いたまま表示してあるため、背景を設定して<br/>
    /// <see cref="RevealFrameCount"/> フレーム待ってから<b>位置だけ</b>画面内へ移す<br/>
    /// （画面内で合成すると、合成前の 1〜2 フレームに下地やデスクトップが見える）。<br/>
    /// オーナーの退避はその後に実行する（退避の瞬間を見せない。 撮影は既に完了しているため写り込みにも影響しない）。<br/>
    /// <b>重要</b>: <c>out origin</c> はタスク完了後にしか有効にならないため、<br/>
    /// 原点はタスク完了後に <paramref name="originProvider"/> で遅延評価する（マルチモニタの座標ずれ防止）。<br/>
    /// <b>重要</b>: ウィンドウは引数で受け取る（背景スレッドがフィールドを読むと、キャプチャが即完了した場合に<br/>
    /// 生成前の null を読んで背景を設定しないまま Bitmap を破棄する競合が起きる）。
    /// </remarks>
    private void SetupVirtualScreenContinuation(
        CaptureOverlayWindow window,
        Task<EsBitmap?> captureTask,
        Func<PixelPoint> originProvider,
        PixelPoint targetPosition,
        CaptureOwnerScope ownerScope,
        CaptureCompletion<CaptureResult?> completion)
    {
        _ = Task.Run(async () =>
        {
            EsBitmap? bitmap = null;
            var origin = default(PixelPoint);
            try
            {
                // タイムアウト付きで待つ（間に合わなかった結果はコアが破棄する）
                bitmap = await CaptureVirtualScreen.AwaitAsync(captureTask).ConfigureAwait(false);
                // タスク完了後に原点を評価する（out パラメータが有効になった値）
                origin = originProvider();
            }
            catch (Exception ex) when (EsErrors.IsRecoverable(ex))
            {
                // 取得の失敗でオーバーレイを画面外のまま放置しない（表示だけは必ず行う）
                _log.LogOperationError(ex, nameof(SetupVirtualScreenContinuation));
            }

            // UI スレッドで背景を設定し、合成されたフレームでウィンドウを画面内へ移す。
            // ウィンドウが閉じられていても SetVirtualScreen は安全（破棄はウィンドウが一元管理する）
            Dispatcher.UIThread.Post(
                () => _ = ApplyBackgroundAsync(window, bitmap, origin, targetPosition, ownerScope, completion));
        });
    }

    /// <summary>撮影画像を背景へ設定し、合成されたフレームでオーバーレイを画面内へ移す。</summary>
    /// <param name="window">対象のオーバーレイウィンドウ。</param>
    /// <param name="bitmap">撮影した仮想スクリーン。 取得できなかった場合は null（背景なしで表示する）。</param>
    /// <param name="origin">仮想スクリーンの原点。</param>
    /// <param name="targetPosition">オーバーレイを表示する位置。</param>
    /// <param name="ownerScope">オーナーの退避と復帰のスコープ。</param>
    /// <param name="completion">セッションの完了通知。</param>
    /// <returns>表示が完了したときに完了するタスク。</returns>
    /// <remarks>
    /// セッションが完了している（確定・キャンセル済み）場合は何もしない<br/>
    /// （閉じたウィンドウを表示し直したり、復帰済みのオーナーを退避したりしない）。<br/>
    /// 表示の操作で例外が出てもセッションを止めない（ウィンドウが閉じられている場合等）。<br/>
    /// 画像の取得に失敗した場合も表示だけは行う（画面外のまま操作不能になるのを防ぐ）。
    /// </remarks>
    private async Task ApplyBackgroundAsync(
        CaptureOverlayWindow window,
        EsBitmap? bitmap,
        PixelPoint origin,
        PixelPoint targetPosition,
        CaptureOwnerScope ownerScope,
        CaptureCompletion<CaptureResult?> completion)
    {
        try
        {
            if (completion.Task.IsCompleted)
            {
                return;
            }

            if (bitmap is not null)
            {
                window.SetVirtualScreen(bitmap, origin);
            }

            // 撮影画像が合成されたフレームを待ってから、位置だけ画面内へ移す
            await WaitForFramesAsync(window, RevealFrameCount).ConfigureAwait(true);
            if (completion.Task.IsCompleted)
            {
                return;
            }

            window.Position = targetPosition;

            // 画面内へ移した次のフレームでオーナーを退避する（退避の瞬間を見せない）
            await WaitForFramesAsync(window, OwnerOffScreenFrameCount).ConfigureAwait(true);
            if (!completion.Task.IsCompleted)
            {
                ownerScope.ApplyPendingHide();
            }
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 表示の継続が例外で止まるとセッションが終われないため、境界で記録して続行する
            // （オーバーレイは画面外のまま残らないよう、位置だけは移す）
            _log.LogOperationError(ex, nameof(ApplyBackgroundAsync));
            window.Position = targetPosition;
        }
    }

    /// <summary>進行中の選択キャプチャをプログラム的にキャンセルする。</summary>
    /// <remarks>
    /// ウィンドウを直接閉じずにキャンセルとして完了させる（閉じる順序は <see cref="RunAsync"/> が単一所有する）。
    /// </remarks>
    public void Cancel() => _window?.RequestCancel();
}





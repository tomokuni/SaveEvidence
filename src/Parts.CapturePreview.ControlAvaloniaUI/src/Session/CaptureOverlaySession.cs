using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using EsUtil.Platform.Core.Native;
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
/// 内部で生成しない（DIP）。 状態は保持せず、<see cref="CaptureOverlayWindow"/> が選択 UI を担う。
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

        // オーバーレイウィンドウを生成・表示（キャプチャ完了を待たない）
        _window = new CaptureOverlayWindow(
            windowService, captureService, elementFinder,
            captureType, settings, scaling,
            // キャンセル（null）は 0x0 の空結果として扱う（規則はコアの CaptureResult が定める）
            (bitmap, bounds) => completion.TrySetResult(CaptureResult.From(bitmap, bounds)),
            _log);

        // 背景キャプチャの完了をウィンドウへ反映する継続を登録する。
        // ウィンドウはローカル変数で渡す（背景スレッドがフィールドを読むと、キャプチャが即完了した場合に
        // 生成前の null を読んで背景を設定しないまま Bitmap を破棄する競合が起きる）
        var window = _window;
        SetupVirtualScreenContinuation(window, captureTask, () => origin.ToAvaloniaPixelPoint());

        _window.Show(owner);

        // 予約した退避を実行する（オーバーレイが画面を覆ってから退避するため、
        // 撮影開始時にデスクトップが一瞬見える「白い画面」が発生しない）
        ownerScope.ApplyPendingHide();

        try
        {
            var result = await completion.Task;
            // 完了の記録（確定は 2 件・キャンセルは 1 件）はコアが単一所有する
            _log.LogCaptureFinished(result);
            // キャプチャ完了イベントを発行する（同期実行）。
            // C# のイベントは同期型のため、ホストのハンドラ（画像変更）が完了してから
            // 以下の finally（オーナー再表示）が実行される。
            // フロー: APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）
            onCaptured?.Invoke(result);
            return result;
        }
        finally
        {
            _window = null;
            // オーナーウィンドウを元の状態に復元する（キャプチャ完了イベント＝画像変更の後）。
            // 画像は onCaptured で同期更新済みのため、画面外から元の位置に戻して Opacity を
            // 復元するだけで、新しい画像が完全に描画された状態で表示される。
            // ウィンドウは表示されたまま透明化していたため、Show/Hide による再作成や
            // 「一瞬表示」が発生しない（チラつきの根本対策）。
            ownerScope.Dispose();
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
    /// <remarks>
    /// <b>タイムアウトと未使用結果の破棄はコアの <see cref="CaptureVirtualScreen.AwaitAsync"/> が単一所有する</b><br/>
    /// （間に合わない場合は設定せず、確定時の <c>CaptureArea</c> フォールバックに委ねる）。<br/>
    /// 完了時は UI スレッドで <see cref="CaptureOverlayWindow.SetVirtualScreen"/> を呼んで背景を設定する（Bitmap 操作は UI スレッド）。<br/>
    /// ウィンドウが既に閉じられた（キャンセル等）場合はウィンドウ側が Bitmap を破棄する。<br/>
    /// <b>重要</b>: <c>out origin</c> はタスク完了後にしか有効にならないため、<br/>
    /// 原点はタスク完了後に <paramref name="originProvider"/> で遅延評価する（マルチモニタの座標ずれ防止）。<br/>
    /// <b>重要</b>: ウィンドウは引数で受け取る（背景スレッドがフィールドを読むと、キャプチャが即完了した場合に<br/>
    /// 生成前の null を読んで背景を設定しないまま Bitmap を破棄する競合が起きる）。
    /// </remarks>
    private static void SetupVirtualScreenContinuation(
        CaptureOverlayWindow window, Task<EsBitmap?> captureTask, Func<PixelPoint> originProvider)
    {
        _ = Task.Run(async () =>
        {
            // タイムアウト付きで待つ（間に合わなかった結果はコアが破棄する）
            var bitmap = await CaptureVirtualScreen.AwaitAsync(captureTask).ConfigureAwait(false);
            if (bitmap is null)
            {
                // 時間切れまたは取得失敗: 背景を設定しない（確定時の領域指定キャプチャに委ねる）
                return;
            }

            // タスク完了後に原点を評価する（out パラメータが有効になった値）
            var origin = originProvider();

            // UI スレッドで背景を設定（Bitmap 操作は UI スレッドで行う）。
            // ウィンドウが閉じられていても SetVirtualScreen は安全（破棄はウィンドウが一元管理する）
            Dispatcher.UIThread.Post(() => window.SetVirtualScreen(bitmap, origin));
        });
    }

    /// <summary>進行中の選択キャプチャをプログラム的にキャンセルする。</summary>
    public void Cancel()
    {
        // オーバーレイウィンドウのクローズは Deactivated 経由でキャンセルされる
        _window?.Close();
    }
}





using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Estable.AvaloniaUI.CaptureSession.Logging;
using Microsoft.Extensions.Logging;
using Estable.CaptureSession;

namespace Estable.AvaloniaUI.CaptureSession;

/// <summary>選択キャプチャセッション（背景キャプチャ → オーバーレイ表示 → 結果取得）を管理する。</summary>
/// <remarks>
/// 内部実装であり、公開 API は <see cref="CaptureSessionControl"/> を使用する。<br/>
/// ユーザーの選択操作（ScreenSelect / WindowSelect / AreaSelect）の開始〜完了を一区切りのセッションとして完結させる。<br/>
/// プラットフォーム抽象（<see cref="ICaptureService"/> / <see cref="IWindowService"/> / <see cref="IElementFinder"/>）は注入され、<br/>
/// 内部で生成しない（DIP）。状態は保持せず、<see cref="OverlayWindow"/> が選択 UI を担う。
/// </remarks>
internal sealed class SelectionCaptureSession(
    IWindowService windowService,
    ICaptureService captureService,
    IElementFinder elementFinder,
    CaptureType captureType,
    CaptureSettings settings,
    double scaling,
    Action<CaptureResult?>? onCaptured = null,
    ILogger? logger = null)
{
    /// <summary>背景キャプチャのタイムアウト（ms）。画面キャプチャが DWM 応答不能等でブロックした場合の安全策。</summary>
    private const int CaptureTimeoutMs = 5000;

    /// <summary>オーナーを退避させる画面外の座標（画面物理ピクセル）。</summary>
    private const int OffScreenCoordinate = -32000;

    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger.OrNullLogger();

    /// <summary>表示中の選択オーバーレイウィンドウ（キャンセル用）。</summary>
    private OverlayWindow? _window;

    /// <summary>キャプチャセッションを開始する。UI スレッドから呼び出すこと。</summary>
    /// <param name="owner">オーナーウィンドウ（キャプチャ除外設定に使用）。</param>
    /// <param name="showWithOwner">OverlayWindow をオーナー付きで表示するか。</param>
    /// <returns>キャプチャ結果。キャンセル時は 0x0 の空の結果。</returns>
    /// <remarks>
    /// 背景キャプチャは UI スレッドをブロックしないようバックグラウンドで実行する。<br/>
    /// 画面キャプチャは DWM 応答不能時に長時間ブロックする可能性があるため、タイムアウト付きで実行する。<br/>
    /// </remarks>
    public async Task<CaptureResult?> RunAsync(Window owner, bool showWithOwner = true)
    {
        var tcs = new TaskCompletionSource<CaptureResult?>();
        _log.LogCaptureStart(captureType.ToString());

        // オーナーの退避とキャプチャ除外の設定・解除は CaptureOwnerScope（コア）が単一所有する
        // （手順: 除外の設定 → 画面外へ退避 → 撮影 → 配置の復帰 → 除外の解除）
        // ネイティブハンドルが無い場合（ヘッドレステスト等）は既定値（0）を渡す（除外は設定されない）
        var hideOwner = showWithOwner && (settings.HideOwnerWindow ?? true);
        var ownerHandle = owner.TryGetPlatformHandle() is { } handle
            ? handle.ToEsPlatformHandle()
            : default(EsPlatformHandle);
        using var ownerScope = CaptureOwnerScope.Enter(
            windowService, ownerHandle, new OwnerWindowAdapter(owner), hideOwner);
        var ownerHidden = hideOwner;

        // 背景キャプチャをバックグラウンドで開始する（完了を待たずにオーバーレイを表示する = 選択可能までの高速化）。
        // 全モード共通で取得し、確定時にこの Bitmap から切り出す（確定時の再キャプチャなしで高速化）。
        // キャプチャ完了前に確定された場合は OverlayWindow.ExtractClip が CaptureArea でフォールバックする。
        // 注: origin は out パラメータのためタスク完了後にしか有効にならない（遅延評価で渡す）。
        var origin = default(EsPixelPoint);
        var captureTask = Task.Run(() => captureService.CaptureVirtualScreen(out origin));
        SetupVirtualScreenContinuation(captureTask, () => origin.ToAvaloniaPixelPoint());

        // オーバーレイウィンドウを生成・表示（キャプチャ完了を待たない）
        _window = new OverlayWindow(
            windowService, captureService, elementFinder,
            captureType, settings, scaling,
            (bitmap, bounds) =>
            {
                // キャンセル（null）は 0x0 の空結果として扱う
                if (bitmap is null || bounds is null)
                {
                    // キャンセルは 0x0 の矩形（画像は null のまま）
                    tcs.TrySetResult(new CaptureResult(null, default(EsPixelRect)));
                }
                else
                {
                    tcs.TrySetResult(new CaptureResult(bitmap, bounds.Value.ToEsPixelRect()));
                }
            },
            _log);

        // オーナー非表示時はオーナーなしで表示する（Win32 ではオーナー非表示の所有ウィンドウは
        // 一緒に非表示になるため。オーバーレイは Topmost のため前面に表示される）
        if (showWithOwner && !ownerHidden)
        {
            _window.Show(owner);
        }
        else
        {
            _window.Show();
        }

        try
        {
            var result = await tcs.Task;
            // キャプチャ完了イベントを発行する（同期実行）。
            // C# のイベントは同期型のため、ホストのハンドラ（画像変更）が完了してから
            // 以下の finally（オーナー再表示）が実行される。
            // フロー: APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）
            if (result is { Image: not null, SourceBounds: { } bounds })
            {
                _log.LogCaptureCompleted(bounds.ToString());
                _log.LogResult(result.Image.Size.Width, result.Image.Size.Height, bounds.ToString());
            }
            else
            {
                _log.LogCaptureCancelled();
            }
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
            window.Position = new PixelPoint(OffScreenCoordinate, OffScreenCoordinate);
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
    /// <param name="captureTask">仮想スクリーンキャプチャの Task。</param>
    /// <param name="originProvider">仮想スクリーンの原点（タスク完了後に評価される遅延プロバイダ）。</param>
    /// <remarks>
    /// タイムアウト（<see cref="CaptureTimeoutMs"/>）付きで完了を監視し、<br/>
    /// 完了時は UI スレッドで <see cref="OverlayWindow.SetVirtualScreen"/> を呼んで背景を設定する。<br/>
    /// タイムアウト時は設定せず、確定時の <c>CaptureArea</c> フォールバックに委ねる。<br/>
    /// ウィンドウが既に閉じられた（キャンセル等）場合は Bitmap を破棄する。<br/>
    /// <b>重要</b>: <c>out origin</c> はタスク完了後にしか有効にならないため、<br/>
    /// 原点はタスク完了後に <paramref name="originProvider"/> で遅延評価する（マルチモニタの座標ずれ防止）。
    /// </remarks>
    private void SetupVirtualScreenContinuation(Task<EsBitmap?> captureTask, Func<PixelPoint> originProvider)
    {
        _ = Task.Run(async () =>
        {
            // タイムアウト監視: キャプチャ完了 or タイムアウトまで待機
            var done = await Task.WhenAny(captureTask, Task.Delay(CaptureTimeoutMs)).ConfigureAwait(false);
            if (done != captureTask)
            {
                // タイムアウト: キャプチャ結果は未使用（完了時に破棄する）
                _ = captureTask.ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully) t.Result?.Dispose();
                }, TaskScheduler.Default);
                return;
            }

            var bitmap = captureTask.Result;
            // タスク完了後に原点を評価する（out パラメータが有効になった値）
            var origin = originProvider();
            if (_window is { } w)
            {
                // UI スレッドで背景を設定（Bitmap 操作は UI スレッドで行う）
                Dispatcher.UIThread.Post(() => w.SetVirtualScreen(bitmap, origin));
            }
            else
            {
                // ウィンドウが閉じられた（キャンセル等）場合は破棄する
                bitmap?.Dispose();
            }
        });
    }

    /// <summary>進行中の選択キャプチャをプログラム的にキャンセルする。</summary>
    public void Cancel()
    {
        // オーバーレイウィンドウのクローズは Deactivated 経由でキャンセルされる
        _window?.Close();
    }
}


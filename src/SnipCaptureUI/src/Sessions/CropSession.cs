using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.SnipCaptureUI.Logging;
using Estable.AvaloniaUI.FadeMaskDragRect;
using Estable.AvaloniaUI.ZoomView;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.SnipCaptureUI;

/// <summary>切出し（Crop）セッション（FadeMaskDragRectControl のライフサイクル管理と確定・キャンセル処理）。</summary>
/// <remarks>
/// 内部実装であり、公開 API は <see cref="SnipCapture.StartCropAsync"/> を使用する。<br/>
/// <br/>
/// 状態は持たない（選択矩形・ルーペ表示状態・暗転マスクは複合コントロール<br/>
/// <see cref="FadeMaskDragRectControl"/> が保持し、本クラスはライフサイクルのみ）。<br/>
/// - 配線（SelectionChanged → 暗転マスク同期 / LoupePositionMoved → ルーペ更新 / 確定・キャンセル）は<br/>
///   複合コントロール内で完結する（ホストの配線コードは不要）。<br/>
/// - ドメイン = 元画像ピクセル（論理座標）。座標変換ポートは<br/>
///   <see cref="CoordinatePort.ImageLayout"/> + <see cref="CoordinatePort.ScrollOffset"/>（PlatformFactory）で提供。<br/>
/// <br/>
/// プレビュー状態（ScrollOffset / ViewportOrigin / ZoomPercent）は保持しない。<br/>
/// スクロールは <see cref="VisualExtensions.TranslatePoint"/>（スクロール込みの表示位置）に含まれるため、<br/>
/// 座標変換は「論理 ↔ 親パネルDIP」の 1 段で完結する。
/// </remarks>
internal sealed class CropSession(
    ZoomViewControl previewControl,
    Window hostWindow,
    PbBitmap sourceImage,
    CaptureSettings settings,
    ILogger? logger = null,
    Action<bool>? zoomHandler = null)
{
    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger ?? NullLogger.Instance;

    /// <summary>ズーム要求ハンドラ（Ctrl+ホイール。true = ズームイン / false = ズームアウト）。上位 AP がズームを実施する。</summary>
    private readonly Action<bool>? _zoomHandler = zoomHandler;

    /// <summary>元画像のピクセルサイズ（論理座標系の範囲）。</summary>
    private readonly Size _sourceImageSize = new(sourceImage.Size.Width, sourceImage.Size.Height);

    /// <summary>オーバーレイ・ルーペの配置先パネル（previewControl の祖先 Panel）。</summary>
    private readonly Panel _parentPanel = FindAncestorPanel(previewControl)
        ?? throw new InvalidOperationException("previewControl の親に Panel が見つかりません。");

    /// <summary>選択オーバーレイ複合コントロール（DragRect + FadeMask + Loupe の配線を内包）。</summary>
    /// <remarks>
    /// <c>ClipToBounds = true</c>: 画像のスクロール・ズームで選択矩形がパネル（ImageAreaGrid）境界外に<br/>
    /// はみ出す場合でも、ツールバー・ステータスバー等の兄弟領域へ描画しないようクリップする。<br/>
    /// モードは Crop（プレビュー内の切出し。全面暗転 + 選択矩形 + ルーペ）。
    /// </remarks>
    private readonly FadeMaskDragRectControl _selectionOverlay =
        new() { ZIndex = 50, ClipToBounds = true, Mode = FadeMaskDragRectMode.Crop };

    /// <summary>カーソル上書き抑止（Crop 中の十字カーソル維持）。</summary>
    private readonly ICursorOverrideGuard _cursorGuard = PlatformFactory.CreateCursorOverrideGuard();

    /// <summary>設定（全メンバー non-null）。実行中に <see cref="ApplySettings"/> で更新できる。</summary>
    private CaptureSettings _settings = settings.ToNonNull();

    /// <summary>セッション結果の待機先（RunAsync で生成）。</summary>
    private TaskCompletionSource<PixelRect?>? _tcs;

    /// <summary>完了済みかどうか（確定・キャンセルの二重実行防止）。</summary>
    private bool _completed;

    /// <summary>選択矩形が有効かどうか（複合コントロールの選択状態）。ホスト（SnipCapture）の操作分岐用。</summary>
    /// <remarks>選択状態の真のソースは FadeMaskRenderer（<see cref="FadeMaskDragRectControl.HasSelection"/>）に集約する。</remarks>
    public bool HasSelection => _selectionOverlay.HasSelection;

    /// <summary>セッション実行中かどうか。</summary>
    private bool _isRunning;

    /// <summary>切出しセッションを実行する（確定待機 → 結果返却）。UI スレッドから呼び出すこと。</summary>
    /// <returns>確定時は元画像上の切り出し範囲、キャンセル時は 0x0 の空 PixelRect。</returns>
    public Task<PixelRect?> RunAsync()
    {
        if (_isRunning) return Task.FromResult<PixelRect?>(null); // 再入禁止
        _isRunning = true;
        _completed = false;
        var tcs = new TaskCompletionSource<PixelRect?>();
        _tcs = tcs;

        // 選択オーバーレイ複合コントロールを親パネルに配置
        _parentPanel.Children.Add(_selectionOverlay);
        ConfigureControls();
        _log.LogCropStart((int)_sourceImageSize.Width, (int)_sourceImageSize.Height);

        // プレビューの ScrollViewer を複合コントロール（DragRect）へ明示注入する（スクロール・ズーム検知 + ホイール操作）。
        // DragRect はプレビューと兄弟の親パネルに配置されるため、祖先探索では見つからない（明示注入が唯一の連携方式）。
        if (FindAncestorScrollViewer(previewControl) is { } scrollViewer)
        {
            _selectionOverlay.AttachScrollViewer(scrollViewer);
        }

        void OnConfirm(Rect logicalRect)
        {
            if (_completed) return;
            _completed = true;
            var bounds = ExecuteCropForSize(logicalRect);
            _log.LogCropCompleted(bounds.ToString());
            Cleanup();
            tcs.TrySetResult(bounds);
        }

        void OnCancel()
        {
            if (_completed) return;
            _completed = true;
            _log.LogCropCancelled();
            Cleanup();
            tcs.TrySetResult(default); // 0x0 の空 PixelRect
        }

        void OnConfirmEvent(object? sender, FadeMaskDragRectSelectionEventArgs e) => OnConfirm(e.SelectionRect);

        void OnCancelEvent(object? sender, EventArgs e) => OnCancel();

        void Cleanup()
        {
            hostWindow.Deactivated -= OnDeactivated;
            _selectionOverlay.ConfirmRequested -= OnConfirmEvent;
            _selectionOverlay.CancelRequested -= OnCancelEvent;
            _selectionOverlay.ZoomRequested -= OnZoomRequested;
            _selectionOverlay.AttachScrollViewer(null); // 明示注入した ScrollViewer 連携を解除
            _cursorGuard.Dispose();
            _selectionOverlay.ClearLoupe();
            _parentPanel.Children.Remove(_selectionOverlay);
            hostWindow.Cursor = Cursor.Default;
            _isRunning = false;
        }

        void OnDeactivated(object? s, EventArgs e) => OnCancel();

        // ズーム要求（Ctrl+ホイール）: 上位 AP のズーム処理へ委譲する
        void OnZoomRequested(object? sender, bool zoomIn) => _zoomHandler?.Invoke(zoomIn);

        // イベント配線（配線は複合コントロール内で完結。ホストは確定・キャンセル・ズーム要求のみ購読）
        _selectionOverlay.ConfirmRequested += OnConfirmEvent;
        _selectionOverlay.CancelRequested += OnCancelEvent;
        _selectionOverlay.ZoomRequested += OnZoomRequested;
        hostWindow.Deactivated += OnDeactivated;

        // カーソル上書き抑止（WM_SETCURSOR による十字カーソルの上書き防止）
        _cursorGuard.Attach(hostWindow);
        _selectionOverlay.FocusDragRect();

        return tcs.Task;
    }

    /// <summary>進行中の切出しセッションをプログラム的にキャンセルする。</summary>
    /// <remarks>DragRect にキャンセル要求を委譲する（選択破棄 + CancelRequested 発火 → OnCancelEvent 経由で完了）。</remarks>
    public void Cancel()
    {
        if (!_isRunning) return;
        _selectionOverlay.RequestCancel();
    }

    /// <summary>進行中の切出しセッションを現在の選択矩形で確定する（Enter キー相当）。</summary>
    /// <remarks>DragRect に確定要求を委譲する（選択確定時のみ ConfirmRequested 発火 → OnConfirmEvent 経由で完了）。</remarks>
    public void Confirm()
    {
        if (!_isRunning) return;
        _selectionOverlay.RequestConfirm();
    }

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化をオーバーレイへ通知する（ホストからの呼び出し）。</summary>
    /// <remarks>
    /// ビューポート実装（ScrollViewer / RenderTransform ベース等）に依存せず、ホスト（上位 AP）が<br/>
    /// ビューポート変化を検知した際に本メソッドで中継する（ScrollViewer 連携の DragRect の ViewportChanged と同等）。<br/>
    /// 暗転マスク・選択枠・ルーペの再描画・再計算を要求する。座標変換ポートは動的評価のため再描画だけで追従する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        if (!_isRunning) return;
        _selectionOverlay.NotifyViewportChanged();
    }

    /// <summary>コントロール（外観・座標変換・ルーペソース）を設定する。</summary>
    /// <remarks>セッション開始時（<see cref="RunAsync"/>）と、実行中の設定変更（<see cref="ApplySettings"/>）で呼ばれる。</remarks>
    private void ConfigureControls()
    {
        var s = _settings;
        // 外観設定（DragRect / FadeMask / Loupe を一括適用。設定変換は拡張メソッドに一元化）
        _selectionOverlay.AppearanceSettings =
            s.ToFadeMaskDragRectSettings(scaling: 1.0, positionOffset: s.LoupeOffset!.Value);
        // サイズラベルも描画時に最新の表示スケールから生成する（ズーム率表示の追従）。
        // 選択状態の真のソースは複合コントロール（HasSelection / SelectionRect）に集約する。
        _selectionOverlay.SizeLabelOverrideProvider = () =>
            _selectionOverlay.HasSelection ? FormatSizeLabel(_selectionOverlay.SelectionRect) : null;
        // 座標変換ポート: ドメイン = 論理座標（元画像ピクセル）。ズーム・スクロール・レターボックスは
        // プレビュー表示状態（Fit 判定・レイアウト）と TranslatePoint（スクロール込みの表示位置）から導出する。
        // ① プレビュー内部DIP ↔ 論理（ImageLayout は呼び出し毎にレイアウトを再評価 = ズーム・サイズ変更に追従）
        // ② 親パネルDIP ↔ プレビュー内部DIP（PreviewOrigin = レイアウト位置 + スクロール。ScrollOffset で平行移動）
        // 2 段階の変換を CoordinatePort.Compose で 1 ポートに合成し、DragRect / FadeMask / Loupe の 3 コントロールへ一括分配する。
        _selectionOverlay.SetCoordinateTransform(CoordinatePort.Compose(
            CoordinatePort.ImageLayout(
                DisplaySize,
                _sourceImageSize,
                () => previewControl.IsFit),
            CoordinatePort.ScrollOffset(() =>
            {
                // 同値の原点取得は一度だけ実行して共有する
                var origin = PreviewOrigin();
                return new Vector(origin.X, origin.Y);
            })));
        // ルーペソース（元画像）。LoupeControl.Source は IImage のため、PbBitmap を内包した IImage
        // （PbBitmapImage）を設定する（WriteableBitmap を生成しない Skia 直描画。ゼロコピー）。
        // sourceImage の所有権は呼び出し側にあり、本セッションでは破棄しない（Dispose を呼ばない）。
        _selectionOverlay.SetLoupeSource(new PbBitmapImage(sourceImage));
    }

    /// <summary>プレビューの表示サイズ（RenderTransform 込み）を親パネル上の座標差から導出する。</summary>
    /// <returns>表示サイズ。未配置（TranslatePoint が null）の場合は Bounds を返す。</returns>
    /// <remarks>
    /// ズーム表示は RenderTransform（ScaleTransform）で行われるため、<c>Bounds.Size</c> は元サイズのまま。<br/>
    /// 親パネル上の表示位置（TranslatePoint、RenderTransform 込み）の原点と右下の差から表示サイズを算出する。<br/>
    /// Width/Height 方式（Fill + 明示サイズ）でも TranslatePoint は表示サイズを返すため、両方式で共通に動作する。
    /// </remarks>
    private Size DisplaySize()
    {
        var bounds = previewControl.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return bounds.Size;
        var origin = previewControl.TranslatePoint(default, _parentPanel) ?? default;
        var bottomRight = previewControl.TranslatePoint(
            new Point(bounds.Width, bounds.Height), _parentPanel) ?? default;
        var size = new Size(bottomRight.X - origin.X, bottomRight.Y - origin.Y);
        return size.Width > 0 && size.Height > 0 ? size : bounds.Size;
    }

    /// <summary>セッション実行中に最新の設定を再適用する（Crop 中の設定変更の即時反映）。</summary>
    /// <param name="newSettings">最新の設定（null メンバーは現在値を維持）。</param>
    /// <remarks>
    /// <see cref="SnipCapture.Settings"/> の変更時に SnipCapture から呼ばれる。<br/>
    /// 外観・クリップ設定（<see cref="ConfigureControls"/>）を再適用し、オーバーレイを再描画する。<br/>
    /// セッションの選択状態・ルーペの表示位置は維持される（座標変換ポートは再設定されるが、動的評価のため追従）。
    /// </remarks>
    public void ApplySettings(CaptureSettings newSettings)
    {
        if (!_isRunning) return;
        _settings = newSettings.ToNonNull();
        ConfigureControls();
    }

    /// <summary>プレビューの親パネル上の原点（スクロール込みの表示位置）。</summary>
    private Point PreviewOrigin()
        => previewControl.TranslatePoint(default, _parentPanel) ?? default;

    /// <summary>論理座標の選択矩形からサイズ情報ラベルの文字列を生成する。</summary>
    /// <param name="rectLogical">論理座標（元画像ピクセル）の選択矩形。</param>
    /// <returns>サイズ情報ラベル文字列。</returns>
    /// <remarks>WH は閉区間（始点・終点のピクセルを含む）のピクセル数（<c>幅 + 1</c>）で表示する。</remarks>
    private string FormatSizeLabel(Rect rectLogical)
    {
        int l = (int)rectLogical.X, t = (int)rectLogical.Y;
        int r = (int)rectLogical.Right, b = (int)rectLogical.Bottom;
        // Zoom は実際の表示スケール（元画像ピクセル / DIP）から導出する（プレビュー状態の保持なし）
        double scaleX = CoordinatePort.ImageLayoutScaleX(
            DisplaySize,
            _sourceImageSize,
            () => previewControl.IsFit);
        int displayPct = (int)Math.Round(100.0 / Math.Max(scaleX, 0.01));
        return $"LT: ({l}, {t}),  RB: ({r}, {b}),  WH: ({r - l + 1}, {b - t + 1}), Zoom: ({displayPct}%)";
    }

    /// <summary>元画像サイズと選択矩形を指定して切り出し範囲を論理座標（元画像ピクセル）で返す。</summary>
    /// <param name="logicalRect">選択矩形（ドメイン座標 = 論理座標）。</param>
    /// <returns>切り出し範囲。最小サイズ未満の場合は 0x0 の空 PixelRect。</returns>
    /// <remarks>
    /// 切り出し範囲は閉区間（始点・終点のピクセルを含む）とする。<br/>
    /// DragRect の矩形は半開区間（<c>幅 = 終点 − 始点</c>）のため、<c>Width / Height を +1</c> して<br/>
    /// 終点のピクセルまで切り出す（仕様: 選択矩形の始点・終点は切出し対象に含まれる）。<br/>
    /// 画像右端・下端では <c>+1</c> が画像サイズを超えないようクランプする。<br/>
    /// 単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal PixelRect ExecuteCropForSize(Rect logicalRect)
        => ComputeCropBounds(logicalRect, _sourceImageSize, _settings.MinSelectionSize!.Value);

    /// <summary>選択矩形（半開区間）を閉区間の切り出し範囲（論理座標）へ変換する（純粋関数）。</summary>
    /// <param name="logicalRect">選択矩形（ドメイン座標 = 論理座標・半開区間）。</param>
    /// <param name="sourceImageSize">元画像のピクセルサイズ。</param>
    /// <param name="minSize">最小サイズ（ドメイン座標）。</param>
    /// <returns>切り出し範囲。最小サイズ未満・範囲外の場合は 0x0 の空 PixelRect。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 切り出し範囲は閉区間（始点・終点のピクセルを含む）とする。<br/>
    /// DragRect の矩形は半開区間（<c>幅 = 終点 − 始点</c>）のため、<c>Width / Height を +1</c> して<br/>
    /// 終点のピクセルまで切り出す（仕様: 選択矩形の始点・終点は切出し対象に含まれる）。<br/>
    /// 画像右端・下端では <c>+1</c> が画像サイズを超えないようクランプする。<br/>
    /// </remarks>
    internal static PixelRect ComputeCropBounds(Rect logicalRect, Size sourceImageSize, double minSize)
    {
        if (logicalRect.Width < minSize || logicalRect.Height < minSize) return default;
        double l = Math.Max(logicalRect.X, 0), t = Math.Max(logicalRect.Y, 0);
        double rb = Math.Min(logicalRect.Right, sourceImageSize.Width), bt = Math.Min(logicalRect.Bottom, sourceImageSize.Height);
        if (rb <= l || bt <= t) return default;
        // 閉区間（+1）: 始点・終点のピクセルを含む。画像右端・下端でクランプ
        int x = (int)l, y = (int)t;
        int w = Math.Min((int)(rb - l) + 1, (int)sourceImageSize.Width - x);
        int h = Math.Min((int)(bt - t) + 1, (int)sourceImageSize.Height - y);
        return new PixelRect(x, y, w, h);
    }

    /// <summary>指定コントロールの祖先にある ScrollViewer を探索する。</summary>
    /// <param name="control">探索開始コントロール。</param>
    /// <returns>最初に見つかった ScrollViewer。存在しない場合は null。</returns>
    /// <remarks>DragRect への明示注入（DragRectControl.AttachScrollViewer）用。</remarks>
    private static ScrollViewer? FindAncestorScrollViewer(Control control)
    {
        for (var v = control.GetVisualParent(); v is not null; v = v.GetVisualParent())
        {
            if (v is ScrollViewer scrollViewer) return scrollViewer;
        }
        return null;
    }

    /// <summary>指定コントロールの祖先にある Panel を探索する。</summary>
    /// <param name="control">探索開始コントロール。</param>
    /// <returns>最初に見つかった Panel。存在しない場合は null。</returns>
    private static Panel? FindAncestorPanel(Control control)
    {
        var parent = control.Parent;
        while (parent is not null)
        {
            if (parent is Panel panel) return panel;
            parent = parent.Parent;
        }
        return null;
    }
}


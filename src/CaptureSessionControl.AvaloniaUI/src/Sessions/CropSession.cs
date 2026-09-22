using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Size = Avalonia.Size;
using Estable.CaptureSession;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Native;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Estable.AvaloniaUI.CaptureSession.Logging;
using Estable.AvaloniaUI.CropView;
using Estable.AvaloniaUI.FadeMaskDragRect;
using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.CaptureSession;

/// <summary>切出し（Crop）セッション（CropViewControl のライフサイクル管理と確定・キャンセル処理）。</summary>
/// <remarks>
/// 内部実装であり、公開 API は <see cref="CaptureSessionControl.StartCropAsync"/> を使用する。<br/>
/// <br/>
/// 状態は持たない（選択矩形・ルーペ表示状態・暗転マスクは <see cref="CropViewControl"/> が保持し、本クラスはライフサイクルのみ）。<br/>
/// - 配線（SelectionChanged → 暗転マスク同期 / LoupePositionMoved → ルーペ更新 / 確定・キャンセル）は<br/>
///   複合コントロール内で完結する（ホストの配線コードは不要）。<br/>
/// - ドメイン = 元画像ピクセル（論理座標）。座標変換は <see cref="CropViewControl"/> が内包する<br/>
///   <see cref="ZoomViewControl"/>（ズーム・パン・Fit）へ委譲する（ズーム座標の単一所有）。<br/>
/// <br/>
/// プレビュー状態（ScrollOffset / ViewportOrigin / ZoomPercent）は保持しない。<br/>
/// オーバーレイ（DragRect / FadeMask / Loupe）とズームビューポートは <see cref="CropViewControl"/> 内で<br/>
/// 同一座標空間に配置されるため、座標変換は「ビューポートDIP ↔ 元画像ピクセル」の 1 段で完結する。
/// </remarks>
internal sealed class CropSession(
    CropViewControl cropView,
    Window hostWindow,
    EsBitmap sourceImage,
    CaptureSettings settings,
    ILogger? logger = null)
{
    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger.OrNullLogger();

    /// <summary>元画像のピクセルサイズ（論理座標系の範囲）。</summary>
    private readonly Size _sourceImageSize = new(sourceImage.Size.Width, sourceImage.Size.Height);

    /// <summary>カーソル上書き抑止（Crop 中の十字カーソル維持）。</summary>
    private readonly ICursorOverrideGuard _cursorGuard = PlatformFactory.CreateCursorOverrideGuard();

    /// <summary>座標変換ポート（ビューポートDIP ↔ 元画像ピクセル）。一度だけ生成して再利用する。</summary>
    private readonly ICoordTransform _coordinatePort = CreateCoordinatePort(cropView, sourceImage);

    /// <summary>ルーペソース用の画像アダプタ（<see cref="sourceImage"/> を内包。一度だけ生成して再利用する）。</summary>
    private readonly EsBitmapAvaloniaImage _loupeSourceImage = new(sourceImage);

    /// <summary>設定（全メンバー non-null）。実行中に <see cref="ApplySettings"/> で更新できる。</summary>
    private CaptureSettings _settings = settings.ToNonNull();

    /// <summary>セッションの状態（再入禁止・完了は 1 回だけ）。</summary>
    private readonly CaptureSessionState _state = new();

    /// <summary>セッション結果の完了通知（確定・キャンセルの最初の 1 件を採用する）。</summary>
    private readonly CaptureCompletion<PixelRect?> _completion = new();

    /// <summary>選択矩形が有効かどうか（複合コントロールの選択状態）。ホスト（CaptureSessionControl）の操作分岐用。</summary>
    /// <remarks>選択状態の真のソースは FadeMask レンダラー（<see cref="CropViewControl.HasSelection"/>）に集約する。</remarks>
    public bool HasSelection => cropView.HasSelection;

    /// <summary>ビューポートDIP ↔ 元画像ピクセルの座標変換ポートを生成する。</summary>
    /// <param name="cropView">CropViewControl（ズームビューポートとオーバーレイを内包）。</param>
    /// <param name="sourceImage">元画像（論理座標系の基準）。</param>
    /// <returns>Forward = ビューポートDIP → 元画像ピクセル、Inverse = 元画像ピクセル → ビューポートDIP、Bounds = 元画像サイズのポート。</returns>
    /// <remarks>
    /// 内包する <see cref="ZoomViewControl"/> の座標変換（<see cref="ZoomViewControl.ViewportToContentImage"/> /
    /// <see cref="ZoomViewControl.ContentToViewportImage"/>）を委譲する。これにより、ズーム・パン・Fit・中央寄せの<br/>
    /// 原点補正を全て内包ビューポートが単一所有し、ホストが変換を重複実装する必要がない（DRY 原則）。
    /// </remarks>
    private static ICoordTransform CreateCoordinatePort(CropViewControl cropView, EsBitmap sourceImage)
    {
        var size = new EsSize(sourceImage.Size.Width, sourceImage.Size.Height);
        return new CoordTransform(
            forward: dip => cropView.ViewportToContent(dip.ToAvaloniaPoint()).ToEsPoint(),
            inverse: logical => cropView.ContentToViewport(logical.ToAvaloniaPoint()).ToEsPoint(),
            bounds: () => size);
    }

    /// <summary>切出しセッションを実行する（確定待機 → 結果返却）。UI スレッドから呼び出すこと。</summary>
    /// <returns>確定時は元画像上の切り出し範囲、キャンセル時は 0x0 の空 PixelRect。</returns>
    public Task<PixelRect?> RunAsync()
    {
        if (!_state.TryBegin())
        {
            return Task.FromResult<PixelRect?>(null); // 再入禁止
        }

        // 外観・座標変換・ルーペソースを設定する（CropViewControl は既に配置済み。追加・除去はしない）
        ConfigureControls();
        // 領域選択オーバーレイを可視化する（プレビュー表示時は非表示。Crop セッション中のみ表示して暗転を出す）。
        cropView.SetSelectionOverlayVisible(true);
        _log.LogCropStart((int)_sourceImageSize.Width, (int)_sourceImageSize.Height);

        void OnConfirm(Rect logicalRect)
        {
            if (!_state.TryComplete()) return; // 二重完了を防ぐ
            var bounds = ExecuteCropForSize(logicalRect);
            _log.LogCropCompleted(bounds.ToString());
            Cleanup();
            _completion.TrySetResult(bounds);
        }

        void OnCancel()
        {
            if (!_state.TryComplete()) return; // 二重完了を防ぐ
            _log.LogCropCancelled();
            Cleanup();
            _completion.TrySetResult(default); // 0x0 の空 PixelRect
        }

        void OnConfirmEvent(object? sender, FadeMaskDragRectSelectionEventArgs e) => OnConfirm(e.SelectionRect);

        void OnCancelEvent(object? sender, EventArgs e) => OnCancel();

        void Cleanup()
        {
            hostWindow.Deactivated -= OnDeactivated;
            cropView.ConfirmRequested -= OnConfirmEvent;
            cropView.CancelRequested -= OnCancelEvent;
            _cursorGuard.Dispose();
            cropView.ClearLoupe();
            // 確定・キャンセル後は領域選択オーバーレイを非表示に戻す（プレビュー表示時は非表示のため）。
            // 選択状態のクリアとオーバーレイの非表示化を一括で行う（暗転マスクの残留を防ぐ）。
            cropView.SetSelectionOverlayVisible(false);
            hostWindow.Cursor = Cursor.Default;
            _state.End();
        }

        void OnDeactivated(object? s, EventArgs e) => OnCancel();

        // イベント配線（配線は複合コントロール内で完結。ホストは確定・キャンセルのみ購読）
        cropView.ConfirmRequested += OnConfirmEvent;
        cropView.CancelRequested += OnCancelEvent;
        hostWindow.Deactivated += OnDeactivated;

        // カーソル上書き抑制（WM_SETCURSOR による十字カーソルの上書き防止）
        _cursorGuard.Attach(hostWindow);
        cropView.FocusDragRect();

        return _completion.Task;
    }

    /// <summary>進行中の切出しセッションをプログラム的にキャンセルする。</summary>
    /// <remarks>DragRect にキャンセル要求を委譲する（選択破棄 + CancelRequested 発火 → OnCancelEvent 経由で完了）。</remarks>
    public void Cancel()
    {
        if (!_state.IsRunning) return;
        cropView.RequestCancel();
    }

    /// <summary>進行中の切出しセッションを現在の選択矩形で確定する（Enter キー相当）。</summary>
    /// <remarks>DragRect に確定要求を委譲する（選択確定時のみ ConfirmRequested 発火 → OnConfirmEvent 経由で完了）。</remarks>
    public void Confirm()
    {
        if (!_state.IsRunning) return;
        cropView.RequestConfirm();
    }

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化をオーバーレイへ通知する（ホストからの呼び出し）。</summary>
    /// <remarks>
    /// ビューポート実装（ScrollViewer / RenderTransform ベース等）に依存せず、ホスト（上位 AP）が<br/>
    /// ビューポート変化を検知した際に本メソッドで中継する（ScrollViewer 連携の DragRect の ViewportChanged と同等）。<br/>
    /// 暗転マスク・選択枠・ルーペの再描画・再計算を要求する。座標変換ポートは動的評価のため再描画だけで追従する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        if (!_state.IsRunning) return;
        cropView.NotifyViewportChanged();
    }

    /// <summary>コントロール（外観・座標変換・ルーペソース）を設定する。</summary>
    /// <remarks>セッション開始時（<see cref="RunAsync"/>）と、実行中の設定変更（<see cref="ApplySettings"/>）で呼ばれる。</remarks>
    private void ConfigureControls()
    {
        var s = _settings;
        // 設定を複合コントロールの設定値（1 値）へ変換して一括適用する（変換はコアのマッパーが単一所有する）
        cropView.SelectionOverlayOptions = s.ToFadeMaskDragRectOptions();
        // DPI スケーリング・ルーペ描画スケールは設定（Options）の対象外（環境値。コードから動的設定）。
        // プレビュー内 Crop はドメイン = 論理座標（元画像ピクセル）のため Scaling は 1.0 固定。
        cropView.Scaling = 1.0;
        cropView.LoupeRenderScaling = s.LoupeRenderScaling!.Value;
        // サイズラベルも描画時に最新のズーム率から生成する（ズーム率表示の追従）。
        cropView.SizeLabelOverrideProvider = () =>
            cropView.HasSelection ? FormatSizeLabel(cropView.SelectionRect) : null;
        // 座標変換ポート: ドメイン = 論理座標（元画像ピクセル）。ズーム・パン・Fit・中央寄せの原点補正は
        // 内包する ZoomViewControl が単一所有する（CoordinatePort はそれを委譲）。
        cropView.SetCoordinateTransform(_coordinatePort);
        // ルーペソース（元画像）。LoupeControl.Source は IImage のため、EsBitmap を内包した IImage
        // （EsBitmapAvaloniaImage）を設定する（WriteableBitmap を生成しない Skia 直描画。ゼロコピー）。
        // sourceImage の所有権は呼び出し側にあり、本セッションでは破棄しない（Dispose を呼ばない）。
        // アダプタはフィールドで一度だけ生成して再利用する（ConfigureControls の複数呼び出しでのリーク・再描画防止）。
        cropView.SetLoupeSource(_loupeSourceImage);
    }

    /// <summary>セッション実行中に最新の設定を再適用する（Crop 中の設定変更の即時反映）。</summary>
    /// <param name="newSettings">最新の設定（null メンバーは現在値を維持）。</param>
    /// <remarks>
    /// <see cref="CaptureSessionControl.AppearanceSettings"/> の変更時に CaptureSessionControl から呼ばれる。<br/>
    /// 外観・クリップ設定（<see cref="ConfigureControls"/>）を再適用し、オーバーレイを再描画する。<br/>
    /// セッションの選択状態・ルーペの表示位置は維持される（座標変換ポートは再設定されるが、動的評価のため追従）。
    /// </remarks>
    public void ApplySettings(CaptureSettings newSettings)
    {
        if (!_state.IsRunning) return;
        _settings = newSettings.ToNonNull();
        ConfigureControls();
    }

    /// <summary>論理座標の選択矩形からサイズ情報ラベルの文字列を生成する。</summary>
    /// <param name="rectLogical">論理座標（元画像ピクセル）の選択矩形。</param>
    /// <returns>サイズ情報ラベル文字列。</returns>
    /// <remarks>WH は閉区間（始点・終点のピクセルを含む）のピクセル数（<c>幅 + 1</c>）で表示する。</remarks>
    private string FormatSizeLabel(Rect rectLogical)
    {
        int l = (int)rectLogical.X, t = (int)rectLogical.Y;
        int r = (int)rectLogical.Right, b = (int)rectLogical.Bottom;
        // ズーム率は内包する ZoomViewControl の実効ズーム率を使用する（元画像ピクセル / DIP の可視倍率）。
        int displayPct = (int)Math.Round(cropView.ZoomPercent);
        return $"LT: ({l}, {t}),  RB: ({r}, {b}),  WH: ({r - l + 1}, {b - t + 1}), Zoom: ({displayPct}%)";
    }

    /// <summary>元画像サイズと選択矩形を指定して切り出し範囲を論理座標（元画像ピクセル）で返す。</summary>
    /// <param name="logicalRect">選択矩形（ドメイン座標 = 論理座標）。</param>
    /// <returns>切り出し範囲。最小サイズ未満の場合は 0x0 の空 PixelRect。</returns>
    /// <remarks>
    /// 切り出し範囲は閉区間（始点・終点のピクセルを含む）とする。<br/>
    /// 計算は <see cref="CropBoundsMath.ComputeCropBounds(EsRect, EsSize, double, bool)"/>（コア）が単一所有する<br/>
    /// （MewUI 版と同じ規則。画像右端・下端のクランプも含む）。<br/>
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
        var bounds = CropBoundsMath.ComputeCropBounds(
            new EsRect(logicalRect.X, logicalRect.Y, logicalRect.Width, logicalRect.Height),
            new EsSize(sourceImageSize.Width, sourceImageSize.Height),
            minSize);
        return bounds.IsEmpty
            ? default
            : new PixelRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }
}

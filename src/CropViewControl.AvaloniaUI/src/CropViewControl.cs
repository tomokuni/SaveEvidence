using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Estable.AvaloniaUI.FadeMaskDragRect;
using Estable.AvaloniaUI.Loupe;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.ZoomView;
using Estable.CropView;
using Estable.FadeMaskDragRect;
using Estable.Platform.Coordinate;
using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.CropView;

/// <summary>
/// 画像表示（拡大・パン・Fit）と領域選択（ドラッグ・暗転・ルーペ）を統合した高レベル複合コントロール。
/// </summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（<see cref="ZoomViewControl"/> と <see cref="FadeMaskDragRectControl"/> を内包し、
/// ホストへ統一インターフェースを提供）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="ZoomViewControl"/>: 画像ビューポート（拡大・パン・Fit・原寸）。<br/>
/// - <see cref="FadeMaskDragRectControl"/>: 領域選択オーバーレイ（ドラッグ選択・暗転マスク・ルーペ）。<br/>
/// - 矩形オーバーレイ: ホストから渡された <see cref="OverlayRect"/> をビューポート座標へ変換して表示する。<br/>
/// <br/>
/// <b>責務分離</b>:<br/>
/// - 画像表示（ズーム・パン）は <see cref="ZoomViewControl"/> が担う（ズーム率管理は本コントロール）。<br/>
/// - 領域選択（ドラッグ・暗転・ルーペ）は <see cref="FadeMaskDragRectControl"/> が担う。<br/>
/// - 外部提供矩形（<see cref="Rects"/>）の描画は本コントロールが担う（View / ルーペの両方へ描画）。<br/>
/// <br/>
/// <b>ライフサイクル</b>:<br/>
/// - 画像（<see cref="Source"/>）はホストが設定する。<br/>
/// - ズーム操作（<see cref="ZoomIn"/> / <see cref="ZoomOut"/> / Fit）は本コントロールが提供する。<br/>
/// - 選択状態の外部設定（<see cref="SetSelection"/> / <see cref="ClearSelection"/>）は <see cref="FadeMaskDragRectControl"/> へ委譲する。
/// </remarks>
public sealed partial class CropViewControl : Panel
{
    /// <summary>ズーム・パンビューポート（自己完結型複合コントロール。拡大・パン・Fit）。</summary>
    private readonly ZoomViewControl _zoomView = new() { ZIndex = 0 };

    /// <summary>領域選択オーバーレイ（ドラッグ選択・暗転マスク・ルーペ）。</summary>
    /// <remarks>画像がない間は <see cref="Source"/> に応じてヒットテストを切るとともに非表示にする（画像なしでの選択矩形作成と全面暗転を防止）。</remarks>
    private readonly FadeMaskDragRectControl _selectionOverlay = new() { ZIndex = 10, IsHitTestVisible = false, IsVisible = false };

    /// <summary>内部ロガー（ズームビューポートへ伝搬）。null の場合は <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger"/>。</summary>
    /// <remarks><see cref="ZoomViewControl.Log"/> へ委譲する（ズーム操作・座標変換の詳細ログをホストのログ基盤へ転送）。</remarks>
    public ILogger Log
    {
        get => _zoomView.Log;
        set => _zoomView.Log = value;
    }

    /// <summary>外部提供矩形オーバーレイ（ビューポート座標で描画）。</summary>
    private readonly Canvas _rectOverlay = new() { ZIndex = 20, IsHitTestVisible = false };

    /// <summary>
    /// <see cref="CropViewControl"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <remarks>
    /// 子コントロール（<see cref="ZoomViewControl"/> / <see cref="FadeMaskDragRectControl"/> / 矩形オーバーレイ）を生成して配置し、<br/>
    /// ビューポート変化（パン・ズーム）を配線する。<br/>
    /// ズーム要求（Ctrl+ホイール）・ズーム率の遷移は <see cref="ZoomViewControl"/> が自己完結する。
    /// </remarks>
    public CropViewControl()
    {
        _zoomView.HorizontalAlignment = HorizontalAlignment.Stretch;
        _zoomView.VerticalAlignment = VerticalAlignment.Stretch;
        Children.Add(_zoomView);
        Children.Add(_selectionOverlay);
        Children.Add(_rectOverlay);

        // ビューポート変化（パン・ズーム）を領域選択オーバーレイと矩形オーバーレイへ中継する
        _zoomView.ViewportChanged += (_, _) =>
        {
            _selectionOverlay.NotifyViewportChanged();
            UpdateRectOverlay();
        };

        // ルーペ描画完了時に、外部提供矩形をルーペ Bitmap へ描画する
        _selectionOverlay.LoupeRendered += OnLoupeRendered;

        // 既定は Crop（プレビュー内の切出し）。ホストが SelectionMode で変更できる
        _selectionOverlay.Mode = FadeMaskDragRectMode.Crop;
    }

    // ====================================================================
    // 公開 API: 選択モード
    // ====================================================================

    /// <summary>選択モード（既定は <see cref="FadeMaskDragRectMode.Crop"/>）。</summary>
    /// <remarks>
    /// <see cref="FadeMaskDragRectControl.Mode"/> へ委譲する（内包コントロールの有効・無効と描画モードを一括切替）。
    /// </remarks>
    public FadeMaskDragRectMode SelectionMode
    {
        get => _selectionOverlay.Mode;
        set => _selectionOverlay.Mode = value;
    }

    /// <summary>領域選択オーバーレイの外観設定（DragRect / FadeMask / Loupe の外観）。<see cref="FadeMaskDragRectControl.AppearanceSettings"/> へ委譲する。</summary>
    public FadeMaskDragRectSettings AppearanceSettings
    {
        get => _selectionOverlay.AppearanceSettings;
        set => _selectionOverlay.AppearanceSettings = value;
    }

    /// <summary>DPI スケーリング係数（<see cref="FadeMaskDragRectControl.Scaling"/> へ委譲）。</summary>
    /// <remarks>ユーザー設定ではなく、セッション（Crop は 1.0）がコードから動的設定する。</remarks>
    public double Scaling
    {
        get => _selectionOverlay.Scaling;
        set => _selectionOverlay.Scaling = value;
    }

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正）。<see cref="FadeMaskDragRectControl.DisplayScaleScaling"/> へ委譲する。</summary>
    /// <remarks>ユーザー設定ではなく、セッションがコードから動的設定する。0 以下は <see cref="Scaling"/> と同値。</remarks>
    public double DisplayScaleScaling
    {
        get => _selectionOverlay.DisplayScaleScaling;
        set => _selectionOverlay.DisplayScaleScaling = value;
    }

    /// <summary>ルーペの DIP → 物理ピクセル倍率（0.1〜8.0）。<see cref="FadeMaskDragRectControl.LoupeRenderScaling"/> へ委譲する。</summary>
    /// <remarks>ユーザー設定ではなく、ウィンドウの DPI スケーリングから自動取得する。</remarks>
    public double LoupeRenderScaling
    {
        get => _selectionOverlay.LoupeRenderScaling;
        set => _selectionOverlay.LoupeRenderScaling = value;
    }

    /// <summary>表示する画像を設定する（ZoomViewControl とルーペへ伝搬する）。</summary>
    /// <remarks>
    /// 画像変更時は Fit 表示にリセットし、ズーム状態を適用する。<br/>
    /// オーバーレイ（領域選択）の可視性は本プロパティでは制御しない。<br/>
    /// 領域選択オーバーレイの可視性は <see cref="SetSelectionOverlayVisible"/>（Crop セッションが開始・終了時に制御）に一本化する。<br/>
    /// 画像が <c>null</c> に変わるときは既存の選択をクリアする（画像なしで選択矩形が残るのを防ぐ）。
    /// </remarks>
    public IImage? Source
    {
        get => _source;
        set
        {
            _source = value;
            _zoomView.Source = value;
            UpdateLoupeSource();
            // 画像が null に変わるときは選択状態をクリアする。
            // （オーバーレイの表示・ヒットテストは SetSelectionOverlayVisible が専任。ここでは触らない）
            if (value is null)
            {
                ClearSelection();
            }
        }
    }

    private IImage? _source;

    /// <summary>ルーペの拡大元画像を <see cref="FadeMaskDragRectControl"/> へ設定する。</summary>
    private void UpdateLoupeSource() => _selectionOverlay.SetLoupeSource(_source);

    // ====================================================================
    // 公開 API: 外部提供矩形
    // ====================================================================

    /// <summary>オーバーレイとして表示する矩形の一覧（ドメイン座標）。</summary>
    /// <remarks>
    /// 設定すると View（ビューポート）とルーペ画像の両方へ描画される。<br/>
    /// 矩形情報（<see cref="OverlayRect"/>）には破線種類・前景色・背景色・線幅が含まれる。
    /// </remarks>
    public IReadOnlyList<OverlayRect> Rects
    {
        get => _rects;
        set
        {
            _rects = value;
            UpdateRectOverlay();
            RaiseRectOverlayChanged();
        }
    }

    private IReadOnlyList<OverlayRect> _rects = [];

    // ====================================================================
    // 公開 API: ズーム
    // ====================================================================

    /// <summary>現在のズーム率（%）。0 = Fit（自動）。変更時は <see cref="ZoomViewControl"/> が表示を適用する。</summary>
    /// <remarks>
    /// ズーム率は <see cref="ZoomViewControl"/> が単一所有するため、getter / setter は <see cref="ZoomViewControl.ZoomPercent"/> へ委譲する。<br/>
    /// （表示・通知は <see cref="ZoomViewControl"/> が行う）。<c>double</c> でスライダ等の連続値も設定できる。
    /// </remarks>
    public double ZoomPercent
    {
        get => _zoomView.ZoomPercent;
        set => _zoomView.ZoomPercent = value;
    }

    /// <summary>ズームインする（⊖⊕ボタン相当。段階遷移）。</summary>
    public void ZoomIn() => _zoomView.ZoomIn();

    /// <summary>ズームアウトする（⊖⊕ボタン相当。段階遷移）。</summary>
    public void ZoomOut() => _zoomView.ZoomOut();

    /// <summary>Fit 表示（ウィンドウに合わせて画像を表示）に戻す。</summary>
    /// <remarks><see cref="ZoomViewControl.ZoomFit"/> へ委譲する（ズーム率の単一所有）。<see cref="ZoomIn"/> / <see cref="ZoomOut"/> と対になるズーム操作。</remarks>
    public void ZoomFit() => _zoomView.ZoomFit();

    /// <summary>現在 Fit 表示中かどうか（ズーム率が 0 = 自動）。</summary>
    /// <value>Fit 表示中なら true。</value>
    /// <remarks><see cref="ZoomViewControl.IsFit"/> へ委譲する。ホストは内部 <c>Image.Stretch</c> に依存せず Fit 状態を判定できる。</remarks>
    public bool IsFit => _zoomView.IsFit;

    /// <summary>内包するズーム・パンビューポート（CaptureSessionControl の Crop セッションが座標変換・Fit 状態を利用する）。</summary>
    /// <remarks>
    /// <see cref="ZoomViewControl"/> を公開する（<c>StartCropAsync</c> の引数として利用するため）。<br/>
    /// ズーム操作（<see cref="ZoomPercent"/> / <see cref="ZoomIn"/> / <see cref="ZoomOut"/> / <see cref="ZoomFit"/>）は本コントロールの公開 API へ委譲済みで、
    /// 通常のホストは <see cref="ZoomView"/> を直接参照する必要はない。<br/>
    /// CaptureSessionControl の Crop セッション（<see cref="ZoomView"/> の <see cref="ZoomViewControl.IsFit"/> 等）が利用する。
    /// </remarks>
    public ZoomViewControl ZoomView => _zoomView;

    // ====================================================================
    // 公開 API: ズーム（ZoomViewControl へ委譲）
    // ====================================================================

    /// <summary>ズーム率が変更されたときに発火するイベント（<see cref="ZoomViewControl.ZoomChanged"/> へ委譲）。</summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged
    {
        add => _zoomView.ZoomChanged += value;
        remove => _zoomView.ZoomChanged -= value;
    }

    /// <summary>ビューポート（パン・ズーム・表示状態）が変化したときに発火するイベント（<see cref="ZoomViewControl.ViewportChanged"/> へ委譲）。</summary>
    public event EventHandler? ViewportChanged
    {
        add => _zoomView.ViewportChanged += value;
        remove => _zoomView.ViewportChanged -= value;
    }

    /// <summary>現在のズーム状態でズームインが可能かどうか（<see cref="ZoomViewControl.CanZoomIn"/> へ委譲）。</summary>
    public bool CanZoomIn => _zoomView.CanZoomIn;

    /// <summary>現在のズーム状態でズームアウトが可能かどうか（<see cref="ZoomViewControl.CanZoomOut"/> へ委譲）。</summary>
    public bool CanZoomOut => _zoomView.CanZoomOut;

    /// <summary>画像の中央寄せ / 左上寄せ（<see cref="ZoomViewControl.CenterAlign"/> へ委譲）。</summary>
    public bool CenterAlign
    {
        get => _zoomView.CenterAlign;
        set => _zoomView.CenterAlign = value;
    }

    /// <summary>画像の表示矩形（ビューポート座標）を取得する（<see cref="ZoomViewControl.ImageRectViewport"/> へ委譲）。</summary>
    public Rect ImageRectViewport() => _zoomView.ImageRectViewport();

    /// <summary>ズーム・パンビューポートの背景（市松模様等。レターボックス領域の描画）。<see cref="ZoomViewControl.Background"/> へ委譲する。</summary>
    /// <remarks>
    /// 本コントロール自体の背景（<see cref="Panel.Background"/>）とは独立し、内包するビューポートの背景を設定する。<br/>
    /// 画像のレターボックス領域（画像がビューポートより小さい場合の余白）に市松模様等を表示するために使用する。
    /// </remarks>
    public IBrush? ViewportBackground
    {
        get => _zoomView.Background;
        set => _zoomView.Background = value;
    }

    // ====================================================================
    // 公開 API: 選択状態（FadeMaskDragRectControl へ委譲）
    // ====================================================================

    /// <summary>領域選択オーバーレイ（テスト用アクセサ。画像なし時のドラッグ無効化の検証用）。</summary>
    internal FadeMaskDragRectControl SelectionOverlay => _selectionOverlay;

    /// <summary>現在の選択状態（選択矩形の有無）。</summary>
    public bool HasSelection => _selectionOverlay.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）。未選択時は 0x0 の空矩形。</summary>
    public Rect SelectionRect => _selectionOverlay.SelectionRect;

    /// <summary>選択状態を外部から設定する（暗転マスクとルーペへ同期）。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。サイズ 0 以下は選択なし。</param>
    public void SetSelection(Rect rect) => _selectionOverlay.SetSelection(rect);

    /// <summary>選択状態をクリアする（全面暗転に戻す）。</summary>
    public void ClearSelection() => _selectionOverlay.ClearSelection();

    /// <summary>領域選択オーバーレイの可視性とヒットテストを設定する（Crop セッションの開始・終了時に制御）。</summary>
    /// <param name="visible">true でオーバーレイを可視化しヒットテストを有効化する。false で非表示かつヒットテストを無効化する。</param>
    /// <remarks>
    /// オーバーレイはプレビュー表示時（キャプチャ完了・画像読込）は非表示で、Crop セッション中のみ表示する。<br/>
    /// Crop モード（<see cref="SelectionMode"/>）では全面暗転（FadeMask）を描画するため、プレビュー表示時に勝手に表示すると<br/>
    /// 「Crop を開始した状態」のような暗転が残る。<see cref="Source"/> 設定では自動表示せず、本メソッドで明示的に制御する。
    /// </remarks>
    public void SetSelectionOverlayVisible(bool visible)
    {
        _selectionOverlay.IsVisible = visible;
        _selectionOverlay.IsHitTestVisible = visible;
        if (!visible)
        {
            ClearSelection();
        }
    }

    /// <summary>座標変換ポートを設定する（内包コントロールへ分配）。</summary>
    /// <param name="port">座標変換ポート。null で等価変換。</param>
    public void SetCoordinateTransform(ICoordTransform? port) => _selectionOverlay.SetCoordinateTransform(port);

    /// <summary>祖先 ScrollViewer を内包 DragRect へ明示注入する（スクロール・ズーム検知 + ホイール操作。Crop 用）。</summary>
    /// <param name="scrollViewer">連携する ScrollViewer。null で連携解除。</param>
    /// <remarks><see cref="FadeMaskDragRectControl.AttachScrollViewer"/> へ委譲する。</remarks>
    public void AttachScrollViewer(ScrollViewer? scrollViewer) => _selectionOverlay.AttachScrollViewer(scrollViewer);

    /// <summary>内包 DragRect へフォーカスを設定する（矢印キー nudge の受付開始）。</summary>
    /// <remarks><see cref="FadeMaskDragRectControl.FocusDragRect"/> へ委譲する。</remarks>
    public void FocusDragRect() => _selectionOverlay.FocusDragRect();

    /// <summary>ルーペの拡大元画像を設定する（<see cref="FadeMaskDragRectControl.SetLoupeSource"/> へ委譲）。</summary>
    /// <param name="source">ルーペの拡大元画像。null でルーペをクリア。</param>
    public void SetLoupeSource(IImage? source) => _selectionOverlay.SetLoupeSource(source);

    /// <summary>ルーペのソースをクリアする（<see cref="FadeMaskDragRectControl.ClearLoupe"/> へ委譲）。</summary>
    public void ClearLoupe() => _selectionOverlay.ClearLoupe();

    /// <summary>サイズラベルを描画時に評価するプロバイダ（<see cref="FadeMaskDragRectControl.SizeLabelOverrideProvider"/> へ委譲）。</summary>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _selectionOverlay.SizeLabelOverrideProvider;
        set => _selectionOverlay.SizeLabelOverrideProvider = value;
    }

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化を内包オーバーレイへ通知する。</summary>
    /// <remarks><see cref="FadeMaskDragRectControl.NotifyViewportChanged"/> へ委譲する（暗転マスク・選択枠・ルーペの追従）。</remarks>
    public void NotifyViewportChanged() => _selectionOverlay.NotifyViewportChanged();

    /// <summary>選択確定要求を <see cref="FadeMaskDragRectControl"/> へ委譲する（Enter キー相当）。</summary>
    public void RequestConfirm() => _selectionOverlay.RequestConfirm();

    /// <summary>選択キャンセル要求を <see cref="FadeMaskDragRectControl"/> へ委譲する（Escape キー相当）。</summary>
    public void RequestCancel() => _selectionOverlay.RequestCancel();

    /// <summary>選択確定要求イベント（<see cref="FadeMaskDragRectControl.ConfirmRequested"/> へ委譲）。</summary>
    public event EventHandler<FadeMaskDragRectSelectionEventArgs>? ConfirmRequested
    {
        add => _selectionOverlay.ConfirmRequested += value;
        remove => _selectionOverlay.ConfirmRequested -= value;
    }

    /// <summary>選択キャンセル要求イベント（<see cref="FadeMaskDragRectControl.CancelRequested"/> へ委譲）。</summary>
    public event EventHandler? CancelRequested
    {
        add => _selectionOverlay.CancelRequested += value;
        remove => _selectionOverlay.CancelRequested -= value;
    }

    /// <summary>ズーム要求イベント（<see cref="FadeMaskDragRectControl.ZoomRequested"/> へ委譲）。</summary>
    public event EventHandler<bool>? ZoomRequested
    {
        add => _selectionOverlay.ZoomRequested += value;
        remove => _selectionOverlay.ZoomRequested -= value;
    }

    /// <summary>ルーペ中心の更新イベント（<see cref="FadeMaskDragRectControl.LoupePositionMoved"/> へ委譲）。</summary>
    public event EventHandler<FadeMaskDragRectPointEventArgs>? LoupePositionMoved
    {
        add => _selectionOverlay.LoupePositionMoved += value;
        remove => _selectionOverlay.LoupePositionMoved -= value;
    }

    // ====================================================================
    // 矩形オーバーレイ（外部提供矩形の描画）
    // ====================================================================

    /// <summary>矩形オーバーレイを再描画する。</summary>
    /// <remarks>
    /// <see cref="Rects"/> の各矩形を <see cref="ContentToViewport"/>（画像表示矩形の原点基準）でビューポート座標へ変換し、<br/>
    /// <see cref="_rectOverlay"/>（Canvas）へ <c>Rectangle</c> を配置する。
    /// </remarks>
    private void UpdateRectOverlay()
    {
        _rectOverlay.Children.Clear();
        if (Source is null || Rects.Count == 0)
        {
            return;
        }

        foreach (var rect in Rects)
        {
            var topLeft = ContentToViewport(new Point(rect.Bounds.X, rect.Bounds.Y));
            var bottomRight = ContentToViewport(new Point(rect.Bounds.Right, rect.Bounds.Bottom));

            var rectElement = CreateRectElement(topLeft, bottomRight, rect);

            Canvas.SetLeft(rectElement, topLeft.X);
            Canvas.SetTop(rectElement, topLeft.Y);
            _rectOverlay.Children.Add(rectElement);
        }
    }

    /// <summary>ビューポート座標をドメイン座標（元画像ピクセル）へ変換する。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <returns>ドメイン座標。</returns>
    /// <remarks>
    /// 画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="ZoomViewControl.ViewportToContentImage"/> に委譲する（座標変換の単一所有）。
    /// </remarks>
    public Point ViewportToContent(Point viewportPos)
        => _zoomView.ViewportToContentImage(viewportPos);

    /// <summary>ドメイン座標（元画像ピクセル）をビューポート座標へ変換する。</summary>
    /// <param name="contentPos">ドメイン座標。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// 画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="ZoomViewControl.ContentToViewportImage"/> に委譲する（座標変換の単一所有）。
    /// </remarks>
    public Point ContentToViewport(Point contentPos)
        => _zoomView.ContentToViewportImage(contentPos);

    // ====================================================================
    // 矩形オーバーレイ作成（破線スタイル・色・幅）
    // ====================================================================

    /// <summary>オーバーレイ矩形の表示要素（Rectangle）を作成する。</summary>
    /// <param name="topLeft">左上（ビューポート座標）。</param>
    /// <param name="bottomRight">右下（ビューポート座標）。</param>
    /// <param name="rect">矩形情報（ドメイン座標・スタイル・色・幅）。</param>
    /// <returns>作成した Panel（背景色の下層 + 前景色の上層を重ねた要素）。</returns>
    /// <remarks>
    /// 破線（<see cref="OverlayLineStyle.Dashed"/> / <see cref="OverlayLineStyle.Dotted"/>）の場合は
    /// 前景色と背景色をダッシュごとに交互に塗る（市松風）。Avalonia の <c>StrokeDashArray</c> は
    /// 単色しか表現できないため、前景色で破線を描き、背景色を下層の別矩形で表現する。<br/>
    /// ダッシュ長は <see cref="OverlayLineDash"/>（コア）を単一所有元とする（ルーペのラスタ描画と共有）。
    /// </remarks>
    private static Panel CreateRectElement(Point topLeft, Point bottomRight, OverlayRect rect)
    {
        var isDashed = OverlayLineDash.IsDashed(rect.Style);
        var (dashLength, gapLength) = OverlayLineDash.Get(rect.Style);
        var dashArray = new AvaloniaList<double>([dashLength, gapLength]);

        var foreground = new SolidColorBrush(rect.Foreground.ToAvaloniaColor());
        var background = new SolidColorBrush(rect.Background.ToAvaloniaColor());

        var recWidth = Math.Max(0, bottomRight.X - topLeft.X);
        var recHeight = Math.Max(0, bottomRight.Y - topLeft.Y);

        // 背景色を下層に、前景色（破線）を上層に重ねる（破線の市松風）。実線は前景色のみ。
        var backgroundElement = new Rectangle
        {
            Width = recWidth,
            Height = recHeight,
            Fill = isDashed ? background : null,
        };

        var foregroundElement = new Rectangle
        {
            Width = recWidth,
            Height = recHeight,
            Stroke = foreground,
            StrokeThickness = rect.Thickness,
            StrokeDashArray = isDashed ? dashArray : null,
        };

        // 2 要素を重ねて返す（背景 → 前景）。Canvas の子として両方を追加するため、親要素としてグルーピングする。
        var group = new Panel();
        group.Children.Add(backgroundElement);
        group.Children.Add(foregroundElement);
        group.Width = recWidth;
        group.Height = recHeight;
        return group;
    }

    // ====================================================================
    // イベント（矩形オーバーレイ変化の通知）
    // ====================================================================

    /// <summary>外部提供矩形の変化を通知する（ルーペへの矩形描画用フック）。</summary>
    public event EventHandler? RectsChanged;

    /// <summary><see cref="RectsChanged"/> イベントを発火する。</summary>
    private void RaiseRectOverlayChanged() => RectsChanged?.Invoke(this, EventArgs.Empty);

    // ====================================================================
    // ルーペへの矩形描画（Rendered イベント経由）
    // ====================================================================

    /// <summary>ルーペの描画完了時に、外部提供矩形を描画済み Bitmap へ描く。</summary>
    /// <param name="sender">イベント送信元（FadeMaskDragRectControl）。</param>
    /// <param name="e">描画済み Bitmap と表示情報。</param>
    /// <remarks>
    /// 外部提供矩形（<see cref="Rects"/>）を <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> で<br/>
    /// ルーペ画像ピクセル座標へ変換し、4 辺を破線（前景色 / 背景色・市松風）で描画する。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (Rects.Count == 0 || e.CaptureTopLeft is not { } topLeft)
        {
            return;
        }

        foreach (var rect in Rects)
        {
            OverlayRectRasterizer.Draw(e.Bitmap, rect, topLeft.ToEsPoint(), e.Scale);
        }
    }
}

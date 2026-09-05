using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Estable.AvaloniaUI.FadeMaskDragRect;
using Estable.AvaloniaUI.Loupe;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.ZoomView;

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
    private readonly FadeMaskDragRectControl _selectionOverlay = new() { ZIndex = 10 };

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

    /// <summary>表示する画像を設定する（ZoomViewControl とルーペへ伝搬する）。</summary>
    /// <remarks>画像変更時は Fit 表示にリセットし、ズーム状態を適用する。</remarks>
    public IImage? Source
    {
        get => _source;
        set
        {
            _source = value;
            _zoomView.Source = value;
            UpdateLoupeSource();
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

    /// <summary>内包するズーム・パンビューポート（SnipCapture の Crop セッションが座標変換・Fit 状態を利用する）。</summary>
    /// <remarks>
    /// <see cref="ZoomViewControl"/> を公開する（<c>StartCropAsync</c> の引数として利用するため）。<br/>
    /// ズーム操作（<see cref="ZoomPercent"/> / <see cref="ZoomIn"/> / <see cref="ZoomOut"/> / <see cref="ZoomFit"/>）は本コントロールの公開 API へ委譲済みで、
    /// 通常のホストは <see cref="ZoomView"/> を直接参照する必要はない。<br/>
    /// SnipCapture の Crop セッション（<see cref="ZoomView"/> の <see cref="ZoomViewControl.IsFit"/> 等）が利用する。
    /// </remarks>
    public ZoomViewControl ZoomView => _zoomView;

    // ====================================================================
    // 公開 API: 選択状態（FadeMaskDragRectControl へ委譲）
    // ====================================================================

    /// <summary>現在の選択状態（選択矩形の有無）。</summary>
    public bool HasSelection => _selectionOverlay.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）。未選択時は 0x0 の空矩形。</summary>
    public Rect SelectionRect => _selectionOverlay.SelectionRect;

    /// <summary>選択状態を外部から設定する（暗転マスクとルーペへ同期）。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。サイズ 0 以下は選択なし。</param>
    public void SetSelection(Rect rect) => _selectionOverlay.SetSelection(rect);

    /// <summary>選択状態をクリアする（全面暗転に戻す）。</summary>
    public void ClearSelection() => _selectionOverlay.ClearSelection();

    /// <summary>座標変換ポートを設定する（内包コントロールへ分配）。</summary>
    /// <param name="port">座標変換ポート。null で等価変換。</param>
    public void SetCoordinateTransform(ICoordinatePort? port) => _selectionOverlay.SetCoordinateTransform(port);

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
    /// 単色しか表現できないため、前景色で破線を描き、背景色を下層の別矩形で表現する。
    /// </remarks>
    private static Panel CreateRectElement(Point topLeft, Point bottomRight, OverlayRect rect)
    {
        var (dashArray, isDashed) = rect.Style switch
        {
            OverlayLineStyle.Solid => (new AvaloniaList<double>(), false),
            OverlayLineStyle.Dashed => (new AvaloniaList<double>([8.0, 4.0]), true),
            OverlayLineStyle.Dotted => (new AvaloniaList<double>([2.0, 2.0]), true),
            _ => (new AvaloniaList<double>(), false),
        };

        var foreground = new SolidColorBrush(rect.Foreground);
        var background = new SolidColorBrush(rect.Background);

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
            DrawRectToLoupe(e.Bitmap, rect, topLeft, e.Scale);
        }
    }

    /// <summary>外部提供矩形の 4 辺をルーペのバッファへ描画する。</summary>
    /// <param name="bitmap">描画対象の UnifiedBitmap。</param>
    /// <param name="rect">描画する矩形（ドメイン座標）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（ドメイン座標系）。</param>
    /// <param name="scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    private static void DrawRectToLoupe(UnifiedBitmap bitmap, OverlayRect rect, Point captureTopLeft, double scale)
    {
        // ドメイン座標 → ルーペピクセル座標（半開区間の終点を含むよう +1 する）
        double x = (rect.Bounds.X - captureTopLeft.X) * scale;
        double y = (rect.Bounds.Y - captureTopLeft.Y) * scale;
        double w = (rect.Bounds.Width + 1) * scale;
        double h = (rect.Bounds.Height + 1) * scale;

        var pixelSize = bitmap.Size.Width;
        if (x + w < 0 || y + h < 0 || x > pixelSize || y > pixelSize)
        {
            return;
        }

        // 4 辺（上・下・左・右）を描画する
        DrawRectEdge(bitmap, new Point(x, y), new Point(x + w, y), rect);
        DrawRectEdge(bitmap, new Point(x, y + h), new Point(x + w, y + h), rect);
        DrawRectEdge(bitmap, new Point(x, y), new Point(x, y + h), rect);
        DrawRectEdge(bitmap, new Point(x + w, y), new Point(x + w, y + h), rect);
    }

    /// <summary>UnifiedBitmap 上に矩形の 1 辺を描画する（実線または破線）。</summary>
    /// <param name="bitmap">描画対象の UnifiedBitmap。</param>
    /// <param name="from">始点（ルーペピクセル座標）。</param>
    /// <param name="to">終点（ルーペピクセル座標）。</param>
    /// <param name="rect">描画スタイル（破線種類・前景色・背景色・幅）。</param>
    private static void DrawRectEdge(UnifiedBitmap bitmap, Point from, Point to, OverlayRect rect)
    {
        bool horizontal = Math.Abs(to.Y - from.Y) < Math.Abs(to.X - from.X);
        double length = horizontal ? to.X - from.X : to.Y - from.Y;
        double start = horizontal ? from.X : from.Y;
        double fixedCoord = horizontal ? from.Y : from.X;
        double sign = length >= 0 ? 1 : -1;
        double distance = Math.Abs(length);

        // 線の太さ
        var thickness = Math.Max(1, (int)Math.Round(rect.Thickness));
        var half = Math.Max(0, thickness / 2);

        var isDashed = rect.Style != OverlayLineStyle.Solid;
        (int dashLen, int gapLen) = rect.Style switch
        {
            OverlayLineStyle.Dashed => (8, 4),
            OverlayLineStyle.Dotted => (2, 2),
            _ => (0, 0),
        };
        int step = dashLen + gapLen;

        int dashIndex = 0;
        for (double pos = 0; pos < distance; pos += isDashed ? step : 1)
        {
            double segEnd = isDashed
                ? start + sign * Math.Min(pos + dashLen, distance)
                : start + sign * (pos + 1);
            double segStartC = start + sign * pos;
            // 破線はダッシュごとに色を交互（前景色 / 背景色・市松風）
            var color = isDashed && dashIndex % 2 == 1 ? rect.Background : rect.Foreground;
            dashIndex++;

            if (horizontal)
            {
                var pxFrom = (int)Math.Round(segStartC);
                var pxTo = (int)Math.Round(segEnd);
                for (var px = pxFrom; px <= pxTo; px++)
                {
                    StampPixel(bitmap, px, (int)Math.Round(fixedCoord), color, half, horizontal);
                }
            }
            else
            {
                var pyFrom = (int)Math.Round(segStartC);
                var pyTo = (int)Math.Round(segEnd);
                for (var py = pyFrom; py <= pyTo; py++)
                {
                    StampPixel(bitmap, (int)Math.Round(fixedCoord), py, color, half, horizontal);
                }
            }
        }
    }

    /// <summary>線の太さを考慮して UnifiedBitmap へ色を打ち込む。</summary>
    /// <param name="bitmap">描画対象の UnifiedBitmap。</param>
    /// <param name="x">中心 X 座標。</param>
    /// <param name="y">中心 Y 座標。</param>
    /// <param name="color">描画色（不透明）。</param>
    /// <param name="half">線の太さの半径（半幅）。</param>
    /// <param name="horizontal">水平辺かどうか。</param>
    private static void StampPixel(UnifiedBitmap bitmap, int x, int y, Color color, int half, bool horizontal)
    {
        // 水平辺は Y 固定で太さ方向が X、垂直辺は X 固定で太さ方向が Y になる。
        // 太さ分だけ固定座標の両側へ広げて描画する（範囲外は UnifiedBitmapExtensions.TrySetPixel が無視）。
        var fixedCoord = horizontal ? y : x;
        for (var d = -half; d <= half; d++)
        {
            var px = horizontal ? x : fixedCoord + d;
            var py = horizontal ? fixedCoord + d : y;
            bitmap.TrySetPixel(px, py, color);
        }
    }
}

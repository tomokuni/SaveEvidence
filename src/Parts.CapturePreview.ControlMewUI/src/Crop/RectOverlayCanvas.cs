using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.ZoomView;
using SkiaSharp;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;

/// <summary>外部提供矩形のオーバーレイを描画するキャンバス（ズームビューの上へ重ねる）。</summary>
/// <remarks>
/// ズームビューが画像を描画し、本キャンバスがその上へ矩形枠のみを描画する（透明なオーバーレイ）。<br/>
/// 矩形のビューポート座標変換はズームビューの座標変換（画像表示矩形の原点基準）を使うため、<br/>
/// Fit の中央寄せ補正を含めて画像と正確に一致する。<br/>
/// ヒットテストは無効化し、下のズームビューが入力を受け取れるようにする。
/// </remarks>
internal sealed class RectOverlayCanvas : SkiaCanvasView
{
    /// <summary>座標変換の基準とするズームビュー。</summary>
    private readonly ZoomViewControl _zoomView;

    /// <summary>描画する矩形（対象座標）。</summary>
    private IReadOnlyList<OverlayRect> _rects = [];

    /// <summary>矩形オーバーレイキャンバスを初期化する。</summary>
    /// <param name="zoomView">座標変換の基準とするズームビュー。</param>
    /// <remarks>静的画像のため連続再描画を無効化し、ヒットテストも無効化する。</remarks>
    public RectOverlayCanvas(ZoomViewControl zoomView)
    {
        _zoomView = zoomView;

        // 静止画のため連続再描画は不要
        ContinuousAnimation = false;

        // オーバーレイは透明なため、入力は下のズームビューへ通す
        IsHitTestVisible = false;

        PaintSurface += OnPaintSurface;
    }

    /// <summary>描画する矩形（対象座標）を設定する。</summary>
    /// <param name="rects">描画する矩形。 空の場合は何も描画しない。</param>
    public void SetRects(IReadOnlyList<OverlayRect> rects)
    {
        _rects = rects;
        InvalidateVisual();
    }

    /// <summary>要求サイズを返す（親の残余を埋める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <c>Size.Empty</c>。</returns>
    /// <remarks>オーバーレイはズームビューと同じ領域を占めるため、自身の要求サイズを持たない。</remarks>
    protected override Size MeasureContent(Size availableSize) => Size.Empty;

    /// <summary>DPI の変更時に再描画する。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>矩形の太さ・ダッシュ長は DIP のため、スケーリングの変更で再描画が必要になる。</remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateVisual();
    }

    /// <summary>矩形オーバーレイを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// ズームビューと同じくキャンバスを DPI スケーリングしてから DIP 座標で描画する。<br/>
    /// 各矩形の左上・右下を対象座標からビューポート座標へ変換し、コア（<see cref="OverlayRectLayout"/>）が<br/>
    /// 確定した描画入力を描画層へ渡す（クランプ・太さ補正・破線の判定は本メソッドが持たない）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);

        if (_rects.Count == 0)
        {
            return;
        }

        // スケーリング係数の正規化（0 以下は基準値）は DpiScaler が単一所有する
        var scaling = DpiScaler.NormalizeScaling(_zoomView.ClientToPixelScaling);

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            foreach (var rect in _rects)
            {
                var topLeft = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.X, rect.Bounds.Y));
                var bottomRight = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.Right, rect.Bounds.Bottom));

                CropSkiaRenderer.Render(canvas, OverlayRectLayout.Resolve(rect, topLeft, bottomRight));
            }
        }
        finally
        {
            canvas.Restore();
        }
    }
}

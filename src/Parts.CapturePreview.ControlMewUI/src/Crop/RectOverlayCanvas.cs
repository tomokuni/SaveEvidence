using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.ZoomView;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;

/// <summary>外部提供矩形のオーバーレイを描画する層（ズームビューの上へ重ねる）。</summary>
/// <remarks>
/// ズームビューが画像を描画し、本層がその上へ矩形枠のみを描画する（透明なオーバーレイ）。<br/>
/// 矩形のビューポート座標変換はズームビューの座標変換（画像表示矩形の原点基準）を使うため、<br/>
/// Fit の中央寄せ補正を含めて画像と正確に一致する。<br/>
/// ヒットテストは無効化し、下のズームビューが入力を受け取れるようにする。
/// </remarks>
internal sealed class RectOverlayCanvas : Control
{
    /// <summary>座標変換の基準とするズームビュー。</summary>
    private readonly ZoomViewControl _zoomView;

    /// <summary>描画する矩形（対象座標）。</summary>
    private IReadOnlyList<OverlayRect> _rects = [];

    /// <summary>矩形オーバーレイの層を初期化する。</summary>
    /// <param name="zoomView">座標変換の基準とするズームビュー。</param>
    /// <remarks>オーバーレイは透明なため、入力は下のズームビューへ通す。</remarks>
    public RectOverlayCanvas(ZoomViewControl zoomView)
    {
        _zoomView = zoomView;
        IsHitTestVisible = false;
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
    /// <param name="context">描画先のコンテキスト（物理ピクセル）。</param>
    /// <remarks>
    /// 各矩形の左上・右下を対象座標からビューポート座標へ変換し、コア（<see cref="OverlayRectLayout"/>）が<br/>
    /// 確定した描画入力を描画層へ渡す（クランプ・太さ補正・破線の判定は本メソッドが持たない）。
    /// </remarks>
    protected override void OnRender(IGraphicsContext context)
    {
        if (_rects.Count == 0)
        {
            return;
        }

        // スケーリング係数の正規化（0 以下は基準値）はコアが単一所有する
        var scaling = DpiScaler.NormalizeScaling(context.DpiScale);

        context.Save();
        context.Scale(scaling, scaling);
        try
        {
            foreach (var rect in _rects)
            {
                var topLeft = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.X, rect.Bounds.Y));
                var bottomRight = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.Right, rect.Bounds.Bottom));

                CropOverlayRenderer.Render(context, OverlayRectLayout.Resolve(rect, topLeft, bottomRight));
            }
        }
        finally
        {
            context.Restore();
        }
    }
}

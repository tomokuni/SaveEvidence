using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop;

/// <summary>外部提供矩形のオーバーレイを表示する層（ズームビューの上へ重ねる）。</summary>
/// <remarks>
/// ズームビューが画像を表示し、本層がその上へ矩形枠のみを置く（背景を持たない透明な層）。<br/>
/// 矩形のビューポート座標変換はズームビューの座標変換（画像表示矩形の原点基準）を使うため、<br/>
/// Fit の中央寄せ補正を含めて画像と正確に一致する。<br/>
/// ヒットテストは無効化し、下のズームビューが入力を受け取れるようにする。<br/>
/// <b>XAML の図形だけを使う</b>: 描画内容は <see cref="DrawList"/> を組み立てて <see cref="RectShapeLayer"/> へ渡す<br/>
/// （移行前の Skia のキャンバスは使わない）。
/// </remarks>
internal sealed partial class RectOverlayCanvas : Grid
{
    /// <summary>座標変換の基準とするズームビュー。</summary>
    private readonly ZoomViewControl _zoomView;

    /// <summary>矩形を表す図形の層。</summary>
    private readonly RectShapeLayer _shapes = new() { IsHitTestVisible = false };

    /// <summary>描画内容（更新のたびに詰め直して使い回す）。</summary>
    private readonly DrawList _drawList = new();

    /// <summary>描画する矩形（対象座標）。</summary>
    private IReadOnlyList<OverlayRect> _rects = [];

    /// <summary>矩形オーバーレイの層を初期化する。</summary>
    /// <param name="zoomView">座標変換の基準とするズームビュー。</param>
    /// <remarks>矩形は静止画のため、描画は矩形の設定時とビューポートの変更時だけ更新する。 ヒットテストも無効化する。</remarks>
    public RectOverlayCanvas(ZoomViewControl zoomView)
    {
        _zoomView = zoomView;

        // オーバーレイは透明なため、入力は下のズームビューへ通す
        IsHitTestVisible = false;

        Children.Add(_shapes);
    }

    /// <summary>描画する矩形（対象座標）を設定する。</summary>
    /// <param name="rects">描画する矩形。 空の場合は何も表示しない。</param>
    /// <exception cref="ArgumentNullException"><paramref name="rects"/> が null の場合。</exception>
    public void SetRects(IReadOnlyList<OverlayRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);

        _rects = rects;
        Refresh();
    }

    /// <summary>表示を最新の状態へ更新する（ビューポートが変わったときにホストが呼ぶ）。</summary>
    /// <remarks>座標変換はズームビューの現在の表示矩形を使うため、パン・ズームの後に呼ぶ。</remarks>
    public void Refresh()
    {
        _drawList.Clear();

        foreach (var rect in _rects)
        {
            var topLeft = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.X, rect.Bounds.Y));
            var bottomRight = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.Right, rect.Bounds.Bottom));

            OverlayRectShapes.Add(_drawList, OverlayRectLayout.Resolve(rect, topLeft, bottomRight));
        }

        _shapes.Apply(_drawList);
    }
}

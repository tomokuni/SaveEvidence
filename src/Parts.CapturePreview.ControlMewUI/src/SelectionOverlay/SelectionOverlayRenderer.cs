using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.Ui.MewUI.Interop;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.SelectionOverlay;

/// <summary>選択矩形の破線オーバーレイを MewUI の描画コンテキストへ描画するレンダラー。</summary>
/// <remarks>
/// 破線の幾何（位置・長さ・色の種別）はコアの <c>SelectionOutlineRasterizer.EnumerateDashes</c> が単一所有するため、<br/>
/// 本クラスは列挙された 1 ピクセル厚の矩形を色ごとに塗るだけである（Avalonia 版と同じ破線になる）。<br/>
/// 塗りは矩形の列として写すため、線種を持たない描画 API でも同じ見た目になる。
/// </remarks>
internal static class SelectionOverlayRenderer
{
    /// <summary>選択矩形の 4 辺を 2 色の破線でコンテキストへ描画する。</summary>
    /// <param name="context">描画先のコンテキスト（ピクセル等倍で描画できる状態であること）。</param>
    /// <param name="selectionSourceRect">選択矩形（対象座標系・半開区間）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="pixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。</param>
    /// <param name="colorA">破線の色 A（描画開始色）。</param>
    /// <param name="colorB">破線の色 B（色 A と交互に使用する）。</param>
    public static void DrawSelectionRect(
        IGraphicsContext context,
        EsRect selectionSourceRect,
        EsPoint captureTopLeft,
        double scale,
        int pixelSize,
        DrawingColor colorA,
        DrawingColor colorB)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mewA = colorA.ToMewUIColor();
        var mewB = colorB.ToMewUIColor();

        foreach (var dash in SelectionOutlineRasterizer.EnumerateDashes(selectionSourceRect, captureTopLeft, scale, pixelSize))
        {
            context.FillRectangle(dash.Bounds.ToMewUIRect(), dash.IsColorA ? mewA : mewB);
        }
    }
}

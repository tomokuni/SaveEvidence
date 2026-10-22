using Avalonia.Media;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using EsUtil.Ui.AvaloniaUI.Interop;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.SelectionOverlay;

/// <summary>選択矩形の破線オーバーレイを Avalonia の描画コンテキストへ描画するレンダラー。</summary>
/// <remarks>
/// 破線の幾何（位置・長さ・色の種別）はコアの <c>SelectionOutlineRasterizer.EnumerateDashes</c> が単一所有するため、<br/>
/// 本クラスは列挙された 1 ピクセル厚の矩形を色ごとに塗るだけである（MewUI 版と同じ破線になる）。<br/>
/// 塗りは 2 色ぶんのブラシ（<see cref="SolidColorBrushCache"/> が共有する）で行う。
/// </remarks>
internal static class SelectionOutlineContextRenderer
{
    /// <summary>選択矩形の 4 辺を 2 色の破線で描画する。</summary>
    /// <param name="context">描画先の描画コンテキスト（描画中のみ有効）。 null 不可。</param>
    /// <param name="selectionSourceRect">選択矩形（対象座標系・半開区間）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="pixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。</param>
    /// <param name="colorA">破線の色 A（描画開始色）。</param>
    /// <param name="colorB">破線の色 B（色 A と交互に使用する）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    public static void DrawSelectionRect(
        DrawingContext context,
        EsRect selectionSourceRect,
        EsPoint captureTopLeft,
        double scale,
        int pixelSize,
        DrawingColor colorA,
        DrawingColor colorB)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 破線は 2 色を交互に使うため、ブラシは 2 つだけ取得して使い回す
        var brushA = SolidColorBrushCache.GetOrCreate(colorA);
        var brushB = SolidColorBrushCache.GetOrCreate(colorB);

        foreach (var dash in SelectionOutlineRasterizer.EnumerateDashes(selectionSourceRect, captureTopLeft, scale, pixelSize))
        {
            context.FillRectangle(dash.IsColorA ? brushA : brushB, dash.Bounds.ToAvaloniaRect());
        }
    }
}

using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.Ui.WinForms.Interop.Imaging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.SelectionOverlay;

/// <summary>選択矩形の破線オーバーレイを GDI+ の描画面へ描画するレンダラー。</summary>
/// <remarks>
/// 破線の幾何（位置・長さ・色の種別）はコアの <see cref="SelectionOutlineRasterizer.EnumerateDashes"/> が単一所有するため、<br/>
/// 本クラスは列挙された 1 ピクセル厚の矩形を色ごとに塗るだけである（他の UI 版と同じ破線になる）。<br/>
/// 描画面の座標系は<b>ルーペ画像の物理ピクセル</b>である（呼び出し側がその座標系で渡す）。
/// </remarks>
internal static class SelectionOverlayGraphicsRenderer
{
    /// <summary>選択矩形の 4 辺を 2 色の破線で描画面へ描画する。</summary>
    /// <param name="graphics">描画先の描画面（ピクセル等倍で描画できる状態であること）。 null 不可。</param>
    /// <param name="selectionSourceRect">選択矩形（対象座標系・半開区間）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="pixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。</param>
    /// <param name="colorA">破線の色 A（描画開始色）。</param>
    /// <param name="colorB">破線の色 B（色 A と交互に使用する）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    /// <remarks>
    /// 破線は 2 色を交互に使うため、塗りのブラシを 1 つ持ち色だけを書き換える（<see cref="GdiFill"/> が単一所有する）。<br/>
    /// 破線の一部は画像の外側へ出る（選択が切り出し領域の端に接する場合）が、GDI+ は描画先の範囲で切り取るため<br/>
    /// 例外にはならない（切り出し領域の判定はコアが済ませている）。
    /// </remarks>
    public static void DrawSelectionRect(
        Graphics graphics,
        EsRect selectionSourceRect,
        EsPoint captureTopLeft,
        double scale,
        int pixelSize,
        Color colorA,
        Color colorB)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        foreach (var dash in SelectionOutlineRasterizer.EnumerateDashes(
            selectionSourceRect, captureTopLeft, scale, pixelSize))
        {
            GdiFill.Rect(graphics, dash.Bounds, dash.IsColorA ? colorA : colorB);
        }
    }
}

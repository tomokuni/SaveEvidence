using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.FadeMaskDragRect;


namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.FadeMaskDragRect;

/// <summary>選択矩形の破線オーバーレイを Skia のキャンバスへ描画するレンダラー。</summary>
/// <remarks>
/// 破線の幾何（位置・長さ・色の種別）はコアの <c>FadeMaskDragRectOverlay.EnumerateDashes</c> が単一所有するため、<br/>
/// 本クラスは列挙された 1 ピクセル厚の矩形を色ごとに塗るだけである（Avalonia 版と同じ破線になる）。<br/>
/// <see cref="SKCanvas"/> へ直接描画するため、ウィンドウを必要とせず画素を実測できる。
/// </remarks>
internal static class FadeMaskDragRectSkiaRenderer
{
    /// <summary>選択矩形の 4 辺を 2 色の破線でキャンバスへ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（ピクセル等倍で描画できる状態であること）。</param>
    /// <param name="selectionSourceRect">選択矩形（対象座標系・半開区間）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="pixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。</param>
    /// <param name="colorA">破線の色 A（描画開始色）。</param>
    /// <param name="colorB">破線の色 B（色 A と交互に使用する）。</param>
    public static void DrawSelectionRect(
        SKCanvas canvas,
        EsRect selectionSourceRect,
        EsPoint captureTopLeft,
        double scale,
        int pixelSize,
        DrawingColor colorA,
        DrawingColor colorB)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };

        foreach (var dash in FadeMaskDragRectOverlay.EnumerateDashes(selectionSourceRect, captureTopLeft, scale, pixelSize))
        {
            paint.Color = (dash.IsColorA ? colorA : colorB).ToSKColor();
            canvas.DrawRect(new SKRect(dash.X, dash.Y, dash.X + dash.Width, dash.Y + dash.Height), paint);
        }
    }

    // ※ 色の変換（コアの色型 → Skia）は EsUtil.Platform.Core の SkiaColorInterop（拡張メソッド）を共用する
}

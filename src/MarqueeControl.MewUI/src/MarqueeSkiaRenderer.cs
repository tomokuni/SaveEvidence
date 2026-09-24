using Estable.Marquee;
using Estable.Marquee.Selection;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Marquee;

/// <summary>ドラッグ矩形の描画（ドラッグハンドル・始点マーカー）を Skia で行うレンダラー。</summary>
/// <remarks>
/// UI 非依存コア（<see cref="SelectionGeometry"/>）の計算結果を <see cref="SKCanvas"/> へ描画するだけの層。<br/>
/// Avalonia 版のドロワー（<c>MarqueeDrawer</c>）と等価な見た目を、中間ビットマップなしの直接描画で実現する。<br/>
/// ハンドル位置は <see cref="SelectionGeometry.GetHandlePosition"/> から取得する（コアが単一所有）。<br/>
/// 選択矩形の外側に描く装飾（暗転マスク・穴あけ枠）は <see cref="MarqueeOuterSkiaRenderer"/> が担う。<br/>
/// <b>文字（ラベル・座標ラベル）は本クラスが描かない</b>: テキストの実寸測定が必要なため、描画完了時にホストへ通知し<br/>
/// <see cref="MarqueeLabelRenderer"/> が描く（文字の描画をホストの責務とする）。<br/>
/// 座標はすべて DIP で扱い、呼び出し側が DPI スケーリングを適用済みであることを前提とする。
/// </remarks>
internal static class MarqueeSkiaRenderer
{
    /// <summary>ドラッグハンドル（塗り矩形 + 枠線）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="rect">選択矩形（DIP）。</param>
    /// <param name="handleSize">ハンドルの一辺サイズ（DIP）。</param>
    /// <param name="handleColor">ハンドルの塗り色（アルファを含む）。</param>
    /// <param name="borderColor">ハンドル枠の色（アルファを含む）。</param>
    /// <remarks>
    /// 8 方向のハンドル（None / Move を除く）を列挙し、<see cref="SelectionGeometry.GetHandlePosition"/> から<br/>
    /// 中心位置を取得して描画する。ハンドルの追加時は <see cref="HitTarget"/> の追加のみで描画が追従する。<br/>
    /// <b>ペイントは 2 つ（塗り・枠）を 8 個のハンドルで共有する</b>（ハンドルの矩形は列挙時に決まるため、<br/>
    /// 矩形の一覧を作って <see cref="SkiaFill"/> へ渡すと、ポインター移動毎の一覧生成が増える）。
    /// </remarks>
    public static void DrawHandles(SKCanvas canvas, EsRect rect, double handleSize, DrawingColor handleColor, DrawingColor borderColor)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var fillPaint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = handleColor.ToSKColor(),
        };

        using var borderPaint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)MarqueeAppearance.HandlePenThickness,
            Color = borderColor.ToSKColor(),
        };

        var half = handleSize / 2;

        foreach (var handle in SelectionGeometry.DirectionalHandles)
        {
            var center = SelectionGeometry.GetHandlePosition(rect, handle);
            var bounds = new SKRect(
                (float)(center.X - half),
                (float)(center.Y - half),
                (float)(center.X + half),
                (float)(center.Y + half));

            canvas.DrawRect(bounds, fillPaint);
            canvas.DrawRect(bounds, borderPaint);
        }
    }

    /// <summary>始点マーカー（十字線）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="center">十字線の中心位置（DIP）。</param>
    /// <param name="color">十字線の色（アルファを含む）。</param>
    /// <param name="thickness">十字線の太さ（DIP）。</param>
    /// <param name="size">十字線の半幅（DIP）。</param>
    public static void DrawStartMarker(SKCanvas canvas, EsPoint center, DrawingColor color, double thickness, double size)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)thickness,
            Color = color.ToSKColor(),
        };

        var cx = (float)center.X;
        var cy = (float)center.Y;
        var half = (float)size;

        canvas.DrawLine(cx - half, cy, cx + half, cy, paint);
        canvas.DrawLine(cx, cy - half, cx, cy + half, paint);
    }
}

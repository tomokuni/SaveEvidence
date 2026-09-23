using Estable.Marquee;
using Estable.Marquee.Selection;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Marquee;

/// <summary>ドラッグ矩形の描画（ドラッグハンドル・始点マーカー・カーソル座標ラベル）を Skia で行うレンダラー。</summary>
/// <remarks>
/// UI 非依存コア（<see cref="SelectionGeometry"/>）の計算結果を <see cref="SKCanvas"/> へ描画するだけの層。<br/>
/// Avalonia 版のドロワー（<c>MarqueeDrawer</c>）と等価な見た目を、中間ビットマップなしの直接描画で実現する。<br/>
/// ハンドル位置は <see cref="SelectionGeometry.GetHandlePosition"/> から取得する（コアが単一所有）。<br/>
/// 選択矩形の外側に描く装飾（暗転マスク・穴あけ枠・ラベル）は <see cref="MarqueeOuterSkiaRenderer"/> が担う。<br/>
/// 座標はすべて DIP で扱い、呼び出し側が DPI スケーリングを適用済みであることを前提とする。
/// </remarks>
internal static class MarqueeSkiaRenderer
{
    /// <summary>座標ラベル背景の上下パディング（DIP）。</summary>
    /// <remarks>フォントの描画基準（ベースライン）が UI ごとに異なるため、縦方向の余白は本 UI が持つ。</remarks>
    private const double LabelVerticalPadding = 2.0;

    /// <summary>ドラッグハンドル（塗り矩形 + 枠線）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="rect">選択矩形（DIP）。</param>
    /// <param name="handleSize">ハンドルの一辺サイズ（DIP）。</param>
    /// <param name="handleColor">ハンドルの塗り色（アルファを含む）。</param>
    /// <param name="borderColor">ハンドル枠の色（アルファを含む）。</param>
    /// <remarks>
    /// 8 方向のハンドル（None / Move を除く）を列挙し、<see cref="SelectionGeometry.GetHandlePosition"/> から<br/>
    /// 中心位置を取得して描画する。ハンドルの追加時は <see cref="HitTarget"/> の追加のみで描画が追従する。
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

    /// <summary>カーソルの右側にカーソルのドメイン座標ラベル（"(x, y)"）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="cursorDip">カーソルのローカル DIP 座標。</param>
    /// <param name="domainPos">カーソルのドメイン座標（表示する値）。</param>
    /// <param name="clientSize">描画領域のサイズ（はみ出しクランプ用・DIP）。</param>
    /// <param name="textColor">ラベルの文字色（アルファを含む）。</param>
    /// <param name="backgroundColor">ラベルの背景色（アルファを含む）。</param>
    /// <param name="fontSize">ラベルのフォントサイズ（DIP）。</param>
    /// <remarks>
    /// 1. 座標は <see cref="CoordinateFormatter"/>（コア）で "(x, y)" 形式へ文字列化する（Avalonia 版と同一の表記）。<br/>
    /// 2. ラベル配置（カーソル右側 + はみ出しクランプ）はコアの <see cref="MarqueeAppearance.ComputeCoordLabelPosition"/> へ委譲する。<br/>
    /// 3. 背景を塗ってから文字を描画する。
    /// </remarks>
    public static void DrawCoordLabel(
        SKCanvas canvas,
        EsPoint cursorDip,
        EsPoint domainPos,
        EsSize clientSize,
        DrawingColor textColor,
        DrawingColor backgroundColor,
        double fontSize)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var text = CoordinateFormatter.Format(domainPos);

        using var font = new SKFont(SKTypeface.Default, (float)fontSize);
        using var textPaint = new SKPaint { IsAntialias = true, Color = textColor.ToSKColor() };
        using var backgroundPaint = new SKPaint { IsAntialias = false, Color = backgroundColor.ToSKColor() };

        var width = font.MeasureText(text, textPaint);
        var height = (float)fontSize;
        var labelWidth = width + (float)(MarqueeAppearance.LabelPadding * 2);
        var labelHeight = height + (float)(LabelVerticalPadding * 2);

        var position = MarqueeAppearance.ComputeCoordLabelPosition(cursorDip, clientSize, labelWidth, labelHeight);
        var left = (float)position.X;
        var top = (float)position.Y;

        canvas.DrawRect(new SKRect(left, top, left + labelWidth, top + labelHeight), backgroundPaint);
        canvas.DrawText(text, left + (float)MarqueeAppearance.LabelPadding, top + (float)LabelVerticalPadding + height, SKTextAlign.Left, font, textPaint);
    }

    /// <summary>コア幾何型の矩形を Skia の矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（DIP）。</param>
    /// <returns>Skia の矩形。</returns>
    private static SKRect ToSKRect(EsRect rect) => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

    /// <summary>コアの色型を Skia の色へ変換する。</summary>
    /// <param name="color">変換元の色（アルファを含む）。</param>
    /// <returns>Skia の色。</returns>
    // ※ 色の変換（コアの色型 → Skia）は Platform.Core の SkiaColorInterop（拡張メソッド）を共用する
}

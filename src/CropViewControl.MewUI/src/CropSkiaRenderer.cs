using Estable.CropView;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.CropView;

/// <summary>オーバーレイ矩形（<see cref="OverlayRect"/>）を Skia で描画するレンダラー。</summary>
/// <remarks>
/// UI 非依存コアの矩形情報（<see cref="OverlayRect"/>）とダッシュ長（<see cref="OverlayLineDash"/>）を受け取り、<br/>
/// ビューポート座標の矩形を <see cref="SKCanvas"/> へ描画するだけの層。<br/>
/// 実線は前景色の枠のみ、破線は「背景色の実線 → 前景色の破線」の 2 回描画で市松風の視認性を出す<br/>
/// （Avalonia 版が背景色の下層矩形と前景色の破線矩形を重ねる方式と等価）。<br/>
/// 座標変換（ドメイン座標 → ビューポート座標）は呼び出し側が済ませている前提とする。
/// </remarks>
internal static class CropSkiaRenderer
{
    /// <summary>ビューポート座標の矩形を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="rect">描画する矩形（ビューポート座標・DIP）。</param>
    /// <param name="info">矩形のスタイル（線種・色・太さ）。</param>
    /// <remarks>
    /// 幅・高さが 0 以下の場合は何も描画しない（早期リターン）。<br/>
    /// 破線のダッシュ長は <see cref="OverlayLineDash"/>（コア）を単一所有元とする（ルーペのラスタ描画と共有）。
    /// </remarks>
    public static void RenderRect(SKCanvas canvas, EsRect rect, OverlayRect info)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var strokeWidth = (float)Math.Max(1, info.Thickness);
        var skRect = new SKRect((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

        // 枠線は矩形の内側に描く（Avalonia の Rectangle.Stroke と同じ見た目にする）
        var half = strokeWidth / 2f;
        var inset = new SKRect(skRect.Left + half, skRect.Top + half, skRect.Right - half, skRect.Bottom - half);

        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeJoin = SKStrokeJoin.Miter,
        };

        if (OverlayLineDash.IsDashed(info.Style))
        {
            // 背景色の実線を下層に描く（破線の隙間から見える市松風の下地）
            paint.Color = ToSKColor(info.Background);
            canvas.DrawRect(inset, paint);
        }

        var (dashLength, gapLength) = OverlayLineDash.Get(info.Style);

        paint.Color = ToSKColor(info.Foreground);

        if (OverlayLineDash.IsDashed(info.Style))
        {
            // 前景色をダッシュで描く（間隔はコアの単一所有値を使う）
            using var dashEffect = SKPathEffect.CreateDash([(float)dashLength, (float)gapLength], 0f);
            paint.PathEffect = dashEffect;
        }

        canvas.DrawRect(inset, paint);
    }

    /// <summary>コアの色を Skia の色へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>Skia の色。</returns>
    private static SKColor ToSKColor(DrawingColor color) => new(color.R, color.G, color.B, color.A);
}

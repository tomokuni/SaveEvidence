using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;


namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;

/// <summary>オーバーレイ矩形の描画入力（<see cref="OverlayRectFrame"/>）を Skia で描画するレンダラー。</summary>
/// <remarks>
/// コアが確定した描画入力（配置・太さ・破線の判定と長さ・色）を <see cref="SKCanvas"/> へ写すだけの層であり、<br/>
/// 式・判断・クランプを持たない（描画手順として「枠を内側へ寄せる」「背景→前景の順に描く」だけを持つ）。<br/>
/// 実線は前景色の枠のみ、破線は「背景色の実線 → 前景色の破線」の 2 回描画で市松風の視認性を出す<br/>
/// （Avalonia 版が背景色の下層矩形と前景色の破線矩形を重ねる方式と等価）。<br/>
/// 座標変換（対象座標 → ビューポート座標）と描画入力の組み立ては呼び出し側が済ませている前提とする。
/// </remarks>
internal static class CropSkiaRenderer
{
    /// <summary>描画入力を Skia のキャンバスへ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="frame">描画入力（コアが確定した配置・太さ・破線・色）。</param>
    /// <remarks>
    /// 描画できる大きさでない場合（幅または高さが 0 以下）は何も描画しない（早期リターン）。<br/>
    /// 破線の判定とダッシュ長はコア（<see cref="OverlayRectFrame"/>）を単一所有元とする（ルーペのラスタ描画と共有）。
    /// </remarks>
    public static void Render(SKCanvas canvas, OverlayRectFrame frame)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (!frame.HasDrawableSize)
        {
            return;
        }

        var bounds = frame.Bounds;
        var strokeWidth = (float)frame.Thickness;
        var skRect = bounds.ToSKRect();

        // 枠線は矩形の内側に描く（Avalonia の Rectangle.Stroke と同じ見た目にする）
        var half = (float)frame.HalfThickness;
        var inset = new SKRect(skRect.Left + half, skRect.Top + half, skRect.Right - half, skRect.Bottom - half);

        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeJoin = SKStrokeJoin.Miter,
        };

        if (frame.DrawsBackground)
        {
            // 背景色の実線を下層に描く（破線の隙間から見える市松風の下地）
            paint.Color = frame.Background.ToSKColor();
            canvas.DrawRect(inset, paint);
        }

        paint.Color = frame.Foreground.ToSKColor();

        if (frame.IsDashed)
        {
            // 前景色をダッシュで描く（間隔はコアの単一所有値を使う）
            using var dashEffect = SKPathEffect.CreateDash([(float)frame.DashLength, (float)frame.GapLength], 0f);
            paint.PathEffect = dashEffect;
        }

        canvas.DrawRect(inset, paint);
    }
}

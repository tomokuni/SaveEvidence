using SkiaSharp;

namespace Estable.CaptureSession;

/// <summary>画像を描画先の領域へ配置する矩形計算（拡大・縮小の倍率は 1 箇所で決める）。</summary>
/// <remarks>
/// <b>Uniform</b>: 縦横比を保ち、はみ出さない倍率（幅・高さの小さい方）で中央へ配置する（余白は描画しない）。<br/>
/// <b>Fill</b>: 縦横比を無視して領域全体へ引き伸ばす。<br/>
/// <b>空の入力</b>: 元画像・描画先のいずれかが 0 以下なら空の矩形を返す（描画しない）。<br/>
/// <b>純粋関数</b>: UI ホストを必要としないため、倍率・中央寄せ・空の扱いをヘッドレスで検証できる。
/// </remarks>
public static class ImageDrawMath
{
    /// <summary>元画像を描画先の領域へ配置する矩形を計算する。</summary>
    /// <param name="sourceWidth">元画像の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元画像の高さ（ピクセル）。</param>
    /// <param name="targetWidth">描画先の幅。</param>
    /// <param name="targetHeight">描画先の高さ。</param>
    /// <param name="uniform">true の場合は縦横比を保って中央へ配置する。false の場合は領域全体へ引き伸ばす。</param>
    /// <returns>描画先の矩形。元画像または描画先が空の場合は <see cref="SKRect.Empty"/>。</returns>
    public static SKRect ComputeDestination(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool uniform)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            return SKRect.Empty;
        }

        if (!uniform)
        {
            return new SKRect(0, 0, targetWidth, targetHeight);
        }

        // 縦横比を保つため、幅・高さそれぞれの倍率の小さい方（Uniform）を採用する
        var scale = Math.Min((double)targetWidth / sourceWidth, (double)targetHeight / sourceHeight);
        var width = (float)(sourceWidth * scale);
        var height = (float)(sourceHeight * scale);

        return new SKRect(
            (targetWidth - width) / 2,
            (targetHeight - height) / 2,
            (targetWidth + width) / 2,
            (targetHeight + height) / 2);
    }
}

using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

/// <summary>画像の色を要約する純粋関数（平均色の算出）。</summary>
/// <remarks>
/// 撮影オーバーレイの下地色（撮影した画像に合わせた背景）を決めるために使う。<br/>
/// 全画素を走査すると時間がかかるため標本（間引き）する規則と、空画像のフォールバックを本クラスが単一所有する<br/>
/// （UI ごとに間引き幅・既定色がずれると、同じ画像でも下地色が変わる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class ImageColorMath
{
    /// <summary>平均色の標本を間引く基準（この値で長辺を割った間隔でサンプルする）。</summary>
    public const int SampleDivisor = 40;

    /// <summary>下地色の上に暗転色を重ねた色を求める（アルファ合成）。</summary>
    /// <param name="backdrop">下地の色（不透明として扱う）。</param>
    /// <param name="overlay">重ねる色（アルファが不透明度）。</param>
    /// <returns>合成した色（不透明）。 不透明度 0 なら <paramref name="backdrop"/> と同じ色。</returns>
    /// <remarks>
    /// 撮影オーバーレイの下地（表示直後、まだ画像を描いていない瞬間の背景）を「暗転後の見た目」に合わせるために使う。<br/>
    /// 下地を画像の平均色のままにすると、画像を描く前の数フレームが明るい画面になり「一瞬白くなる」ため、<br/>
    /// あらかじめ暗転色を合成した色を下地にする（合成の式は本メソッドが単一所有する）。
    /// </remarks>
    public static DrawingColor Composite(DrawingColor backdrop, DrawingColor overlay)
    {
        var alpha = overlay.A / 255.0;
        byte Mix(byte back, byte front)
            => (byte)Math.Round((front * alpha) + (back * (1.0 - alpha)), MidpointRounding.AwayFromZero);

        return DrawingColor.FromArgb(
            255,
            Mix(backdrop.R, overlay.R),
            Mix(backdrop.G, overlay.G),
            Mix(backdrop.B, overlay.B));
    }

    /// <summary>画像の平均色を求める（標本間引き）。</summary>
    /// <param name="image">対象の画像。</param>
    /// <returns>平均色（不透明）。 画素数が 0 の場合は黒。</returns>
    /// <remarks>
    /// 長辺を <see cref="SampleDivisor"/> で割った間隔（最低 1 ピクセル）でサンプルする。<br/>
    /// アルファは平均せず不透明として返す（下地色として使うため）。
    /// </remarks>
    public static DrawingColor AverageColor(EsReadOnlyBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var step = Math.Max(1, Math.Min(image.Width, image.Height) / SampleDivisor);
        long red = 0, green = 0, blue = 0;
        var count = 0;

        for (var y = 0; y < image.Height; y += step)
        {
            for (var x = 0; x < image.Width; x += step)
            {
                var (b, g, r, _) = image.GetPixel(x, y);
                red += r;
                green += g;
                blue += b;
                count++;
            }
        }

        return count == 0
            ? DrawingColor.FromArgb(255, 0, 0, 0)
            : DrawingColor.FromArgb(255, (byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }
}

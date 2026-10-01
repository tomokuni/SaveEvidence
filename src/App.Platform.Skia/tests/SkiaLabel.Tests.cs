using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Platform.Skia.Tests;

/// <summary><see cref="SkiaLabel"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「背景 → 文字」の描画順は、両 UI のラベル描画と各ホストが共有する規則であるため、<br/>
/// 文字が背景の上に乗ること（順序が入れ替わると全画素が背景色になること）を画素で検証する。
/// </remarks>
public class SkiaLabelTests
{
    /// <summary>検証に使うラベル背景（黒・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 0, 0, 0);

    /// <summary>テスト用の描画先（透明で初期化したビットマップとキャンバス）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。 破棄は呼び出し側。</returns>
    private static (SKBitmap Bitmap, SKCanvas Canvas) CreateTarget(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        return (bitmap, new SKCanvas(bitmap));
    }

    /// <summary>指定領域で指定色の画素が何個あるかを数える。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="color">数える色（アルファを含めて比較する）。</param>
    /// <param name="x0">左上 X 座標。</param>
    /// <param name="y0">左上 Y 座標。</param>
    /// <param name="x1">右下 X 座標（含まない）。</param>
    /// <param name="y1">右下 Y 座標（含まない）。</param>
    /// <returns>一致する画素数。</returns>
    private static int CountColor(SKBitmap bitmap, SKColor color, int x0, int y0, int x1, int y1)
    {
        var count = 0;

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (bitmap.GetPixel(x, y) == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>文字が背景の上に描かれる（順序が入れ替わると背景色のみになる）ことを検証する。</summary>
    [Fact]
    public void Draw_文字を背景の上に描く()
    {
        var (bitmap, canvas) = CreateTarget(80, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaLabel.Draw(
                canvas, new EsRect(10, 10, 60, 24), s_background,
                "ABC", new EsPoint(14, 14), fontSize: 14, DrawingColor.White);

            // 文字（白系）と背景（黒）が同時に存在する = 背景の後に文字を描いている
            Assert.True(CountColor(bitmap, new SKColor(255, 255, 255, 255), 12, 12, 68, 34) > 0, "文字が描画されていない");
            Assert.True(CountColor(bitmap, new SKColor(0, 0, 0, 255), 12, 12, 68, 34) > 0, "背景が描画されていない");
            // 背景の外側は透明のまま
            Assert.Equal(0, bitmap.GetPixel(5, 5).Alpha);
        }
    }

    /// <summary>角丸半径 0 は四角形として背景を塗ることを検証する。</summary>
    [Fact]
    public void Draw_角丸なしは四角形として塗る()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaLabel.Draw(
                canvas, new EsRect(10, 10, 20, 20), s_background,
                "A", new EsPoint(14, 14), fontSize: 12, DrawingColor.White);

            // 四隅まで塗られる
            Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(10, 10));
            Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(29, 29));
        }
    }

    /// <summary>角丸半径を指定すると四隅が塗られない（角が丸い）ことを検証する。</summary>
    [Fact]
    public void Draw_角丸ありは四隅を塗らない()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaLabel.Draw(
                canvas, new EsRect(10, 10, 20, 20), s_background,
                "A", new EsPoint(14, 14), fontSize: 12, DrawingColor.White, cornerRadius: 6);

            // 四隅は丸められて塗られない（辺の中央は塗られる）
            Assert.Equal(0, bitmap.GetPixel(10, 10).Alpha);
            Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(20, 10));
        }
    }

    /// <summary>半透明の背景色がそのまま塗られることを検証する。</summary>
    [Fact]
    public void Draw_半透明の背景色をそのまま塗る()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaLabel.Draw(
                canvas, new EsRect(0, 0, 40, 40), DrawingColor.FromArgb(128, 0, 0, 255),
                "A", new EsPoint(4, 4), fontSize: 12, DrawingColor.White);

            // 背景（青 50%）が乗り、文字（白系）が上に描かれる
            Assert.Equal(new SKColor(0, 0, 255, 128), bitmap.GetPixel(1, 1));
            Assert.True(CountColor(bitmap, new SKColor(255, 255, 255, 255), 0, 0, 40, 40) > 0);
        }
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Draw_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => SkiaLabel.Draw(
            null!, new EsRect(0, 0, 10, 10), s_background, "A", new EsPoint(0, 0), fontSize: 12, DrawingColor.White));
    }
}

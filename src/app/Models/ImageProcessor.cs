namespace app.Models;

/// <summary>
/// 画像の切り出し処理と、背景色を自動判別したウィンドウ領域検出を提供する静的ユーティリティクラス。
/// </summary>
/// <remarks>
/// ウィンドウ領域検出は、画像四隅の背景色を基準に、同じ色が連続する領域を除去する方式を採用する。<br/>
/// 色の類似判定は RGB 各成分の差分合計が 30 未満を「類似」とみなす。<br/>
/// 全メソッドが静的であり、インスタンス化は不要。<br/>
/// </remarks>
public static class ImageProcessor
{
    /// <summary>
    /// 画像から指定された矩形領域を切り出す。
    /// </summary>
    /// <param name="source">元画像</param>
    /// <param name="bounds">切り出す矩形領域（画像座標ピクセル単位）</param>
    /// <returns>切り出された新しいビットマップ</returns>
    public static Bitmap Crop(Image source, Rectangle bounds)
    {
        var cropped = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(cropped);
        g.DrawImage(source, new Rectangle(0, 0, cropped.Width, cropped.Height),
            bounds, GraphicsUnit.Pixel);
        return cropped;
    }

    /// <summary>
    /// 画像内のウィンドウ領域を自動判定して切り出す。
    /// </summary>
    /// <param name="image">処理対象画像</param>
    /// <returns>検出されたウィンドウ領域を切り出した新しいビットマップ。検出できない場合は元画像のコピー。</returns>
    /// <remarks>
    /// 四隅の色と同じ色が続く領域を背景とみなして除去する方式。<br/>
    /// 検出結果が 10x10 ピクセル未満の場合は、検出失敗として元画像のコピーを返す。<br/>
    /// </remarks>
    public static Bitmap DetectAndCropWindow(Image image)
    {
        using var bitmap = new Bitmap(image);
        var bounds = DetectWindowBounds(bitmap);

        if (bounds == Rectangle.Empty || bounds == new Rectangle(0, 0, image.Width, image.Height))
        {
            return new Bitmap(image);
        }

        return Crop(image, bounds);
    }

    private static Rectangle DetectWindowBounds(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        var corners = new[]
        {
            bitmap.GetPixel(0, 0), bitmap.GetPixel(width - 1, 0),
            bitmap.GetPixel(0, height - 1), bitmap.GetPixel(width - 1, height - 1)
        };
        var bgColor = corners.GroupBy(c => ColorToArgbKey(c))
                             .MaxBy(g => g.Count())!.First();

        var top = ScanTopBg(bitmap, bgColor, width, height);
        var bottom = ScanBottomBg(bitmap, bgColor, width, height, top);
        var left = ScanLeftBg(bitmap, bgColor, width, top, bottom);
        var right = ScanRightBg(bitmap, bgColor, width, left, top, bottom);

        var detectedWidth = right - left + 1;
        var detectedHeight = bottom - top + 1;

        if (detectedWidth < 10 || detectedHeight < 10 ||
            (left == 0 && top == 0 && right == width - 1 && bottom == height - 1))
        {
            return new Rectangle(0, 0, width, height);
        }

        return new Rectangle(left, top, detectedWidth, detectedHeight);
    }

    private static int ColorToArgbKey(Color c) => (c.R << 16) | (c.G << 8) | c.B;

    private static int ScanTopBg(Bitmap bmp, Color bg, int w, int h)
    {
        for (var y = 0; y < h; y++)
            if (!IsRowColor(bmp, y, bg, w)) return y;
        return 0;
    }

    private static int ScanBottomBg(Bitmap bmp, Color bg, int w, int h, int top)
    {
        for (var y = h - 1; y >= top; y--)
            if (!IsRowColor(bmp, y, bg, w)) return y;
        return h - 1;
    }

    private static int ScanLeftBg(Bitmap bmp, Color bg, int w, int top, int bottom)
    {
        for (var x = 0; x < w; x++)
            if (!IsColColor(bmp, x, bg, top, bottom)) return x;
        return 0;
    }

    private static int ScanRightBg(Bitmap bmp, Color bg, int w, int left, int top, int bottom)
    {
        for (var x = w - 1; x >= left; x--)
            if (!IsColColor(bmp, x, bg, top, bottom)) return x;
        return w - 1;
    }

    private static bool IsRowColor(Bitmap bmp, int y, Color c, int w)
    {
        var step = Math.Max(1, w / 20);
        for (var x = 0; x < w; x += step)
            if (!IsColorSimilar(bmp.GetPixel(x, y), c)) return false;
        return true;
    }

    private static bool IsColColor(Bitmap bmp, int x, Color c, int top, int bottom)
    {
        var step = Math.Max(1, (bottom - top) / 20);
        for (var y = top; y <= bottom; y += step)
            if (!IsColorSimilar(bmp.GetPixel(x, y), c)) return false;
        return true;
    }

    /// <summary>
    /// 2つの色が類似しているかどうかを判定する（許容誤差 30）
    /// </summary>
    private static bool IsColorSimilar(Color a, Color b)
    {
        var diff = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        return diff < 30;
    }
}

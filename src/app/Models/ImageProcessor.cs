namespace app.Models;

/// <summary>
/// 画像処理を提供するクラス（切り出し、ウィンドウ領域検出）
/// </summary>
public static class ImageProcessor
{
    /// <summary>
    /// 画像から指定された領域を切り出す
    /// </summary>
    /// <param name="source">元画像</param>
    /// <param name="bounds">切り出す領域</param>
    /// <returns>切り出された画像</returns>
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
    /// 画像の四辺から背景色（端のピクセル色）が続いている領域を除去し、
    /// 内部の矩形領域を検出する。
    /// </summary>
    /// <param name="image">解析する画像</param>
    /// <returns>検出されたウィンドウ領域。検出できない場合は元の画像の全領域。</returns>
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

    /// <summary>
    /// 画像内のウィンドウ領域の矩形を検出する。
    /// 四辺から走査し、背景色と異なるピクセルが現れる境界を探す。
    /// </summary>
    private static Rectangle DetectWindowBounds(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        // 四隅の色をサンプリングして背景色を推定
        var bgColor = bitmap.GetPixel(0, 0);

        // 上端から走査：背景色と異なる最初の行を探す
        var top = 0;
        for (var y = 0; y < height; y++)
        {
            if (!IsRowBackgroundColor(bitmap, y, bgColor, width))
            {
                top = y;
                break;
            }
        }

        // 下端から走査
        var bottom = height - 1;
        for (var y = height - 1; y >= top; y--)
        {
            if (!IsRowBackgroundColor(bitmap, y, bgColor, width))
            {
                bottom = y;
                break;
            }
        }

        // 左端から走査
        var left = 0;
        for (var x = 0; x < width; x++)
        {
            if (!IsColumnBackgroundColor(bitmap, x, bgColor, top, bottom))
            {
                left = x;
                break;
            }
        }

        // 右端から走査
        var right = width - 1;
        for (var x = width - 1; x >= left; x--)
        {
            if (!IsColumnBackgroundColor(bitmap, x, bgColor, top, bottom))
            {
                right = x;
                break;
            }
        }

        // 検出結果が極端に小さい、または元画像とほぼ同じ場合は空を返す
        var detectedWidth = right - left + 1;
        var detectedHeight = bottom - top + 1;

        if (detectedWidth < 10 || detectedHeight < 10 ||
            (left == 0 && top == 0 && right == width - 1 && bottom == height - 1))
        {
            return new Rectangle(0, 0, width, height);
        }

        return new Rectangle(left, top, detectedWidth, detectedHeight);
    }

    /// <summary>
    /// 指定された行が背景色かどうかを判定する
    /// </summary>
    private static bool IsRowBackgroundColor(Bitmap bitmap, int y, Color bgColor, int width)
    {
        // 10ピクセル間隔でサンプリング（パフォーマンス向上）
        var step = Math.Max(1, width / 20);
        for (var x = 0; x < width; x += step)
        {
            if (!IsColorSimilar(bitmap.GetPixel(x, y), bgColor))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 指定された列が背景色かどうかを判定する
    /// </summary>
    private static bool IsColumnBackgroundColor(Bitmap bitmap, int x, Color bgColor, int top, int bottom)
    {
        var step = Math.Max(1, (bottom - top) / 20);
        for (var y = top; y <= bottom; y += step)
        {
            if (!IsColorSimilar(bitmap.GetPixel(x, y), bgColor))
            {
                return false;
            }
        }

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

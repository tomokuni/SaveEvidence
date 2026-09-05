using Avalonia;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>画像の切り出し処理と、背景色を自動判別したウィンドウ領域検出を提供する静的ユーティリティクラス。</summary>
/// <remarks>
/// <see cref="AppPixelData"/> に対してピクセル配列レベルで操作を行う（UI フレームワーク非依存・状態なし）。<br/>
/// ウィンドウ領域検出は、画像四隅の背景色を基準に、同じ色が連続する領域を除去する方式を採用する。<br/>
/// 色の類似判定は RGB 各成分の差分合計が <see cref="ColorSimilarThreshold"/> 未満を「類似」とみなす。<br/>
/// 全メソッドが静的であり、インスタンス化は不要（他フレームワーク・テスト環境への移植容易性を確保）。<br/>
/// </remarks>
public static class ImageProcessor
{
    /// <summary>背景色の類似判定におけるサンプル数（走査範囲を均等分割する個数）。</summary>
    private const int SampleCount = 20;

    /// <summary>色の類似判定閾値（RGB 各成分の差分合計）。未満なら「類似」とみなす。</summary>
    private const int ColorSimilarThreshold = 30;

    /// <summary>ウィンドウ領域検出の最小サイズ（縦・横ピクセル）。未満は検出失敗とみなす。</summary>
    private const int MinDetectSize = 10;

    /// <summary>画像から指定された矩形領域を切り出す。</summary>
    /// <param name="source">元画像。</param>
    /// <param name="bounds">切り出す矩形領域（画像座標ピクセル単位）。</param>
    /// <returns>切り出された新しい画像データ。範囲外の場合は null。</returns>
    public static AppPixelData? Crop(AppPixelData source, PixelRect bounds)
    {
        var srcPixels = source.PixelData;
        var srcStride = source.Stride;
        var srcW = source.Width;
        var srcH = source.Height;

        var dstW = bounds.Width;
        var dstH = bounds.Height;
        if (dstW <= 0 || dstH <= 0) return null;

        // ソース範囲内にクランプ
        var cropX = Math.Max(0, bounds.X);
        var cropY = Math.Max(0, bounds.Y);
        var cropW = Math.Min(dstW, srcW - cropX);
        var cropH = Math.Min(dstH, srcH - cropY);
        if (cropW <= 0 || cropH <= 0) return null;

        var dstStride = dstW * AppPixelData.BytesPerPixel;
        var dstPixels = new byte[dstStride * dstH];

        for (var y = 0; y < cropH; y++)
        {
            var srcRowStart = (cropY + y) * srcStride + cropX * AppPixelData.BytesPerPixel;
            var dstRowStart = y * dstStride;
            srcPixels.AsSpan(srcRowStart, cropW * AppPixelData.BytesPerPixel)
                     .CopyTo(dstPixels.AsSpan(dstRowStart, cropW * AppPixelData.BytesPerPixel));
        }

        return new AppPixelData(dstW, dstH, dstStride, dstPixels);
    }

    /// <summary>画像内のウィンドウ領域を自動判定して切り出す。</summary>
    /// <param name="source">処理対象画像。</param>
    /// <returns>検出されたウィンドウ領域を切り出した新しい画像データ。検出できない場合は元画像のコピー。</returns>
    /// <remarks>
    /// 四隅の色と同じ色が続く領域を背景とみなして除去する方式。<br/>
    /// 検出結果が縦横 <see cref="MinDetectSize"/> ピクセル未満の場合は、検出失敗として元画像のコピーを返す。<br/>
    /// </remarks>
    public static AppPixelData? DetectAndCropWindow(AppPixelData source)
    {
        var bounds = DetectWindowBounds(source);

        // DetectWindowBounds は常に 0 以上の矩形を返す（検出失敗時は画像全体）。全体の場合はコピーを返す。
        var fullRect = new PixelRect(0, 0, source.Width, source.Height);
        if (bounds == fullRect)
        {
            // 元画像のコピーを返す
            var stride = source.Width * AppPixelData.BytesPerPixel;
            var pixels = source.PixelData.ToArray();
            return new AppPixelData(source.Width, source.Height, stride, pixels);
        }

        return Crop(source, bounds);
    }

    /// <summary>画像四隅の背景色を基準に、ウィンドウ領域の矩形を自動検出する。</summary>
    /// <param name="source">解析対象の画像データ。</param>
    /// <returns>検出されたウィンドウ領域の矩形。検出失敗時は画像全体の矩形を返す。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 四隅のピクセル色を取得し、最頻色を背景色として決定<br/>
    /// 2. 上・下・左・右から背景色と異なるピクセルが現れる境界を探索<br/>
    /// 3. 検出領域が縦横 <see cref="MinDetectSize"/> ピクセル未満の場合は検出失敗として画像全体を返す<br/>
    /// </remarks>
    public static PixelRect DetectWindowBounds(AppPixelData source)
    {
        var width = source.Width;
        var height = source.Height;
        var stride = source.Stride;
        var pixels = source.PixelData;

        // 四隅の色を取得
        var corners = new[]
        {
            GetPixelArgb(pixels, stride, 0, 0),
            GetPixelArgb(pixels, stride, width - 1, 0),
            GetPixelArgb(pixels, stride, 0, height - 1),
            GetPixelArgb(pixels, stride, width - 1, height - 1)
        };
        var bgArgb = corners.GroupBy(c => c)
                            .MaxBy(g => g.Count())!;

        var bgR = (byte)(bgArgb.Key >> 16);
        var bgG = (byte)(bgArgb.Key >> 8);
        var bgB = (byte)(bgArgb.Key);

        var top = ScanTopBg(pixels, stride, bgR, bgG, bgB, width, height);
        var bottom = ScanBottomBg(pixels, stride, bgR, bgG, bgB, width, height, top);
        var left = ScanLeftBg(pixels, stride, bgR, bgG, bgB, width, top, bottom);
        var right = ScanRightBg(pixels, stride, bgR, bgG, bgB, width, left, top, bottom);

        var detectedWidth = right - left + 1;
        var detectedHeight = bottom - top + 1;

        if (detectedWidth < MinDetectSize || detectedHeight < MinDetectSize ||
            (left == 0 && top == 0 && right == width - 1 && bottom == height - 1))
        {
            return new PixelRect(0, 0, width, height);
        }

        return new PixelRect(left, top, detectedWidth, detectedHeight);
    }

    /// <summary>指定座標のピクセルから ARGB キー値（RGB下位24bit）を取得する。</summary>
    private static int GetPixelArgb(ReadOnlyMemory<byte> pixels, int stride, int x, int y)
    {
        var off = y * stride + x * AppPixelData.BytesPerPixel;
        var span = pixels.Span;
        // BGRA ストレージ: span[off]=B, span[off+1]=G, span[off+2]=R, span[off+3]=A
        var r = span[off + 2];
        var g = span[off + 1];
        var b = span[off];
        return (r << 16) | (g << 8) | b;
    }

    /// <summary>上端から背景色が続く行数をスキャンし、最初の非背景色行のY座標を返す。</summary>
    private static int ScanTopBg(ReadOnlyMemory<byte> pixels, int stride,
        byte bgR, byte bgG, byte bgB, int w, int h)
    {
        for (var y = 0; y < h; y++)
            if (!IsRowColor(pixels, stride, y, bgR, bgG, bgB, w, 0, w - 1)) return y;
        return 0;
    }

    /// <summary>下端から背景色が続く行数をスキャンし、最後の非背景色行のY座標を返す。</summary>
    private static int ScanBottomBg(ReadOnlyMemory<byte> pixels, int stride,
        byte bgR, byte bgG, byte bgB, int w, int h, int top)
    {
        for (var y = h - 1; y >= top; y--)
            if (!IsRowColor(pixels, stride, y, bgR, bgG, bgB, w, 0, w - 1)) return y;
        return h - 1;
    }

    /// <summary>左端から背景色が続く列をスキャンし、最初の非背景色列のX座標を返す。</summary>
    private static int ScanLeftBg(ReadOnlyMemory<byte> pixels, int stride,
        byte bgR, byte bgG, byte bgB, int w, int top, int bottom)
    {
        for (var x = 0; x < w; x++)
            if (!IsColColor(pixels, stride, x, bgR, bgG, bgB, top, bottom)) return x;
        return 0;
    }

    /// <summary>右端から背景色が続く列をスキャンし、最後の非背景色列のX座標を返す。</summary>
    private static int ScanRightBg(ReadOnlyMemory<byte> pixels, int stride,
        byte bgR, byte bgG, byte bgB, int w, int left, int top, int bottom)
    {
        for (var x = w - 1; x >= left; x--)
            if (!IsColColor(pixels, stride, x, bgR, bgG, bgB, top, bottom)) return x;
        return w - 1;
    }

    /// <summary>指定された行の全サンプルピクセルが指定色に類似しているかを判定する。</summary>
    private static bool IsRowColor(ReadOnlyMemory<byte> pixels, int stride, int y,
        byte bgR, byte bgG, byte bgB, int w, int startX, int endX)
    {
        var step = Math.Max(1, (endX - startX + 1) / SampleCount);
        for (var x = startX; x <= endX; x += step)
        {
            var argb = GetPixelArgb(pixels, stride, x, y);
            var pr = (byte)(argb >> 16);
            var pg = (byte)(argb >> 8);
            var pb = (byte)argb;
            if (!IsColorSimilar(pr, pg, pb, bgR, bgG, bgB)) return false;
        }
        return true;
    }

    /// <summary>指定された列の全サンプルピクセルが指定色に類似しているかを判定する。</summary>
    private static bool IsColColor(ReadOnlyMemory<byte> pixels, int stride, int x,
        byte bgR, byte bgG, byte bgB, int top, int bottom)
    {
        var step = Math.Max(1, (bottom - top + 1) / SampleCount);
        for (var y = top; y <= bottom; y += step)
        {
            var argb = GetPixelArgb(pixels, stride, x, y);
            var pr = (byte)(argb >> 16);
            var pg = (byte)(argb >> 8);
            var pb = (byte)argb;
            if (!IsColorSimilar(pr, pg, pb, bgR, bgG, bgB)) return false;
        }
        return true;
    }

    /// <summary>2色が類似しているかを判定する（RGB 各成分の差分合計が閾値未満）。</summary>
    private static bool IsColorSimilar(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var dr = Math.Abs(r1 - r2);
        var dg = Math.Abs(g1 - g2);
        var db = Math.Abs(b1 - b2);
        return dr + dg + db < ColorSimilarThreshold;
    }
}

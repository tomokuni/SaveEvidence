using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ZoomViewControl.Sample.Imaging;

/// <summary>ズーム・パン動作確認用テスト画像を生成する。</summary>
public static class TestImageFactory
{
    /// <summary>グラデーション＋グリッド＋マーカー付きのテスト画像を生成する。</summary>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <returns>生成した WriteableBitmap。</returns>
    /// <remarks>
    /// 左上から右下へのグラデーションを背景とし、<br/>
    /// 10px グリッド（拡大ピクセル検証用）・50px グリッド（倍率計測用）・色付き円マーカー（ズーム確認用）を描画する。<br/>
    /// グリッドの間隔がズーム倍率どおりに拡大されるため、倍率の検証に使用できる。
    /// </remarks>
    public static WriteableBitmap Create(int width = 800, int height = 600)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

        using var locked = bitmap.Lock();
        unsafe
        {
            var ptr = (byte*)locked.Address;
            var stride = locked.RowBytes;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    byte r;
                    byte g;
                    byte b;

                    // 左上から右下へのグラデーション背景
                    var t = (x + y) / (double)(width + height);
                    r = (byte)(t * 200 + 30);
                    g = (byte)((1.0 - (double)x / width) * 140 + 50);
                    b = (byte)((double)y / height * 180 + 40);

                    // 色付きマーカー（円）: ズーム確認用
                    if (InCircle(x, y, 120, 110, 45))
                    {
                        (r, g, b) = (255, 60, 60);
                    }
                    else if (InCircle(x, y, 660, 160, 55))
                    {
                        (r, g, b) = (80, 255, 80);
                    }
                    else if (InCircle(x, y, 400, 430, 75))
                    {
                        (r, g, b) = (80, 120, 255);
                    }

                    // 10px グリッド（薄く明るく）: 拡大ピクセル検証用
                    if (x % 10 == 0 || y % 10 == 0)
                    {
                        r = (byte)Math.Min(255, r + 60);
                        g = (byte)Math.Min(255, g + 60);
                        b = (byte)Math.Min(255, b + 60);
                    }

                    // 50px グリッド（濃く暗く）: 倍率計測用
                    if (x % 50 == 0 || y % 50 == 0)
                    {
                        r = (byte)Math.Max(0, r - 100);
                        g = (byte)Math.Max(0, g - 100);
                        b = (byte)Math.Max(0, b - 100);
                    }

                    var offset = y * stride + x * 4;
                    ptr[offset] = b;     // B
                    ptr[offset + 1] = g; // G
                    ptr[offset + 2] = r; // R
                    ptr[offset + 3] = 255; // A
                }
            }
        }

        return bitmap;
    }

    /// <summary>指定座標が円の内側かどうかを判定する。</summary>
    private static bool InCircle(int x, int y, int cx, int cy, int radius)
    {
        var dx = x - cx;
        var dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }
}

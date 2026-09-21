using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Imaging;

namespace ZoomViewControl.Sample.Imaging;

/// <summary>ズーム・パン動作確認用テスト画像を生成する。</summary>
public static class TestImageFactory
{
    /// <summary>グラデーション＋グリッド＋マーカー付きのテスト画像を生成する。</summary>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <returns>生成した <see cref="EsBitmapAvaloniaImage"/>（<see cref="IImage"/> として表示できる）。</returns>
    /// <remarks>
    /// 左上から右下へのグラデーションを背景とし、<br/>
    /// 10px グリッド（拡大ピクセル検証用）・50px グリッド（倍率計測用）・色付き円マーカー（ズーム確認用）を描画する。<br/>
    /// グリッドの間隔がズーム倍率どおりに拡大されるため、倍率の検証に使用できる。<br/>
    /// <b>EsBitmap 採用理由</b>: <c>WriteableBitmap</c>（<c>Lock()</c> 書き込み）を避け、
    /// <see cref="EsBitmapAvaloniaImage"/>（Skia 直描画・ゼロコピー）として表示できる。
    /// </remarks>
    public static IImage Create(int width = 800, int height = 600)
    {
        var bitmap = EsBitmap.Create(width, height);

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

                bitmap.SetPixel(x, y, b, g, r, 255);
            }
        }

        // EsBitmap を内包した IImage（EsBitmapAvaloniaImage）を返す（所有権は EsBitmapAvaloniaImage へ移動）。
        return bitmap.ToAvaloniaImage();
    }

    /// <summary>指定座標が円の内側かどうかを判定する。</summary>
    private static bool InCircle(int x, int y, int cx, int cy, int radius)
    {
        var dx = x - cx;
        var dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }
}

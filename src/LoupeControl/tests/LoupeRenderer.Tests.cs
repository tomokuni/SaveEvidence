using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Estable.AvaloniaUI.Loupe.Rendering;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary>
/// <see cref="LoupeRenderer"/> の単体テスト。
/// </summary>
/// <remarks>Avalonia ヘッドレス依存のため <see cref="AvaloniaHeadlessCollection"/> 内で直列実行する。</remarks>
[Collection("AvaloniaHeadless")]
public class LoupeRendererTests
{
    /// <summary>テスト用 Bitmap（白で塗りつぶし）を生成する。</summary>
    private static unsafe Bitmap CreateBitmap(int size)
    {
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var fb = bitmap.Lock();
        // RowBytes を考慮して各行ごとに白を書き込む（連続書き込みは行ずれの原因になる）
        var buffer = new byte[fb.RowBytes * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * fb.RowBytes + x * 4;
                buffer[idx + 0] = 255; // B
                buffer[idx + 1] = 255; // G
                buffer[idx + 2] = 255; // R
                buffer[idx + 3] = 255; // A
            }
        }
        Marshal.Copy(buffer, 0, fb.Address, buffer.Length);
        return bitmap;
    }

    [AvaloniaFact]
    public void DrawLoupe_指定サイズのBitmapを生成する()
    {
        var src = CreateBitmap(10);

        var result = LoupeRenderer.DrawLoupe(src, 160, 4, Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(160, result.PixelSize.Width);
        Assert.Equal(160, result.PixelSize.Height);
    }

    [AvaloniaFact]
    public void DrawLoupeCheckerboardOnly_指定サイズのBitmapを生成する()
    {
        var result = LoupeRenderer.DrawLoupeCheckerboardOnly(100, Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(100, result.PixelSize.Width);
        Assert.Equal(100, result.PixelSize.Height);
    }

    [AvaloniaFact]
    public void DrawLoupeCheckerboardOnly_チェッカーサイズ1未満でも動作する()
    {
        var result = LoupeRenderer.DrawLoupeCheckerboardOnly(50, Brushes.Red, 0, null, null, Brushes.Black, 1);

        Assert.NotNull(result);
        Assert.Equal(50, result.PixelSize.Width);
    }

    [AvaloniaFact]
    public void DrawLoupe_小さいルーペサイズでも動作する()
    {
        var src = CreateBitmap(4);

        var result = LoupeRenderer.DrawLoupe(src, 8, 2, Brushes.Red, 2, Brushes.White, Brushes.LightGray, Brushes.Black, 1);

        Assert.NotNull(result);
        Assert.Equal(8, result.PixelSize.Width);
        Assert.Equal(8, result.PixelSize.Height);
    }

    [AvaloniaFact]
    public void DrawLoupe_色がnullでも動作する()
    {
        var src = CreateBitmap(10);

        // すべての色が null でも例外なく描画する（十字線・市松・外枠をスキップ）
        var result = LoupeRenderer.DrawLoupe(src, 64, 4, null, 6, null, null, null, 2);

        Assert.NotNull(result);
        Assert.Equal(64, result.PixelSize.Width);
    }

    [AvaloniaFact]
    public void DrawLoupeCheckerboardOnly_色がnullでも動作する()
    {
        var result = LoupeRenderer.DrawLoupeCheckerboardOnly(50, null, 6, null, null, null, 2);

        Assert.NotNull(result);
        Assert.Equal(50, result.PixelSize.Width);
    }

    // ====================================================================
    // Render（一元化エントリポイント）
    // ====================================================================

    [AvaloniaFact]
    public void Render_ソースと中心点からルーペBitmapを生成する()
    {
        var src = CreateBitmap(100);

        var result = LoupeRenderer.Render(
            src, new Point(50, 50), 160, 4,
            Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(160, result.PixelSize.Width);
        Assert.Equal(160, result.PixelSize.Height);
    }

    [AvaloniaFact]
    public void Render_ソース未指定では市松模様のみを生成する()
    {
        var result = LoupeRenderer.Render(
            null, null, 100, 4,
            Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(100, result.PixelSize.Width);
        Assert.Equal(100, result.PixelSize.Height);
    }

    [AvaloniaFact]
    public void Render_中心点未指定では市松模様のみを生成する()
    {
        var src = CreateBitmap(100);

        var result = LoupeRenderer.Render(
            src, null, 100, 4,
            Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(100, result.PixelSize.Width);
    }

    // ====================================================================
    // 選択矩形の破線オーバーレイ
    // ====================================================================

    [AvaloniaFact]
    public void DrawLoupe_選択矩形を指定して破線が描画される()
    {
        var src = CreateBitmap(10);

        // 選択矩形（ドメイン座標系）と中心を指定して描画する（2 色破線: 色A/色B・不透明度はアルファに統合）
        var result = LoupeRenderer.DrawLoupe(
            src, 160, 4, new SolidColorBrush(Color.FromArgb(102, 255, 0, 0)), 6,
            Brushes.White, Brushes.LightGray, Brushes.Black, 2,
            selectionRect: new Rect(40, 40, 20, 20),
            centerPoint: new Point(50, 50),
            selectionRectColorA: new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)),
            selectionRectColorB: new SolidColorBrush(Color.FromArgb(204, 0, 0, 0)));

        Assert.NotNull(result);
        Assert.Equal(160, result.PixelSize.Width);
        Assert.Equal(160, result.PixelSize.Height);
    }

    [AvaloniaFact]
    public void DrawLoupe_選択矩形なしでも動作する()
    {
        var src = CreateBitmap(10);

        // 選択矩形なし（既定）でも例外なく描画する
        var result = LoupeRenderer.DrawLoupe(
            src, 64, 4, Brushes.Red, 6,
            Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(64, result.PixelSize.Width);
    }

    [AvaloniaFact]
    public void DrawLoupe_選択矩形の色がnullなら描画しない()
    {
        var src = CreateBitmap(10);

        // 色 null でも例外なく描画する（選択矩形はスキップ）
        var result = LoupeRenderer.DrawLoupe(
            src, 64, 4, null, 6, null, null, null, 2,
            selectionRect: new Rect(10, 10, 20, 20),
            centerPoint: new Point(30, 30),
            selectionRectColorA: null,
            selectionRectColorB: null);

        Assert.NotNull(result);
        Assert.Equal(64, result.PixelSize.Width);
    }

    [AvaloniaFact]
    public void DrawLoupe_選択矩形の色Bのみnullなら描画しない()
    {
        var src = CreateBitmap(10);

        // 色A のみ指定（色B は null）→ 選択矩形はスキップされる（例外なし）
        var result = LoupeRenderer.DrawLoupe(
            src, 64, 4, null, 6, null, null, null, 2,
            selectionRect: new Rect(10, 10, 20, 20),
            centerPoint: new Point(30, 30),
            selectionRectColorA: Brushes.White,
            selectionRectColorB: null);

        Assert.NotNull(result);
        Assert.Equal(64, result.PixelSize.Width);
    }

    [AvaloniaFact]
    public void DrawSelectionRect_終点のピクセルを含む閉区間で描画される()
    {
        // 白背景に選択矩形（半開区間 Rect(0,0,2,2) = ピクセル [0,2)）の破線を直接描画する
        var rtb = new RenderTargetBitmap(new PixelSize(40, 40), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.FillRectangle(Brushes.White, new Rect(0, 0, 40, 40));
            // zoom=4: 終点を含む閉区間では右辺 = (Width+1)*zoom = 3*4 = 12（終点ピクセル [1,2) の右端 = 2*4=8 ではない）
            // 2 色破線（色A=黒 / 色B=透明）: 色A のダッシュ（index 0 始まり = 黒）で描画される
            LoupeDecorationRenderer.DrawSelectionRect(
                ctx, new Rect(0, 0, 2, 2), default, 4, 40,
                Brushes.Black, Brushes.Transparent);
        }

        // 右辺の破線位置（x=12）のピクセルを読み取る。
        // 破線パターンは 1px 描画 → 1px 空白（step=2）のため、y=4 は dash 内（4 mod 2 = 0 < 1）。
        // 色は 1px ごとに色A → 色B と交互のため、y=4 は色A（黒）が描画される。
        var pixels = new byte[4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            rtb.CopyPixels(new PixelRect(12, 4, 1, 1), handle.AddrOfPinnedObject(), pixels.Length, 4);
        }
        finally
        {
            handle.Free();
        }
        // 白背景（255）より暗い = 線が描画されている（終点のピクセルを含む閉区間で右辺が引かれている）
        Assert.True(pixels[0] < 255, $"終点の境界に線が描画されていない: B={pixels[0]}");
    }

    // ====================================================================
    // LoupeDecorationRenderer（十字線・外枠・選択矩形の null / 無効分岐）
    // ====================================================================

    [AvaloniaFact]
    public void DrawCrosshair_色がnullなら何も描画しない()
    {
        using var ctx = CreateContext();

        // null 色 → 十字線を描画しない（例外なし）
        LoupeDecorationRenderer.DrawCrosshair(ctx, 100, 4, null);
    }

    [AvaloniaFact]
    public void DrawFrame_色がnullなら何も描画しない()
    {
        using var ctx = CreateContext();

        // null 色 → 外枠を描画しない（例外なし）
        LoupeDecorationRenderer.DrawFrame(ctx, 100, null, 2);
    }

    [AvaloniaFact]
    public void DrawSelectionRect_色がnullなら何も描画しない()
    {
        using var ctx = CreateContext();

        // 色A / 色B のいずれかが null → 選択矩形を描画しない
        LoupeDecorationRenderer.DrawSelectionRect(ctx, new Rect(10, 10, 50, 40), new Point(0, 0), 4, 160, null, Brushes.Black);
        LoupeDecorationRenderer.DrawSelectionRect(ctx, new Rect(10, 10, 50, 40), new Point(0, 0), 4, 160, Brushes.White, null);
    }

    [AvaloniaFact]
    public void DrawSelectionRect_ズーム1倍なら何も描画しない()
    {
        using var ctx = CreateContext();

        // ズーム 1 倍 → 破線が潰れるため描画しない
        LoupeDecorationRenderer.DrawSelectionRect(ctx, new Rect(10, 10, 50, 40), new Point(0, 0), 1, 160, Brushes.White, Brushes.Black);
    }

    [AvaloniaFact]
    public void DrawSelectionRect_切り出し領域と交差しない場合は何も描画しない()
    {
        using var ctx = CreateContext();

        // ルーペの切り出し領域（0,0 起点）と交差しない矩形（負方向）→ 描画しない
        LoupeDecorationRenderer.DrawSelectionRect(ctx, new Rect(-100, -100, 10, 10), new Point(0, 0), 4, 160, Brushes.White, Brushes.Black);
    }

    /// <summary>テスト用の DrawingContext を生成する。</summary>
    private static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(160, 160), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }
}

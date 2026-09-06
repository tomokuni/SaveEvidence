using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Loupe.Rendering;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary>
/// <see cref="LoupeRenderer"/> の単体テスト。
/// </summary>
/// <remarks>Avalonia ヘッドレス依存のため <see cref="AvaloniaHeadlessCollection"/> 内で直列実行する。</remarks>
[Collection("AvaloniaHeadless")]
public class LoupeRendererTests
{
    /// <summary>テスト用 PbBitmap（白で塗りつぶし）を生成する。</summary>
    private static PbBitmap CreateBitmap(int size)
    {
        var bitmap = PbBitmap.Create(size, size);
        bitmap.Clear(b: 255, g: 255, r: 255, a: 255);
        return bitmap;
    }

    [AvaloniaFact]
    public void DrawLoupe_指定サイズのBitmapを生成する()
    {
        using var src = CreateBitmap(10);

        var result = LoupeRenderer.DrawLoupe(src, 160, 4, Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(160, result.Size.Width);
        Assert.Equal(160, result.Size.Height);
    }

    [AvaloniaFact]
    public void DrawLoupeCheckerboardOnly_指定サイズのBitmapを生成する()
    {
        var result = LoupeRenderer.DrawLoupeCheckerboardOnly(100, Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(100, result.Size.Width);
        Assert.Equal(100, result.Size.Height);
    }

    [AvaloniaFact]
    public void DrawLoupeCheckerboardOnly_チェッカーサイズ1未満でも動作する()
    {
        var result = LoupeRenderer.DrawLoupeCheckerboardOnly(50, Brushes.Red, 0, null, null, Brushes.Black, 1);

        Assert.NotNull(result);
        Assert.Equal(50, result.Size.Width);
    }

    [AvaloniaFact]
    public void DrawLoupe_小さいルーペサイズでも動作する()
    {
        using var src = CreateBitmap(4);

        var result = LoupeRenderer.DrawLoupe(src, 8, 2, Brushes.Red, 2, Brushes.White, Brushes.LightGray, Brushes.Black, 1);

        Assert.NotNull(result);
        Assert.Equal(8, result.Size.Width);
        Assert.Equal(8, result.Size.Height);
    }

    [AvaloniaFact]
    public void DrawLoupe_色がnullでも動作する()
    {
        using var src = CreateBitmap(10);

        // すべての色が null でも例外なく描画する（十字線・市松・外枠をスキップ）
        var result = LoupeRenderer.DrawLoupe(src, 64, 4, null, 6, null, null, null, 2);

        Assert.NotNull(result);
        Assert.Equal(64, result.Size.Width);
    }

    [AvaloniaFact]
    public void DrawLoupeCheckerboardOnly_色がnullでも動作する()
    {
        var result = LoupeRenderer.DrawLoupeCheckerboardOnly(50, null, 6, null, null, null, 2);

        Assert.NotNull(result);
        Assert.Equal(50, result.Size.Width);
    }

    // ====================================================================
    // Render（一元化エントリポイント）
    // ====================================================================

    [AvaloniaFact]
    public void Render_ソースと中心点からルーペBitmapを生成する()
    {
        using var src = CreateBitmap(100);

        var result = LoupeRenderer.Render(
            src, new Point(50, 50), 160, 4,
            Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(160, result.Size.Width);
        Assert.Equal(160, result.Size.Height);
    }

    [AvaloniaFact]
    public void Render_ソース未指定では市松模様のみを生成する()
    {
        var result = LoupeRenderer.Render(
            null, null, 100, 4,
            Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(100, result.Size.Width);
        Assert.Equal(100, result.Size.Height);
    }

    [AvaloniaFact]
    public void Render_中心点未指定では市松模様のみを生成する()
    {
        using var src = CreateBitmap(100);

        var result = LoupeRenderer.Render(
            src, null, 100, 4,
            Brushes.Red, 6, Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(100, result.Size.Width);
    }

    [AvaloniaFact]
    public void DrawLoupe_選択矩形なしでも動作する()
    {
        using var src = CreateBitmap(10);

        // 選択矩形なし（既定）でも例外なく描画する
        var result = LoupeRenderer.DrawLoupe(
            src, 64, 4, Brushes.Red, 6,
            Brushes.White, Brushes.LightGray, Brushes.Black, 2);

        Assert.NotNull(result);
        Assert.Equal(64, result.Size.Width);
    }

    // ====================================================================
    // LoupeDecorationRenderer（十字線・外枠の null / 無効分岐）
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

    /// <summary>テスト用の DrawingContext を生成する。</summary>
    private static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(160, 160), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }
}

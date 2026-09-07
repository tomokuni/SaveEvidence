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

/// <summary><see cref="LoupeDecorationRenderer"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 十字線・外枠の描画矩形の計算のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <c>DrawingContext</c> への描画は <see cref="LoupeRendererTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeDecorationRendererTests
{
    [Fact]
    public void ComputeCrosshairRects_十字線の縦横2本の矩形を返す()
    {
        // pixelSize=100, zoomLevel=4 → blocks = ceil(100/4) = 25、start = 25/2*4 = 48
        var rects = LoupeDecorationRenderer.ComputeCrosshairRects(100, 4);

        Assert.Equal(2, rects.Count);
        Assert.Equal(new Rect(48, 0, 4, 100), rects[0]); // 縦
        Assert.Equal(new Rect(0, 48, 100, 4), rects[1]); // 横
    }

    [Fact]
    public void ComputeCrosshairRects_zoomLevel1では中央を1ピクセルで描画する()
    {
        // pixelSize=100, zoomLevel=1 → blocks = 100、start = 100/2*1 = 50
        var rects = LoupeDecorationRenderer.ComputeCrosshairRects(100, 1);

        Assert.Equal(new Rect(50, 0, 1, 100), rects[0]);
        Assert.Equal(new Rect(0, 50, 100, 1), rects[1]);
    }

    [Fact]
    public void ComputeFrameRects_外枠の4辺の矩形を返す()
    {
        // pixelSize=100, width=2 → frame=2、inner=100-4=96
        var rects = LoupeDecorationRenderer.ComputeFrameRects(100, 2);

        Assert.Equal(4, rects.Count);
        Assert.Equal(new Rect(0, 0, 100, 2), rects[0]);       // 上
        Assert.Equal(new Rect(0, 98, 100, 2), rects[1]);      // 下
        Assert.Equal(new Rect(0, 2, 2, 96), rects[2]);        // 左
        Assert.Equal(new Rect(98, 2, 2, 96), rects[3]);       // 右
    }

    [Fact]
    public void ComputeFrameRects_幅がルーペサイズを超える場合は1に正規化する()
    {
        // width=0 → frame = clamp(0,1,100) = 1、inner = 100-2 = 98
        var rects = LoupeDecorationRenderer.ComputeFrameRects(100, 0);

        Assert.Equal(new Rect(0, 0, 100, 1), rects[0]);       // 上
        Assert.Equal(new Rect(0, 99, 100, 1), rects[1]);      // 下
        Assert.Equal(new Rect(0, 1, 1, 98), rects[2]);        // 左
        Assert.Equal(new Rect(99, 1, 1, 98), rects[3]);       // 右
    }
}

/// <summary><see cref="LoupeBackgroundRenderer"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 市松模様の色選択（暗色タイル判定）のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <c>DrawingContext</c> への描画は <see cref="LoupeRendererTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeBackgroundRendererTests
{
    [Fact]
    public void IsDarkChecker_交互パターンで暗色を判定する()
    {
        Assert.True(LoupeBackgroundRenderer.IsDarkChecker(0, 1));
        Assert.True(LoupeBackgroundRenderer.IsDarkChecker(1, 0));
        Assert.False(LoupeBackgroundRenderer.IsDarkChecker(0, 0));
        Assert.False(LoupeBackgroundRenderer.IsDarkChecker(1, 1));
    }

    [Fact]
    public void IsDarkChecker_列と行の和が奇数なら暗色になる()
    {
        // (2 + 3) = 5（奇数）→ 暗色
        Assert.True(LoupeBackgroundRenderer.IsDarkChecker(2, 3));
        // (4 + 2) = 6（偶数）→ 明色
        Assert.False(LoupeBackgroundRenderer.IsDarkChecker(4, 2));
    }
}

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Loupe.Rendering;
using Estable.Loupe;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary>
/// <see cref="LoupeRenderer"/> の単体テスト。
/// </summary>
/// <remarks>Avalonia ヘッドレス依存のため <see cref="AvaloniaHeadlessCollection"/> 内で直列実行する。</remarks>
[Collection("AvaloniaHeadless")]
public class LoupeRendererTests
{
    /// <summary>透明色（アルファ 0。描画しない指定）。</summary>
    private static readonly DrawingColor Transparent = DrawingColor.FromArgb(0, 0, 0, 0);

    /// <summary>テスト用の外観を生成する（色は UI 非依存のコア色型）。</summary>
    /// <param name="cross">十字線の色。</param>
    /// <param name="checkerSize">市松模様のタイルサイズ（DIP）。</param>
    /// <param name="checkerA">市松模様の明色。</param>
    /// <param name="checkerB">市松模様の暗色。</param>
    /// <param name="frame">外枠の色。</param>
    /// <param name="frameWidth">外枠の太さ（DIP）。</param>
    /// <returns>テスト用の外観。</returns>
    private static LoupeAppearance CreateAppearance(
        DrawingColor cross, int checkerSize, DrawingColor checkerA, DrawingColor checkerB, DrawingColor frame, int frameWidth)
        => new()
        {
            CrossColor = cross,
            CheckerSize = checkerSize,
            CheckerColorA = checkerA,
            CheckerColorB = checkerB,
            FrameColor = frame,
            FrameWidth = frameWidth,
        };

    /// <summary>テスト用の描画入力を生成する（中心は画像中央）。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="appearance">装飾の外観。</param>
    /// <param name="center">拡大中心（ドメイン座標）。未設定は null。</param>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest CreateRequest(
        int pixelSize, int zoomLevel, LoupeAppearance appearance, EsPoint? center = null)
        => new(center, pixelSize, zoomLevel, appearance, 1.0);

    /// <summary>テスト用 EsBitmap（白で塗りつぶし）を生成する。</summary>
    private static EsBitmap CreateBitmap(int size)
    {
        var bitmap = EsBitmap.Create(size, size);
        bitmap.Clear(b: 255, g: 255, r: 255, a: 255);
        return bitmap;
    }

    [AvaloniaFact]
    public void DrawLoupe_指定サイズのBitmapを生成する()
    {
        using var src = CreateBitmap(10);

        var result = LoupeRenderer.DrawLoupe(
            src, CreateRequest(160, 4,
                CreateAppearance(DrawingColor.Red, 6, DrawingColor.White, DrawingColor.LightGray, DrawingColor.Black, 2),
                new EsPoint(5, 5)));

        Assert.NotNull(result);
        Assert.Equal(160, result.Size.Width);
        Assert.Equal(160, result.Size.Height);
    }

    [AvaloniaFact]
    public void DrawLoupe_切り出せない場合は市松模様のみを生成する()
    {
        // 中心未設定 → 切り出せない（市松模様と外枠のみを描画する）
        var result = LoupeRenderer.DrawLoupe(
            null, CreateRequest(100, 4,
                CreateAppearance(DrawingColor.Red, 6, DrawingColor.White, DrawingColor.LightGray, DrawingColor.Black, 2)));

        Assert.NotNull(result);
        Assert.Equal(100, result.Size.Width);
        Assert.Equal(100, result.Size.Height);
    }

    [AvaloniaFact]
    public void DrawLoupe_チェッカーサイズ1未満でも動作する()
    {
        var result = LoupeRenderer.DrawLoupe(
            null, CreateRequest(50, 4, CreateAppearance(DrawingColor.Red, 0, Transparent, Transparent, DrawingColor.Black, 1)));

        Assert.NotNull(result);
        Assert.Equal(50, result.Size.Width);
    }

    [AvaloniaFact]
    public void DrawLoupe_小さいルーペサイズでも動作する()
    {
        using var src = CreateBitmap(4);

        var result = LoupeRenderer.DrawLoupe(
            src, CreateRequest(8, 2,
                CreateAppearance(DrawingColor.Red, 2, DrawingColor.White, DrawingColor.LightGray, DrawingColor.Black, 1),
                new EsPoint(2, 2)));

        Assert.NotNull(result);
        Assert.Equal(8, result.Size.Width);
        Assert.Equal(8, result.Size.Height);
    }

    [AvaloniaFact]
    public void DrawLoupe_色が透明でも動作する()
    {
        using var src = CreateBitmap(10);

        // すべての色が透明（アルファ 0）でも例外なく描画する（十字線・市松・外枠をスキップ）
        var result = LoupeRenderer.DrawLoupe(
            src, CreateRequest(64, 4, CreateAppearance(Transparent, 6, Transparent, Transparent, Transparent, 2), new EsPoint(5, 5)));

        Assert.NotNull(result);
        Assert.Equal(64, result.Size.Width);
    }

    [AvaloniaFact]
    public void DrawLoupe_色が透明で切り出しもできない場合でも動作する()
    {
        var result = LoupeRenderer.DrawLoupe(
            null, CreateRequest(50, 4, CreateAppearance(Transparent, 6, Transparent, Transparent, Transparent, 2)));

        Assert.NotNull(result);
        Assert.Equal(50, result.Size.Width);
    }

    // ====================================================================
    // LoupeDecorationRenderer（十字線・外枠の描画可否）
    // ====================================================================

    /// <summary>十字線を描画する（描画可否はコアの <see cref="LoupeAppearance.HasCrosshair"/> が所有する）。</summary>
    /// <param name="crossColor">十字線の色。</param>
    /// <remarks>透明色（描画しない指定）と有色の両経路を通す（可否の判断はコアにあるため、本テストは描画層の分岐を実行する）。</remarks>
    [AvaloniaTheory]
    [InlineData(0, 255)]
    [InlineData(255, 255)]
    public void DrawCrosshair_色の有無で描画可否が決まる(int crossAlpha, int frameAlpha)
    {
        using var ctx = CreateContext();
        var appearance = CreateAppearance(
            DrawingColor.FromArgb(crossAlpha, 255, 0, 0), 6, Transparent, Transparent,
            DrawingColor.FromArgb(frameAlpha, 0, 0, 0), 2);

        // 例外なく完了する（透明色は描画せず早期に戻る）
        LoupeDecorationRenderer.DrawCrosshair(ctx, CreateRequest(100, 4, appearance, new EsPoint(50, 50)));
    }

    /// <summary>外枠を描画する（描画可否はコアの <see cref="LoupeAppearance.HasFrame"/> が所有する）。</summary>
    /// <param name="alpha">外枠の色のアルファ。</param>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(255)]
    public void DrawFrame_色の有無で描画可否が決まる(int alpha)
    {
        using var ctx = CreateContext();
        var appearance = CreateAppearance(
            Transparent, 6, Transparent, Transparent, DrawingColor.FromArgb(alpha, 0, 0, 0), 2);

        // 例外なく完了する（透明色は描画せず早期に戻る）
        LoupeDecorationRenderer.DrawFrame(ctx, CreateRequest(100, 4, appearance, new EsPoint(50, 50)));
    }

    /// <summary>テスト用の DrawingContext を生成する。</summary>
    private static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(
            new PixelSize(160, 160), new Vector(DpiScaler.StandardDpi, DpiScaler.StandardDpi));
        return rtb.CreateDrawingContext();
    }
}

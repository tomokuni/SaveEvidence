using EsUtil.Platform.Core.Geometry;
using Estable.Marquee.Decoration;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Marquee.Tests;

/// <summary><see cref="MarqueeOuterSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// 対象: <c>MarqueeOuterSkiaRenderer.cs</c>（フレームの描画のみ）。<br/>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 「どの矩形を暗転するか」「どのラベルを描くか」の判断はコアの <c>OuterDecoFrame</c> が単一所有するため、<br/>
/// 本テストは「与えられたフレームを描く」ことと、UI 固有のフォント解決のみを検証する。
/// </remarks>
public class MarqueeOuterSkiaRendererTests
{
    /// <summary>描画に使う暗転色（赤 50%）。</summary>
    private static readonly DrawingColor s_overlay = DrawingColor.FromArgb(128, 255, 0, 0);

    /// <summary>描画に使う枠線色（青・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>暗転を半透明で塗った結果の色（赤 50% を透明背景へ重ねた色。SKColor の引数順は R,G,B,A）。</summary>
    private static readonly SKColor s_overlayPixel = new(255, 0, 0, 128);

    /// <summary>テスト用のフレームを生成する。</summary>
    /// <param name="overlayRegions">暗転領域。null の場合は空。</param>
    /// <param name="borderRects">枠線の矩形。null の場合は空。</param>
    /// <param name="labelAnchor">ラベルの基準矩形。</param>
    /// <param name="sizeLabelText">サイズラベルの文言。</param>
    /// <param name="subLines">2 行目以降の行。</param>
    /// <param name="sizeLabelBackgroundColor">サイズラベルの背景色。null の場合は黒。</param>
    /// <returns>生成したフレーム。</returns>
    private static OuterDecoFrame CreateFrame(
        IReadOnlyList<EsRect>? overlayRegions = null,
        IReadOnlyList<EsRect>? borderRects = null,
        EsRect? labelAnchor = null,
        string? sizeLabelText = null,
        IReadOnlyList<OuterDecoSubLine>? subLines = null,
        DrawingColor? sizeLabelBackgroundColor = null)
        => new(
            overlayRegions ?? [],
            s_overlay,
            borderRects ?? [],
            s_border,
            labelAnchor,
            sizeLabelText,
            new OuterDecoLabelStyle(
                14.0, DrawingColor.White, sizeLabelBackgroundColor ?? DrawingColor.Black),
            subLines ?? []);

    /// <summary>暗転領域を指定色で塗ることを検証する。</summary>
    [Fact]
    public void Draw_暗転領域を塗る()
    {
        using var target = TestHelper.CreateTarget(100, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            overlayRegions: [new EsRect(0, 0, 100, 30), new EsRect(0, 30, 20, 70)]));

        Assert.Equal(s_overlayPixel, target.GetPixel(50, 10));
        Assert.Equal(s_overlayPixel, target.GetPixel(10, 50));
        // 指定していない領域は透明のまま
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(50, 50));
    }

    /// <summary>暗転領域が空の場合は何も塗らないことを検証する。</summary>
    [Fact]
    public void Draw_暗転領域が空なら何も塗らない()
    {
        using var target = TestHelper.CreateTarget(50, 50);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame());

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(25, 25));
    }

    /// <summary>枠線の矩形を指定色で描くことを検証する。</summary>
    [Fact]
    public void Draw_枠線の矩形を描く()
    {
        using var target = TestHelper.CreateTarget(100, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            borderRects: [new EsRect(20, 30, 40, 2)]));

        Assert.Equal(SKColors.Blue, target.GetPixel(40, 30));
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(40, 50));
    }

    /// <summary>サイズラベルの背景を基準矩形の左上（内側余白の位置）に描くことを検証する。</summary>
    [Fact]
    public void Draw_サイズラベルの背景を基準矩形の左上に描く()
    {
        using var target = TestHelper.CreateTarget(200, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            labelAnchor: new EsRect(10, 10, 180, 80), sizeLabelText: "ABC"));

        // 背景は基準矩形の上端 + 内側余白から始まる（左上は透明のまま）
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(10, 10));
        Assert.Equal(SKColors.Black, target.GetPixel(12, 16));
        // 基準矩形の右下側は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(180, 80));
    }

    /// <summary>サイズラベルの文言が null の場合は描画しないことを検証する。</summary>
    /// <remarks>コアの <c>OuterDecoFrame</c> は「null 以外なら描く」文言を渡す（空文字は渡らない）。</remarks>
    [Fact]
    public void Draw_サイズラベルの文言がnullなら描画しない()
    {
        using var target = TestHelper.CreateTarget(200, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            labelAnchor: new EsRect(10, 10, 180, 80), sizeLabelText: null));

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(12, 16));
    }

    /// <summary>サイズラベルの文言が無い場合はサブラインを 1 行目から描画することを検証する。</summary>
    [Fact]
    public void Draw_サイズラベル文言なしならサブラインを繰り上げる()
    {
        using var withSize = TestHelper.CreateTarget(200, 100);
        using var withoutSize = TestHelper.CreateTarget(200, 100);
        var anchor = new EsRect(10, 10, 180, 80);
        var subLines = new[] { new OuterDecoSubLine("SUB", 12, DrawingColor.White, DrawingColor.Blue) };

        MarqueeOuterSkiaRenderer.Draw(withSize.Canvas, CreateFrame(labelAnchor: anchor, sizeLabelText: "ABC", subLines: subLines));
        MarqueeOuterSkiaRenderer.Draw(withoutSize.Canvas, CreateFrame(labelAnchor: anchor, sizeLabelText: null, subLines: subLines));

        // 文言なし側は 1 行目の位置にサブラインの背景が来る
        Assert.Equal(SKColors.Blue, withoutSize.GetPixel(12, 16));
        // 文言あり側の 1 行目はサイズラベルの背景
        Assert.Equal(SKColors.Black, withSize.GetPixel(12, 16));
        // 文言なし側の方が背景の総高が小さい（サイズラベルの 1 行分だけ短い）
        Assert.True(CountNonTransparentRows(withoutSize) < CountNonTransparentRows(withSize));
    }

    /// <summary>複数のサブラインを上から順に縦へ積むことを検証する。</summary>
    [Fact]
    public void Draw_複数のサブラインを縦に積む()
    {
        using var target = TestHelper.CreateTarget(200, 200);
        var subLines = new[]
        {
            new OuterDecoSubLine("ONE", 12, DrawingColor.White, DrawingColor.Blue),
            new OuterDecoSubLine("TWO", 12, DrawingColor.White, DrawingColor.Lime),
        };

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            labelAnchor: new EsRect(10, 10, 180, 180), sizeLabelText: null, subLines: subLines));

        var firstBlueY = FindFirstColorY(target, SKColors.Blue);
        var firstLimeY = FindFirstColorY(target, SKColors.Lime);
        Assert.True(firstBlueY >= 0);
        Assert.True(firstLimeY > firstBlueY);
    }

    /// <summary>ラベルの基準矩形が無い場合はラベルを描画しないことを検証する。</summary>
    [Fact]
    public void Draw_ラベル基準がなければラベルを描画しない()
    {
        using var target = TestHelper.CreateTarget(200, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            labelAnchor: null,
            sizeLabelText: "ABC",
            subLines: [new OuterDecoSubLine("SUB", 12, DrawingColor.White, DrawingColor.Blue)]));

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(12, 16));
    }

    /// <summary>指定した背景色（半透明）がそのまま描かれることを検証する。</summary>
    /// <remarks>背景色の実効色（アルファ 0 を黒 50% とする規則）の解決はコアが行う。</remarks>
    [Fact]
    public void Draw_指定した背景色をそのまま描く()
    {
        using var target = TestHelper.CreateTarget(200, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            labelAnchor: new EsRect(10, 10, 180, 80), sizeLabelText: "ABC",
            sizeLabelBackgroundColor: DrawingColor.FromArgb(128, 0, 0, 255)));

        Assert.Equal(new SKColor(0, 0, 255, 128), target.GetPixel(12, 16));
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Draw_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => MarqueeOuterSkiaRenderer.Draw(null!, CreateFrame()));
    }

    /// <summary>ASCII のみの文字列を判定できることを検証する。</summary>
    /// <param name="text">判定する文字列。</param>
    /// <param name="expected">ASCII のみと判定される期待値。</param>
    [Theory]
    [InlineData("", true)]
    [InlineData("ABC 123 (100%)", true)]
    [InlineData("ABC\u007F", true)]
    [InlineData("ABC\u0080", false)]
    [InlineData("ダブルクリック: 確定", false)]
    [InlineData("Aあ", false)]
    public void IsAsciiOnly_文字種を判定する(string text, bool expected)
    {
        Assert.Equal(expected, MarqueeOuterSkiaRenderer.IsAsciiOnly(text));
    }

    /// <summary>ASCII のみのテキストは既定のフォントで描画することを検証する。</summary>
    [Fact]
    public void ResolveTypeface_ASCIIのみなら既定のフォントを使う()
    {
        Assert.Same(SKTypeface.Default, MarqueeOuterSkiaRenderer.ResolveTypeface("(10, 20) - (30, 40)"));
    }

    /// <summary>日本語を含むテキストは字形を持つフォントへフォールバックすることを検証する。</summary>
    [Fact]
    public void ResolveTypeface_日本語なら字形を持つフォントへフォールバックする()
    {
        // CJK のフォントが無い環境では既定のフォントへ戻る（描画結果は豆腐になる）
        Assert.SkipUnless(
            SKFontManager.Default.MatchCharacter('あ') is not null, "日本語のフォントがインストールされていない");

        var typeface = MarqueeOuterSkiaRenderer.ResolveTypeface("確定");

        Assert.NotSame(SKTypeface.Default, typeface);
        Assert.NotEqual(0, typeface.GetGlyph('確'));
        Assert.NotEqual(0, typeface.GetGlyph('定'));
    }

    /// <summary>指定色の画素が現れる最小の Y 座標を探す。</summary>
    /// <param name="target">探索する描画先。</param>
    /// <param name="color">探す色。</param>
    /// <returns>見つかった Y 座標。見つからない場合は -1。</returns>
    private static int FindFirstColorY(RenderTarget target, SKColor color)
    {
        for (var y = 0; y < 200; y++)
        {
            if (target.CountColor(0, y, 200, y + 1, color) > 0)
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>透明でない画素を含む行の数を数える。</summary>
    /// <param name="target">探索する描画先。</param>
    /// <returns>描画された行数。</returns>
    private static int CountNonTransparentRows(RenderTarget target)
    {
        var rows = 0;
        for (var y = 0; y < 100; y++)
        {
            if (target.CountColor(0, y, 200, y + 1, new SKColor(0, 0, 0, 0)) < 200)
            {
                rows++;
            }
        }

        return rows;
    }
}

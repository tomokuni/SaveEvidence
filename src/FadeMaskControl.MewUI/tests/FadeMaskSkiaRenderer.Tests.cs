using Estable.Platform.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 幾何計算はコアの <c>FadeMaskOverlayGeometry</c> が単一所有するため、本テストは描画結果を検証する。
/// </remarks>
public class FadeMaskSkiaRendererTests
{
    /// <summary>検証に使う暗転色（赤 50%）。</summary>
    private static readonly DrawingColor s_overlay = DrawingColor.FromArgb(128, 255, 0, 0);

    /// <summary>検証に使う枠線色（青・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>暗転を半透明で塗った結果の色（赤 50% を透明背景へ重ねた色。SKColor の引数順は R,G,B,A）。</summary>
    private static readonly SKColor s_overlayPixel = new(255, 0, 0, 128);

    /// <summary>穴あけ矩形がある場合、穴の内側を暗転せず枠線のみを描くことを検証する。</summary>
    [Fact]
    public void Render_穴あけ矩形の内側は暗転せず枠線を描く()
    {
        using var target = TestHelper.CreateTarget(100, 100);
        var client = new EsRect(0, 0, 100, 100);
        var hole = new EsRect(20, 30, 40, 50);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, hole, s_overlay, s_border, borderThickness: 2, showFullOverlay: false);

        // 穴の内側（枠線を除く中央）は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(40, 55));
        // 穴の上辺は枠線で描かれる
        Assert.Equal(SKColors.Blue, target.GetPixel(40, 30));
        // 穴の外側は暗転される
        Assert.Equal(s_overlayPixel, target.GetPixel(10, 10));
        Assert.Equal(s_overlayPixel, target.GetPixel(50, 90));
        Assert.Equal(s_overlayPixel, target.GetPixel(5, 55));
        Assert.Equal(s_overlayPixel, target.GetPixel(95, 55));
    }

    /// <summary>穴あけ矩形がなく全面暗転が有効なら全面を暗転することを検証する。</summary>
    [Fact]
    public void Render_穴あけ矩形がなく全面暗転なら暗転する()
    {
        using var target = TestHelper.CreateTarget(50, 50);
        var client = new EsRect(0, 0, 50, 50);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, null, s_overlay, s_border, borderThickness: 2, showFullOverlay: true);

        Assert.Equal(s_overlayPixel, target.GetPixel(0, 0));
        Assert.Equal(s_overlayPixel, target.GetPixel(25, 25));
        Assert.Equal(s_overlayPixel, target.GetPixel(49, 49));
    }

    /// <summary>穴あけ矩形がなく全面暗転も無効なら何も描画しないことを検証する。</summary>
    [Fact]
    public void Render_穴あけも全面暗転も無効なら描画しない()
    {
        using var target = TestHelper.CreateTarget(50, 50);
        var client = new EsRect(0, 0, 50, 50);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, null, s_overlay, s_border, borderThickness: 2, showFullOverlay: false);

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(25, 25));
    }

    /// <summary>全面暗転が有効なら穴あけ矩形がなくても暗転することを検証する。</summary>
    [Fact]
    public void Render_全面暗転が有効なら暗転する()
    {
        using var target = TestHelper.CreateTarget(50, 50);
        var client = new EsRect(0, 0, 50, 50);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, null, s_overlay, s_border, borderThickness: 2, showFullOverlay: true);

        Assert.Equal(s_overlayPixel, target.GetPixel(25, 25));
    }

    /// <summary>枠線の色のアルファが 0 なら枠線を描画しないことを検証する。</summary>
    [Fact]
    public void Render_枠線のアルファが0なら枠線を描画しない()
    {
        using var target = TestHelper.CreateTarget(100, 100);
        var client = new EsRect(0, 0, 100, 100);
        var hole = new EsRect(20, 30, 40, 50);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, hole, s_overlay, DrawingColor.FromArgb(0, 0, 0, 255),
            borderThickness: 2, showFullOverlay: false);

        // 穴の上辺に枠線が出ない（暗転のみ）
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(40, 30));
    }

    /// <summary>暗転の色のアルファが 0 なら暗転を描画しないことを検証する。</summary>
    [Fact]
    public void Render_暗転のアルファが0なら暗転を描画しない()
    {
        using var target = TestHelper.CreateTarget(100, 100);
        var client = new EsRect(0, 0, 100, 100);
        var hole = new EsRect(20, 30, 40, 50);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, hole, DrawingColor.FromArgb(0, 0, 0, 0), s_border,
            borderThickness: 2, showFullOverlay: false);

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(10, 10));
        Assert.Equal(SKColors.Blue, target.GetPixel(40, 30));
    }

    /// <summary>穴あけ矩形がクライアント外の場合は全面を暗転することを検証する。</summary>
    [Fact]
    public void Render_穴あけ矩形が外側なら全面を暗転する()
    {
        using var target = TestHelper.CreateTarget(50, 50);
        var client = new EsRect(0, 0, 50, 50);
        var hole = new EsRect(200, 200, 10, 10);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, hole, s_overlay, s_border, borderThickness: 2, showFullOverlay: false);

        Assert.Equal(s_overlayPixel, target.GetPixel(25, 25));
    }

    /// <summary>枠線の太さが 0.5 未満でも描画されることを検証する。</summary>
    [Fact]
    public void Render_枠線の太さが小さくても描画される()
    {
        using var target = TestHelper.CreateTarget(100, 100);
        var client = new EsRect(0, 0, 100, 100);
        var hole = new EsRect(20, 30, 40, 50);

        FadeMaskSkiaRenderer.Render(
            target.Canvas, client, hole, s_overlay, s_border, borderThickness: 0.1, showFullOverlay: false);

        Assert.True(target.CountColor(0, 0, 100, 100, SKColors.Blue) > 0);
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Render_キャンバスがnullなら例外()
    {
        var client = new EsRect(0, 0, 10, 10);

        Assert.Throws<ArgumentNullException>(
            () => FadeMaskSkiaRenderer.Render(null!, client, null, s_overlay, s_border, 2, true));
    }

    /// <summary>キャンバスが null の場合はラベル描画でも例外になることを検証する。</summary>
    [Fact]
    public void DrawLabels_キャンバスがnullなら例外()
    {
        var anchor = new EsRect(0, 0, 100, 100);

        Assert.Throws<ArgumentNullException>(
            () => FadeMaskSkiaRenderer.DrawLabels(
                null!, anchor, true, "ABC", 14, DrawingColor.White, DrawingColor.Black, null));
    }

    /// <summary>サイズラベルのテキストを指定すると、基準矩形の左上に不透明な背景が描かれることを検証する。</summary>
    [Fact]
    public void DrawLabels_サイズラベルの背景を基準矩形の左上に描く()
    {
        using var target = TestHelper.CreateTarget(200, 100);

        FadeMaskSkiaRenderer.DrawLabels(
            target.Canvas, new EsRect(10, 10, 180, 80), showSizeLabel: true, "ABC", 14,
            DrawingColor.White, DrawingColor.Black, subLines: null);

        // 背景は基準矩形の上端 + 内側余白から始まる（左上は透明のまま）
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(10, 10));
        Assert.Equal(SKColors.Black, target.GetPixel(12, 16));
        // 基準矩形の右下側は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(180, 80));
    }

    /// <summary>背景色のアルファが 0 の場合は黒 50% で描画されることを検証する。</summary>
    [Fact]
    public void DrawLabels_背景色のアルファが0なら黒50パーセントで描く()
    {
        using var target = TestHelper.CreateTarget(200, 100);

        FadeMaskSkiaRenderer.DrawLabels(
            target.Canvas, new EsRect(10, 10, 180, 80), showSizeLabel: true, "ABC", 14,
            DrawingColor.White, DrawingColor.FromArgb(0, 0, 0, 0), subLines: null);

        Assert.Equal(new SKColor(0, 0, 0, 128), target.GetPixel(12, 16));
    }

    /// <summary>サイズラベルを非表示にすると、サブラインが 1 行目から描かれることを検証する。</summary>
    [Fact]
    public void DrawLabels_サイズラベル非表示ならサブラインを繰り上げる()
    {
        using var withSize = TestHelper.CreateTarget(200, 100);
        using var withoutSize = TestHelper.CreateTarget(200, 100);
        var anchor = new EsRect(10, 10, 180, 80);
        var subLines = new[] { new FadeMaskSubLine("SUB", 12, DrawingColor.White, DrawingColor.Blue) };

        FadeMaskSkiaRenderer.DrawLabels(
            withSize.Canvas, anchor, true, "ABC", 14, DrawingColor.White, DrawingColor.Black, subLines);
        FadeMaskSkiaRenderer.DrawLabels(
            withoutSize.Canvas, anchor, false, "ABC", 14, DrawingColor.White, DrawingColor.Black, subLines);

        // 非表示側は 1 行目の位置にサブラインの背景が来る
        Assert.Equal(SKColors.Blue, withoutSize.GetPixel(12, 16));
        // 表示側の 1 行目はサイズラベルの背景
        Assert.Equal(SKColors.Black, withSize.GetPixel(12, 16));
        // 非表示側の方が背景の総高が小さい（サイズラベルの 1 行分だけ短い）
        Assert.True(
            CountNonTransparentRows(withoutSize) < CountNonTransparentRows(withSize));
    }

    /// <summary>サイズラベルのテキストが null または空の場合はサイズラベルを描画しないことを検証する。</summary>
    /// <param name="text">サイズラベルのテキスト（null または空）。</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DrawLabels_サイズラベルのテキストが未指定なら描画しない(string? text)
    {
        using var target = TestHelper.CreateTarget(200, 100);

        FadeMaskSkiaRenderer.DrawLabels(
            target.Canvas, new EsRect(10, 10, 180, 80), showSizeLabel: true, text, 14,
            DrawingColor.White, DrawingColor.Black, subLines: null);

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(12, 16));
    }

    /// <summary>複数のサブラインを指定すると、上から順に縦へ積まれることを検証する。</summary>
    [Fact]
    public void DrawLabels_複数のサブラインを縦に積む()
    {
        using var target = TestHelper.CreateTarget(200, 200);
        var subLines = new[]
        {
            new FadeMaskSubLine("ONE", 12, DrawingColor.White, DrawingColor.Blue),
            new FadeMaskSubLine("TWO", 12, DrawingColor.White, DrawingColor.Lime),
        };

        FadeMaskSkiaRenderer.DrawLabels(
            target.Canvas, new EsRect(10, 10, 180, 180), false, null, 14,
            DrawingColor.White, DrawingColor.Black, subLines);

        // 1 行目の色より下に 2 行目の色が現れる
        var firstBlueY = FindFirstColorY(target, SKColors.Blue);
        var firstLimeY = FindFirstColorY(target, SKColors.Lime);
        Assert.True(firstBlueY >= 0);
        Assert.True(firstLimeY > firstBlueY);
    }

    /// <summary>サブラインが null または空の場合はサイズラベルのみを描画することを検証する。</summary>
    /// <param name="lineCount">サブラインの件数（null または 0 件）。</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void DrawLabels_サブラインが無効ならサイズラベルのみ描画する(int lineCount)
    {
        using var target = TestHelper.CreateTarget(200, 100);
        IReadOnlyList<FadeMaskSubLine>? subLines = lineCount < 0 ? null : [];

        FadeMaskSkiaRenderer.DrawLabels(
            target.Canvas, new EsRect(10, 10, 180, 80), true, "ABC", 14,
            DrawingColor.White, DrawingColor.Black, subLines);

        Assert.Equal(SKColors.Black, target.GetPixel(12, 16));
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
        Assert.Equal(expected, FadeMaskSkiaRenderer.IsAsciiOnly(text));
    }

    /// <summary>ASCII のみのテキストは既定のフォントで描画することを検証する。</summary>
    [Fact]
    public void ResolveTypeface_ASCIIのみなら既定のフォントを使う()
    {
        Assert.Same(SKTypeface.Default, FadeMaskSkiaRenderer.ResolveTypeface("(10, 20) - (30, 40)"));
    }

    /// <summary>日本語を含むテキストは字形を持つフォントへフォールバックすることを検証する。</summary>
    [Fact]
    public void ResolveTypeface_日本語なら字形を持つフォントへフォールバックする()
    {
        // CJK のフォントが無い環境では既定のフォントへ戻る（描画結果は豆腐になる）
        Assert.SkipUnless(
            SKFontManager.Default.MatchCharacter('あ') is not null, "日本語のフォントがインストールされていない");

        var typeface = FadeMaskSkiaRenderer.ResolveTypeface("確定");

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

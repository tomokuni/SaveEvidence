using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.MarqueeCore.Drawing;

namespace EsUtil.SaveEvidence.Marquee.UIBase.Tests;

/// <summary><see cref="MarqueeLabelPlan"/> の単体テスト（実寸からラベル要素の配置を解決する）。</summary>
/// <remarks>
/// 文言・位置はコアが、色・フォントサイズは描画の設定が単一所有するため、本テストは「実寸の反映」と「要素への写像」だけを検証する<br/>
/// （配置の計算そのものは <c>EsLabelLayout.ComputeAlignedBox</c> のテストが担う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueeLabelPlanTests
{
    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize MeasuredSize = new(42.0, 14.0);

    /// <summary>テスト用の描画内容を生成する（装飾・ハンドル・マーカーは本テストの対象外のため既定値）。</summary>
    /// <param name="sizeLabel">サイズラベルのテキスト配置。無い場合は null。</param>
    /// <param name="coordLabel">カーソル座標ラベルのテキスト配置。無い場合は null。</param>
    /// <returns>描画内容。</returns>
    private static MarqueeDrawContent CreateContent(
        MarqueeTextPlacement? sizeLabel = null, MarqueeTextPlacement? coordLabel = null)
        => new(default, default, null, null, null, sizeLabel, coordLabel, []);

    /// <summary>テスト用のサイズラベルのテキスト配置を生成する（穴あけ矩形の左上へ置く）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>テキスト配置。</returns>
    private static MarqueeTextPlacement CreateSizeLabel(string text = "(10, 20) - (30, 40),  (20×20)")
        => new(text, new EsPoint(10, 20), EsLabelAlignX.Left, EsLabelAlignY.Top, default);

    /// <summary>テスト用のカーソル座標ラベルのテキスト配置を生成する（カーソル + オフセットへ置き、領域内へクランプする）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>テキスト配置。</returns>
    private static MarqueeTextPlacement CreateCoordLabel(string text = "(50, 60)")
        => new(text, new EsPoint(58, 68), EsLabelAlignX.Left, EsLabelAlignY.Top, new EsSize(200, 200));

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => MeasuredSize;

    /// <summary>テスト用の描画に使う設定を生成する（本テストは色とフォントサイズの写像を検証する）。</summary>
    /// <returns>描画に使う設定。</returns>
    private static MarqueePaint CreatePaint()
        => new()
        {
            SizeLabelColor = DrawingColor.White,
            SizeLabelBackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0),
            SizeLabelFontSize = 14.0,
            CoordLabelColor = DrawingColor.Yellow,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0),
            CoordLabelFontSize = 12.0,
        };

    [Fact]
    public void Resolve_ラベルが無ければ空を返す()
    {
        var elements = MarqueeLabelPlan.Resolve(CreateContent(), CreatePaint(), Measure);

        Assert.Empty(elements);
    }

    [Fact]
    public void Resolve_サイズラベルのみなら1件を返す()
    {
        var elements = MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel("サイズ")), CreatePaint(), Measure);

        var element = Assert.Single(elements);
        Assert.Equal("サイズ", element.Text);
        Assert.Equal(14.0, element.FontSize);
    }

    [Fact]
    public void Resolve_カーソル座標ラベルのみなら1件を返す()
    {
        var elements = MarqueeLabelPlan.Resolve(CreateContent(coordLabel: CreateCoordLabel("(1, 2)")), CreatePaint(), Measure);

        var element = Assert.Single(elements);
        Assert.Equal("(1, 2)", element.Text);
        Assert.Equal(12.0, element.FontSize);
    }

    [Fact]
    public void Resolve_両方ある場合はサイズラベルが先で座標ラベルが後になる()
    {
        // 順序は本クラスが単一所有する（座標ラベルが手前 = 最後）
        var elements = MarqueeLabelPlan.Resolve(
            CreateContent(sizeLabel: CreateSizeLabel("サイズ"), coordLabel: CreateCoordLabel()), CreatePaint(), Measure);

        Assert.Equal(2, elements.Count);
        Assert.Equal("サイズ", elements[0].Text);
        Assert.Equal("(50, 60)", elements[1].Text);
    }

    [Fact]
    public void Resolve_基準点を背景の左上に置く()
    {
        var element = Assert.Single(MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel()), CreatePaint(), Measure));

        // 基準点 (10, 20) がアライン（左端・上端）どおり背景の左上になる
        Assert.Equal(new EsPoint(10, 20), element.Bounds.TopLeft);
        Assert.Equal(MeasuredSize.Width + EsLabelLayout.Padding * 2, element.Bounds.Width);
        Assert.Equal(MeasuredSize.Height + EsLabelLayout.Padding * 2, element.Bounds.Height);
    }

    [Fact]
    public void Resolve_領域をはみ出す場合はクランプする()
    {
        // クランプ領域 (200, 200) へ収まらない位置を指定する
        var label = new MarqueeTextPlacement(
            "(50, 60)", new EsPoint(190, 195), EsLabelAlignX.Left, EsLabelAlignY.Top, new EsSize(200, 200));

        var element = Assert.Single(MarqueeLabelPlan.Resolve(CreateContent(coordLabel: label), CreatePaint(), Measure));

        Assert.Equal(200 - (MeasuredSize.Width + EsLabelLayout.Padding * 2), element.Bounds.X);
        Assert.Equal(200 - (MeasuredSize.Height + EsLabelLayout.Padding * 2), element.Bounds.Y);
    }

    [Fact]
    public void Resolve_内側余白は背景矩形と文字原点の差になる()
    {
        var element = Assert.Single(MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel()), CreatePaint(), Measure));

        Assert.Equal(element.TextOrigin.X - element.Bounds.X, element.PaddingX);
        Assert.Equal(element.TextOrigin.Y - element.Bounds.Y, element.PaddingY);
    }

    [Fact]
    public void Resolve_文言とフォントサイズを測定関数へ渡す()
    {
        var calls = new List<(string Text, double FontSize)>();

        MarqueeLabelPlan.Resolve(
            CreateContent(sizeLabel: CreateSizeLabel("サイズ"), coordLabel: CreateCoordLabel("(7, 8)")),
            CreatePaint(),
            (text, fontSize) =>
            {
                calls.Add((text, fontSize));
                return MeasuredSize;
            });

        Assert.Equal([("サイズ", 14.0), ("(7, 8)", 12.0)], calls);
    }

    [Fact]
    public void Resolve_色と角丸を要素へ写す()
    {
        var element = Assert.Single(MarqueeLabelPlan.Resolve(CreateContent(coordLabel: CreateCoordLabel()), CreatePaint(), Measure));

        // 色は描画の設定（本層が単一所有する）から写す
        Assert.Equal(DrawingColor.Yellow, element.TextColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), element.BackgroundColor);
        // サイズラベル・カーソル座標ラベルはいずれも角丸なしの矩形である
        Assert.Equal(0, element.CornerRadius);
    }

    [Fact]
    public void Resolve_測定関数がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel()), CreatePaint(), null!));
    }

    [Fact]
    public void Resolve_色がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel()), null!, Measure));
    }
}

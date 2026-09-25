using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Marquee.UIBase.Tests;

/// <summary><see cref="MarqueeLabelPlan"/> の単体テスト（実寸からラベル要素の配置を解決する）。</summary>
/// <remarks>
/// 文言・外観・順序はコアが単一所有するため、本テストは「実寸の反映」と「要素への写像」だけを検証する<br/>
/// （配置の計算そのものはコアの <c>MarqueeSizeLabel.ComputeFrame</c> / <c>MarqueeCoordLabel.ComputeFrame</c> のテストが担う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueeLabelPlanTests
{
    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize MeasuredSize = new(42.0, 14.0);

    /// <summary>テスト用の描画内容を生成する（装飾・ハンドル・マーカーは本テストの対象外のため既定値）。</summary>
    /// <param name="sizeLabel">サイズラベル。無い場合は null。</param>
    /// <param name="coordLabel">カーソル座標ラベル。無い場合は null。</param>
    /// <returns>描画内容。</returns>
    private static MarqueeDrawContent CreateContent(MarqueeSizeLabel? sizeLabel = null, MarqueeCoordLabel? coordLabel = null)
        => new(default, default, null, null, null, sizeLabel, coordLabel, []);

    /// <summary>テスト用のサイズラベルを生成する。</summary>
    /// <param name="text">文言。</param>
    /// <returns>サイズラベル。</returns>
    private static MarqueeSizeLabel CreateSizeLabel(string text = "(10, 20) - (30, 40),  (20×20)")
        => new(text, new EsPoint(10, 20), 14.0, DrawingColor.White, DrawingColor.FromArgb(153, 0, 0, 0));

    /// <summary>テスト用のカーソル座標ラベルを生成する。</summary>
    /// <param name="text">文言。</param>
    /// <returns>カーソル座標ラベル。</returns>
    private static MarqueeCoordLabel CreateCoordLabel(string text = "(50, 60)")
        => new(text, new EsPoint(50, 60), new EsSize(200, 200), 12.0, DrawingColor.Yellow, DrawingColor.FromArgb(153, 0, 0, 0));

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => MeasuredSize;

    [Fact]
    public void Resolve_ラベルが無ければ空を返す()
    {
        var elements = MarqueeLabelPlan.Resolve(CreateContent(), Measure);

        Assert.Empty(elements);
    }

    [Fact]
    public void Resolve_サイズラベルのみなら1件を返す()
    {
        var elements = MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel("サイズ")), Measure);

        var element = Assert.Single(elements);
        Assert.Equal("サイズ", element.Text);
        Assert.Equal(14.0, element.FontSize);
    }

    [Fact]
    public void Resolve_カーソル座標ラベルのみなら1件を返す()
    {
        var elements = MarqueeLabelPlan.Resolve(CreateContent(coordLabel: CreateCoordLabel("(1, 2)")), Measure);

        var element = Assert.Single(elements);
        Assert.Equal("(1, 2)", element.Text);
        Assert.Equal(12.0, element.FontSize);
    }

    [Fact]
    public void Resolve_両方ある場合はサイズラベルが先で座標ラベルが後になる()
    {
        // 順序はコアが単一所有する（座標ラベルが手前 = 最後）
        var elements = MarqueeLabelPlan.Resolve(
            CreateContent(sizeLabel: CreateSizeLabel("サイズ"), coordLabel: CreateCoordLabel()), Measure);

        Assert.Equal(2, elements.Count);
        Assert.Equal("サイズ", elements[0].Text);
        Assert.Equal("(50, 60)", elements[1].Text);
    }

    [Fact]
    public void Resolve_配置はコアの計算結果をそのまま使う()
    {
        var sizeLabel = CreateSizeLabel();

        var element = Assert.Single(MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: sizeLabel), Measure));
        var frame = sizeLabel.ComputeFrame(MeasuredSize);

        // 背景矩形・文字の描画原点はコアの ComputeFrame が単一所有する（本クラスは写すだけ）
        Assert.Equal(frame.Background, element.Bounds);
        Assert.Equal(frame.TextOrigin, element.TextOrigin);
    }

    [Fact]
    public void Resolve_内側余白は背景矩形と文字原点の差になる()
    {
        var element = Assert.Single(MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel()), Measure));

        Assert.Equal(element.TextOrigin.X - element.Bounds.X, element.PaddingX);
        Assert.Equal(element.TextOrigin.Y - element.Bounds.Y, element.PaddingY);
    }

    [Fact]
    public void Resolve_文言とフォントサイズを測定関数へ渡す()
    {
        var calls = new List<(string Text, double FontSize)>();

        MarqueeLabelPlan.Resolve(
            CreateContent(sizeLabel: CreateSizeLabel("サイズ"), coordLabel: CreateCoordLabel("(7, 8)")),
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
        var element = Assert.Single(MarqueeLabelPlan.Resolve(CreateContent(coordLabel: CreateCoordLabel()), Measure));

        Assert.Equal(DrawingColor.Yellow, element.TextColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), element.BackgroundColor);
        // サイズラベル・カーソル座標ラベルはいずれも角丸なしの矩形である
        Assert.Equal(0, element.CornerRadius);
    }

    [Fact]
    public void Resolve_測定関数がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarqueeLabelPlan.Resolve(CreateContent(sizeLabel: CreateSizeLabel()), null!));
    }
}

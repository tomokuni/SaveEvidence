using EsUtil.Platform.Core.Geometry;
using Estable.Loupe;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Loupe.CoreForControl.Tests;

/// <summary><see cref="LoupeLabelLayoutPlan"/> の単体テスト（実寸からラベル要素の配置を解決する）。</summary>
/// <remarks>
/// 文言・外観・順序はコアが単一所有するため、本テストは「実寸の反映」と「要素への写像」だけを検証する<br/>
/// （配置の計算そのものはコアの <c>LoupeCoordLabel.ComputeFrame</c> のテストが担う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class LoupeLabelLayoutPlanTests
{
    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize MeasuredSize = new(36.0, 14.0);

    /// <summary>テスト用の通知を生成する。</summary>
    /// <param name="coordLabel">中心座標ラベル。無い場合は null。</param>
    /// <returns>描画完了通知。</returns>
    private static LoupeRenderNotification CreateNotification(LoupeCoordLabel? coordLabel)
        => new(new EsPoint(10, 20), 4, coordLabel, []);

    /// <summary>テスト用の中心座標ラベルを生成する。</summary>
    /// <param name="text">文言。</param>
    /// <returns>中心座標ラベル。</returns>
    private static LoupeCoordLabel CreateCoordLabel(string text = "(50, 60)")
        => new(text, 160.0, 12.0, 2.0, DrawingColor.White, DrawingColor.FromArgb(153, 0, 0, 0));

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => MeasuredSize;

    [Fact]
    public void Resolve_ラベルが無ければ空を返す()
    {
        var elements = LoupeLabelLayoutPlan.Resolve(CreateNotification(null), Measure);

        Assert.Empty(elements);
    }

    [Fact]
    public void Resolve_ラベルがあれば1件を返す()
    {
        var element = Assert.Single(LoupeLabelLayoutPlan.Resolve(CreateNotification(CreateCoordLabel()), Measure));

        Assert.Equal("(50, 60)", element.Text);
        Assert.Equal(12.0, element.FontSize);
        Assert.Equal(DrawingColor.White, element.TextColor);
        Assert.Equal(2.0, element.CornerRadius);
    }

    [Fact]
    public void Resolve_配置はコアの計算結果をそのまま使う()
    {
        var coordLabel = CreateCoordLabel();

        var element = Assert.Single(LoupeLabelLayoutPlan.Resolve(CreateNotification(coordLabel), Measure));
        var frame = coordLabel.ComputeFrame(MeasuredSize);

        // 背景矩形・文字の描画原点はコアの ComputeFrame が単一所有する（本クラスは写すだけ）
        Assert.Equal(frame.Background, element.Bounds);
        Assert.Equal(frame.TextOrigin, element.TextOrigin);
    }

    [Fact]
    public void Resolve_文言とフォントサイズを測定関数へ渡す()
    {
        var calls = new List<(string Text, double FontSize)>();

        LoupeLabelLayoutPlan.Resolve(
            CreateNotification(CreateCoordLabel("(7, 8)")),
            (text, fontSize) =>
            {
                calls.Add((text, fontSize));
                return MeasuredSize;
            });

        Assert.Equal([("(7, 8)", 12.0)], calls);
    }

    [Fact]
    public void Resolve_測定関数がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => LoupeLabelLayoutPlan.Resolve(CreateNotification(CreateCoordLabel()), null!));
    }
}

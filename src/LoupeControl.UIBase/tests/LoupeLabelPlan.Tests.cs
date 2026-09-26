using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.SaveEvidence.Loupe.UIBase.Tests;

/// <summary><see cref="LoupeLabelPlan"/> の単体テスト（実寸からラベル要素の配置を解決する）。</summary>
/// <remarks>
/// 文言・位置はコアが、色・フォントサイズ・角丸・内側余白は描画の設定が単一所有するため、<br/>
/// 本テストは「実寸の反映」と「配置指示への写像」だけを検証する。
/// <br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class LoupeLabelPlanTests
{
    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize MeasuredSize = new(36.0, 14.0);

    /// <summary>テスト用の中心座標ラベルのテキスト配置を生成する（ルーペ上端の中央）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>テキスト配置。</returns>
    private static LoupeTextPlacement CreateCoordLabel(string text = "(50, 60)")
        => new(text, new EsPoint(80, 2), EsHorizontalAlign.Center, EsVerticalAlign.Top, default);

    /// <summary>テスト用の描画に使う設定を生成する。</summary>
    /// <returns>描画に使う設定。</returns>
    private static LoupePaint CreatePaint()
        => new()
        {
            CoordLabelColor = DrawingColor.White,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0),
            CoordLabelFontSize = 12.0,
            CoordLabelCornerRadius = 2,
        };

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => MeasuredSize;

    [Fact]
    public void Resolve_ラベルが無ければ空を返す()
    {
        var elements = LoupeLabelPlan.Resolve(null, CreatePaint(), Measure);

        Assert.Empty(elements);
    }

    [Fact]
    public void Resolve_ラベルがあれば1件を返す()
    {
        var element = Assert.Single(LoupeLabelPlan.Resolve(CreateCoordLabel(), CreatePaint(), Measure));

        Assert.Equal("(50, 60)", element.Text);
        Assert.Equal(12.0, element.FontSize);
        Assert.Equal(DrawingColor.White, element.TextColor);
        Assert.Equal(2.0, element.CornerRadius);
    }

    [Fact]
    public void Resolve_基準点を水平中央へ合わせる()
    {
        var element = Assert.Single(LoupeLabelPlan.Resolve(CreateCoordLabel(), CreatePaint(), Measure));

        // 基準点 (80, 2) がアライン（水平中央・上端）どおり背景の位置になる
        var width = MeasuredSize.Width + element.PaddingX * 2;
        Assert.Equal(80 - width / 2, element.Bounds.X);
        Assert.Equal(2, element.Bounds.Y);
    }

    [Fact]
    public void Resolve_上下の内側余白は左右より狭い()
    {
        var element = Assert.Single(LoupeLabelPlan.Resolve(CreateCoordLabel(), CreatePaint(), Measure));

        // ルーペ上端の帯へ収めるため、上下の余白は左右（共通の体裁値）より狭い
        Assert.True(element.PaddingY < element.PaddingX);
        Assert.Equal(MeasuredSize.Height + element.PaddingY * 2, element.Bounds.Height);
    }

    [Fact]
    public void Resolve_文言とフォントサイズを測定関数へ渡す()
    {
        var calls = new List<(string Text, double FontSize)>();

        LoupeLabelPlan.Resolve(
            CreateCoordLabel("(7, 8)"),
            CreatePaint(),
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
        Assert.Throws<ArgumentNullException>(() => LoupeLabelPlan.Resolve(CreateCoordLabel(), CreatePaint(), null!));
    }

    [Fact]
    public void Resolve_色がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => LoupeLabelPlan.Resolve(CreateCoordLabel(), null!, Measure));
    }
}

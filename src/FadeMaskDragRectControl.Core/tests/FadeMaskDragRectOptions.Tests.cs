using EsUtil.Feature.MarqueeCore;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.Feature.LoupeCore;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectOptions"/> の単体テスト（既定値・部分変更）。</summary>
/// <remarks>UI 非依存の値型のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class FadeMaskDragRectOptionsTests
{
    /// <summary>既定値が内包コントロールの既定値と一致することを検証する。</summary>
    [Fact]
    public void 既定値_内包コントロールの既定値を用いる()
    {
        var options = FadeMaskDragRectOptions.Default;

        // 外側の装飾の外観も選択矩形の設定が単一所有する
        Assert.Equal(MarqueeOptions.Default, options.Marquee);
        Assert.Equal(LoupeOptions.Default, options.Loupe);
        // ホスト固有の付加情報（操作・情報ラベル）の外観は専用の値が単一所有する
        Assert.Equal(FadeMaskDragRectLabelOptions.Default, options.Labels);
        Assert.Equal(DrawingColor.White, options.SelectionRectColorA);
        Assert.Equal(DrawingColor.Black, options.SelectionRectColorB);
        Assert.Equal(20.0, options.LoupeOffset);
    }

    /// <summary>付加情報ラベルの既定値が要件（表示し、ホストの配色を使う）を満たすことを検証する。</summary>
    [Fact]
    public void 既定値_付加情報ラベルは表示して配色と寸法を持つ()
    {
        var labels = FadeMaskDragRectLabelOptions.Default;

        Assert.True(labels.ShowOperationLabel);
        Assert.Equal(12.0, labels.OperationFontSize);
        Assert.Equal(DrawingColor.FromArgb(255, 220, 220, 220), labels.OperationColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), labels.OperationBackgroundColor);
        Assert.True(labels.ShowInfoLabel);
        Assert.Equal(14.0, labels.InfoFontSize);
        Assert.Equal(DrawingColor.White, labels.InfoColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), labels.InfoBackgroundColor);
    }

    /// <summary>新しいインスタンスも既定値に一致することを検証する。</summary>
    [Fact]
    public void 既定値_新規生成はDefaultと等しい()
        => Assert.Equal(FadeMaskDragRectOptions.Default, new FadeMaskDragRectOptions());

    /// <summary>部分変更が他の項目を維持することを検証する。</summary>
    [Fact]
    public void 部分変更_with式は指定した項目だけを差し替える()
    {
        var changed = FadeMaskDragRectOptions.Default with
        {
            LoupeOffset = 32.0,
            SelectionRectColorA = DrawingColor.Lime,
        };

        Assert.Equal(32.0, changed.LoupeOffset);
        Assert.Equal(DrawingColor.Lime, changed.SelectionRectColorA);
        // 指定していない項目は既定値を維持する
        Assert.Equal(FadeMaskDragRectOptions.Default.Loupe, changed.Loupe);
        Assert.Equal(DrawingColor.Black, changed.SelectionRectColorB);
    }

    /// <summary>装飾の外観が装飾の設定に含まれることを検証する。</summary>
    [Fact]
    public void 装飾_選択矩形の設定とは別の値が単一所有する()
    {
        var appearance = new OuterDecoAppearance { BorderThickness = 3.0 };

        var options = FadeMaskDragRectOptions.Default with
        {
            Deco = MarqueeDecoOptions.Default with { OuterAppearance = appearance },
            Marquee = MarqueeOptions.Default with
            {
                MinSelectionSize = 12.0,
                ClampToDomainBounds = false,
            },
        };

        // 枠線の寸法（装飾）は装飾の設定が持ち、選択の設定は選択の判断に必要な項目だけを持つ
        Assert.Equal(3.0, options.Deco.OuterAppearance.BorderThickness);
        Assert.Equal(12.0, options.Marquee.MinSelectionSize);
        Assert.False(options.Marquee.ClampToDomainBounds);
    }

    /// <summary>等価性が値で判定されることを検証する。</summary>
    [Fact]
    public void 等価性_同じ値の設定は等しい()
    {
        var a = FadeMaskDragRectOptions.Default with { LoupeOffset = 24.0 };
        var b = FadeMaskDragRectOptions.Default with { LoupeOffset = 24.0 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, FadeMaskDragRectOptions.Default);
    }
}

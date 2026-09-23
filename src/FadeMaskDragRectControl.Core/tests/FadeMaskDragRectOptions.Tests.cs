using Estable.Marquee;
using Estable.Marquee.Decoration;
using Estable.Loupe;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.FadeMaskDragRect.Tests;

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
        Assert.Equal(DrawingColor.White, options.SelectionRectColorA);
        Assert.Equal(DrawingColor.Black, options.SelectionRectColorB);
        Assert.Equal(20.0, options.LoupeOffset);
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

    /// <summary>外側の装飾の外観が選択矩形の設定に含まれることを検証する。</summary>
    [Fact]
    public void 外側の装飾_選択矩形の設定が単一所有する()
    {
        var appearance = new OuterDecoAppearance { BorderColor = DrawingColor.Lime, BorderThickness = 3.0 };

        var options = FadeMaskDragRectOptions.Default with
        {
            Marquee = MarqueeOptions.Default with
            {
                OuterAppearance = appearance,
                HandleColor = DrawingColor.Orange,
                MinSelectionSize = 12.0,
                ClampToDomainBounds = false,
            },
        };

        // 枠線（外側の装飾）と選択矩形の他の設定が同じ 1 つの値に揃う（写し替える導出処理を持たない）
        Assert.Equal(DrawingColor.Lime, options.Marquee.OuterAppearance.BorderColor);
        Assert.Equal(3.0, options.Marquee.OuterAppearance.BorderThickness);
        Assert.Equal(DrawingColor.Orange, options.Marquee.HandleColor);
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

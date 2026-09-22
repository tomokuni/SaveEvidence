using Estable.DragRect;
using Estable.FadeMask;
using Estable.Loupe;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectOptions"/> の単体テスト（既定値・部分変更・枠線の導出）。</summary>
/// <remarks>UI 非依存の値型のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class FadeMaskDragRectOptionsTests
{
    /// <summary>既定値が内包コントロールの既定値と一致することを検証する。</summary>
    [Fact]
    public void 既定値_内包コントロールの既定値を用いる()
    {
        var options = FadeMaskDragRectOptions.Default;

        Assert.Equal(DragRectOptions.Default, options.DragRect);
        Assert.Equal(LoupeOptions.Default, options.Loupe);
        Assert.Equal(new FadeMaskAppearance(), options.Overlay);
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

    /// <summary>枠線が暗転マスクの外観から導出されることを検証する。</summary>
    [Fact]
    public void 枠線導出_暗転マスクの枠線と同じ値になる()
    {
        var options = FadeMaskDragRectOptions.Default with
        {
            DragRect = DragRectOptions.Default with { BorderColor = DrawingColor.Red, BorderThickness = 1.0 },
            Overlay = new FadeMaskAppearance { BorderColor = DrawingColor.Lime, BorderThickness = 3.0 },
        };

        var resolved = options.ResolveDragRectOptions();

        Assert.Equal(DrawingColor.Lime, resolved.BorderColor);
        Assert.Equal(3.0, resolved.BorderThickness);
    }

    /// <summary>枠線以外の選択矩形の設定が導出で失われないことを検証する。</summary>
    [Fact]
    public void 枠線導出_枠線以外の設定は維持される()
    {
        var options = FadeMaskDragRectOptions.Default with
        {
            DragRect = DragRectOptions.Default with
            {
                HandleColor = DrawingColor.Orange,
                MinSelectionSize = 12.0,
                ClampToDomainBounds = false,
            },
        };

        var resolved = options.ResolveDragRectOptions();

        Assert.Equal(DrawingColor.Orange, resolved.HandleColor);
        Assert.Equal(12.0, resolved.MinSelectionSize);
        Assert.False(resolved.ClampToDomainBounds);
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

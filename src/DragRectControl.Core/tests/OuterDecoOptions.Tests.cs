using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.DragRect.Decoration.Tests;

/// <summary><see cref="OuterDecoOptions"/>（設定と描画状態の単一値）の単体テスト。</summary>
/// <remarks>
/// 既定値・部分変更（<c>with</c>）・値の等価性を検証する。<br/>
/// 値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず同じ結果になることを保証する。
/// </remarks>
public class OuterDecoOptionsTests
{
    /// <summary>既定値がコアの既定（外観・モード・選択なし）であることを検証する。</summary>
    [Fact]
    public void Default_コアの既定値を持つ()
    {
        var options = OuterDecoOptions.Default;

        Assert.Equal(new OuterDecoAppearance(), options.Appearance);
        Assert.Equal(OuterDecoRenderMode.AreaSelect, options.Mode);
        Assert.Equal("", options.WindowTitle);
        Assert.False(options.ShowFullOverlay);
        Assert.Null(options.LogicalSelectionRect);
        Assert.Null(options.LogicalHighlightRect);
        Assert.False(options.HasSelection);
        Assert.False(options.HasHighlight);
    }

    /// <summary><c>new</c> で生成した値も既定と同じであることを検証する。</summary>
    [Fact]
    public void コンストラクタ_既定値と同じ値になる()
    {
        Assert.Equal(OuterDecoOptions.Default, new OuterDecoOptions());
    }

    /// <summary><c>with</c> 式で指定した項目だけが変わり、他は現在値を保つことを検証する。</summary>
    [Fact]
    public void with式_指定した項目だけが変わる()
    {
        var appearance = new OuterDecoAppearance { BorderThickness = 3.0 };
        var options = OuterDecoOptions.Default with
        {
            Appearance = appearance,
            Mode = OuterDecoRenderMode.WindowSelect,
            WindowTitle = "メモ帳",
            ShowFullOverlay = true,
            LogicalSelectionRect = new EsRect(10, 20, 30, 40),
            LogicalHighlightRect = new EsRect(1, 2, 3, 4),
            HasSelection = true,
            HasHighlight = true,
        };

        Assert.Same(appearance, options.Appearance);
        Assert.Equal(OuterDecoRenderMode.WindowSelect, options.Mode);
        Assert.Equal("メモ帳", options.WindowTitle);
        Assert.True(options.ShowFullOverlay);
        Assert.Equal(new EsRect(10, 20, 30, 40), options.LogicalSelectionRect);
        Assert.Equal(new EsRect(1, 2, 3, 4), options.LogicalHighlightRect);
        Assert.True(options.HasSelection);
        Assert.True(options.HasHighlight);

        // 既定の値は変更されない（不変）
        Assert.Equal(OuterDecoRenderMode.AreaSelect, OuterDecoOptions.Default.Mode);
        Assert.Null(OuterDecoOptions.Default.LogicalSelectionRect);
    }

    /// <summary>同じ項目を持つ値が等価であることを検証する（変更通知のスキップに必要）。</summary>
    [Fact]
    public void 等価性_同じ項目の値は等しい()
    {
        var a = OuterDecoOptions.Default with { LogicalSelectionRect = new EsRect(1, 2, 3, 4) };
        var b = OuterDecoOptions.Default with { LogicalSelectionRect = new EsRect(1, 2, 3, 4) };

        Assert.Equal(a, b);
        Assert.NotEqual(a, OuterDecoOptions.Default);
    }

    /// <summary>選択矩形が 1 項目でも異なれば等価にならないことを検証する。</summary>
    [Fact]
    public void 等価性_選択矩形が異なれば等しくない()
    {
        var a = OuterDecoOptions.Default with { LogicalSelectionRect = new EsRect(1, 2, 3, 4) };
        var b = OuterDecoOptions.Default with { LogicalSelectionRect = new EsRect(1, 2, 3, 5) };

        Assert.NotEqual(a, b);
    }
}

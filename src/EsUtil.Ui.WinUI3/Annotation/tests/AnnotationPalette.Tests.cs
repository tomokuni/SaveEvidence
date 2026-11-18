using EsUtil.Feature.Annotation;
using Xunit;

namespace EsUtil.Ui.WinUI3.Annotation.Tests;

/// <summary>WinUI 3 版の注釈が公開する値のテスト（XAML のランタイムを必要としない範囲）。</summary>
/// <remarks>
/// コントロールの生成は XAML のランタイムを必要とするため、ここでは<b>色の並び・フォントの並び・
/// 実寸の近似</b>だけを確かめる（要素の描画はアプリで確認する）。
/// </remarks>
public class AnnotationPaletteTests
{
    /// <summary>代表色は 40 色あり、パレットと代表色の 2 つのタブを作る。</summary>
    [Fact]
    internal void 代表色は40色でタブは2つ()
    {
        Assert.Equal(40, AnnotationPalette.RepresentativeColors.Count);
        Assert.Equal(2, AnnotationPalette.CreateTabs().Count);
        Assert.Equal(AnnotationPalette.ListTabTitle, AnnotationPalette.CreateTabs()[1].Title);
    }

    /// <summary>代表色パレットのタブは指定した列数を持つ。</summary>
    [Fact]
    internal void 代表色パレットは指定した列数を持つ()
    {
        var tab = AnnotationPalette.CreatePaletteTab(5);

        Assert.Equal(5, tab.Columns);
        Assert.Equal(40, tab.Colors.Count);
    }

    /// <summary>フォントの一覧は優先フォントを先頭に置く。</summary>
    [Fact]
    internal void フォントの一覧は優先フォントを先頭に置く()
    {
        Assert.Equal(AnnotationDefaults.PreferredFontFamilies[0], AnnotationFonts.All[0]);
        Assert.True(AnnotationFonts.All.Count > AnnotationDefaults.PreferredFontFamilies.Count);
    }

    /// <summary>XAML を使えない環境では文字数と文字サイズから実寸を近似する。</summary>
    [Fact]
    internal void 実寸は文字数に比例する()
    {
        var style = AnnotationDefaults.TextStyle with { FontSize = 20 };

        var shortSize = AnnotationTextMetrics.Measure("あ", style);
        var longSize = AnnotationTextMetrics.Measure("ああああ", style);

        Assert.True(longSize.Width > shortSize.Width);
        Assert.True(shortSize.Height >= style.FontSize);
    }
}

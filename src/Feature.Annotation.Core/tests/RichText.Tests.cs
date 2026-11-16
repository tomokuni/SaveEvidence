using DrawingColor = System.Drawing.Color;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="RichText"/> の仕様テスト（文字単位の書式変更と run の分割）。</summary>
/// <remarks>
/// 書式は run として保存されるため、範囲適用のたびに run が増え続けないこと（同じ書式は連結されること）も
/// 併せて固定する（保存する JSON の大きさに効く）。
/// </remarks>
public class RichTextTests
{
    /// <summary>範囲の途中だけ書式を変えると run が 3 つへ分かれることを検証する。</summary>
    [Fact]
    internal void ApplyStyle_範囲の途中はrunを分割する()
    {
        var red = AnnotationDefaults.TextStyle with { Color = DrawingColor.FromArgb(255, 255, 0, 0) };
        var text = RichText.Create("あいうえお", AnnotationDefaults.TextStyle);

        var updated = text.ApplyStyle(1, 3, red);

        Assert.Equal(3, updated.Runs.Length);
        Assert.Equal("あ", updated.Runs[0].Text);
        Assert.Equal("いうえ", updated.Runs[1].Text);
        Assert.Equal(red, updated.Runs[1].Style);
        Assert.Equal("お", updated.Runs[2].Text);
        Assert.Equal("あいうえお", updated.PlainText);
    }

    /// <summary>同じ書式の隣接する run が連結されることを検証する。</summary>
    [Fact]
    internal void ApplyStyle_同じ書式はrunを連結する()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あいう", AnnotationDefaults.TextStyle).ApplyStyle(2, 1, bold);

        var updated = text.ApplyStyle(1, 1, bold);

        Assert.Equal(2, updated.Runs.Length);
        Assert.Equal("あ", updated.Runs[0].Text);
        Assert.Equal("いう", updated.Runs[1].Text);
        Assert.True(updated.Runs[1].Style.IsBold);
    }

    /// <summary>長さ 0 は全体へ適用することを検証する。</summary>
    [Fact]
    internal void ApplyStyle_長さ0は全体へ適用する()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var updated = RichText.Create("あいう", AnnotationDefaults.TextStyle).ApplyStyle(0, 0, bold);

        Assert.Single(updated.Runs);
        Assert.True(updated.Runs[0].Style.IsBold);
    }

    /// <summary>範囲が文字数を超えても例外にせず末尾までへ丸めることを検証する。</summary>
    [Fact]
    internal void ApplyStyle_範囲外は末尾までへ丸める()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var updated = RichText.Create("あい", AnnotationDefaults.TextStyle).ApplyStyle(1, 100, bold);

        Assert.Equal("あい", updated.PlainText);
        Assert.False(updated.Runs[0].Style.IsBold);
        Assert.True(updated.Runs[1].Style.IsBold);
    }

    /// <summary>指定位置の書式を取得できることを検証する。</summary>
    [Fact]
    internal void StyleAt_位置の書式を返す()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あい", AnnotationDefaults.TextStyle).ApplyStyle(1, 1, bold);

        Assert.False(text.StyleAt(0).IsBold);
        Assert.True(text.StyleAt(1).IsBold);
        // 範囲外は末尾の書式へフォールバックする（ツールバーの初期表示用）
        Assert.True(text.StyleAt(99).IsBold);
    }
}

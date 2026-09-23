using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration.Tests;

/// <summary><see cref="OuterDecoAppearance"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 既定値・部分変更・サブラインの組み立て（<see cref="OuterDecoAppearance.BuildSubLines"/>）を検証する。<br/>
/// 行の文言・順序・表示可否はコアが単一所有するため、本テストが両 UI の共通仕様を固定する。
/// </remarks>
public class OuterDecoAppearanceTests
{
    // ====================================================================
    // 既定値
    // ====================================================================

    /// <summary>既定値が両 UI の従来既定（黒 50% の暗転・ダークグレーの枠線・白ラベル）と一致することを検証する。</summary>
    [Fact]
    public void 既定値が想定どおりである()
    {
        var appearance = new OuterDecoAppearance();

        Assert.Equal(DrawingColor.FromArgb(128, 0, 0, 0), appearance.OverlayColor);
        Assert.Equal(DrawingColor.FromArgb(255, 169, 169, 169), appearance.BorderColor);
        Assert.Equal(2.0, appearance.BorderThickness);
        Assert.True(appearance.ShowSizeLabel);
        Assert.Equal(DrawingColor.White, appearance.SizeLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), appearance.SizeLabelBackgroundColor);
        Assert.Equal(14.0, appearance.SizeLabelFontSize);
        Assert.True(appearance.ShowSubLines);
        Assert.Equal(DrawingColor.FromArgb(255, 220, 220, 220), appearance.SubLineColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), appearance.SubLineBackgroundColor);
        Assert.Equal(14.0, appearance.SubLineFontSize);
        Assert.True(appearance.InfoLabelVisible);
        Assert.Equal(DrawingColor.White, appearance.InfoLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), appearance.InfoLabelBackgroundColor);
        Assert.Equal(14.0, appearance.InfoLabelFontSize);
    }

    /// <summary>部分変更では他の値が維持されることを検証する。</summary>
    [Fact]
    public void 部分変更では他の値が維持される()
    {
        var baseline = new OuterDecoAppearance();

        var changed = baseline with { BorderThickness = 3.0, ShowSizeLabel = false };

        Assert.Equal(3.0, changed.BorderThickness);
        Assert.False(changed.ShowSizeLabel);
        Assert.Equal(baseline.OverlayColor, changed.OverlayColor);
        Assert.Equal(baseline.InfoLabelFontSize, changed.InfoLabelFontSize);
    }

    // ====================================================================
    // BuildSubLines
    // ====================================================================

    /// <summary>操作ラベルと情報ラベルを上から順に返すことを検証する。</summary>
    [Fact]
    public void BuildSubLines_操作ラベルと情報ラベルを順に返す()
    {
        var appearance = new OuterDecoAppearance();

        var lines = appearance.BuildSubLines("確定: Enter", "メモ帳");

        Assert.Equal(2, lines.Count);
        Assert.Equal("確定: Enter", lines[0].Text);
        Assert.Equal("メモ帳", lines[1].Text);
    }

    /// <summary>操作ラベルの文言はホストが与えた文字列がそのまま使われることを検証する。</summary>
    /// <param name="operationText">操作ラベルの文言。</param>
    [Theory]
    [InlineData("確定: Enter")]
    [InlineData("ダブルクリックで確定")]
    public void BuildSubLines_操作ラベルの文言はホストが与える(string operationText)
    {
        var lines = new OuterDecoAppearance().BuildSubLines(operationText, "");

        Assert.Equal(operationText, Assert.Single(lines).Text);
    }

    /// <summary>操作ラベルの文言が空なら行を返さないことを検証する。</summary>
    /// <param name="operationText">操作ラベルの文言。</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildSubLines_操作ラベルの文言が空なら行を返さない(string? operationText)
    {
        var lines = new OuterDecoAppearance().BuildSubLines(operationText, "");

        Assert.Empty(lines);
    }

    /// <summary>操作ラベルを非表示にすると情報ラベルだけが返ることを検証する。</summary>
    [Fact]
    public void BuildSubLines_操作ラベル非表示なら情報ラベルのみ返す()
    {
        var appearance = new OuterDecoAppearance { ShowSubLines = false };

        var lines = appearance.BuildSubLines("確定: Enter", "メモ帳");

        Assert.Equal("メモ帳", Assert.Single(lines).Text);
    }

    /// <summary>情報ラベルを非表示にすると操作ラベルだけが返ることを検証する。</summary>
    [Fact]
    public void BuildSubLines_情報ラベル非表示なら操作ラベルのみ返す()
    {
        var appearance = new OuterDecoAppearance { InfoLabelVisible = false };

        var lines = appearance.BuildSubLines("確定: Enter", "メモ帳");

        Assert.Equal("確定: Enter", Assert.Single(lines).Text);
    }

    /// <summary>情報ラベルの文言が空なら行を返さないことを検証する。</summary>
    /// <param name="infoText">情報ラベルの文言。</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildSubLines_情報ラベルの文言が空なら行を返さない(string? infoText)
    {
        var lines = new OuterDecoAppearance().BuildSubLines(null, infoText);

        Assert.Empty(lines);
    }

    /// <summary>操作ラベルを非表示かつ情報ラベルの文言が空なら行が空になることを検証する。</summary>
    [Fact]
    public void BuildSubLines_表示する行がない場合は空になる()
    {
        var appearance = new OuterDecoAppearance { ShowSubLines = false };

        var lines = appearance.BuildSubLines("確定: Enter", "");

        Assert.Empty(lines);
    }

    /// <summary>操作ラベルが SubLine 系の外観、情報ラベルが InfoLabel 系の外観を使うことを検証する。</summary>
    [Fact]
    public void BuildSubLines_行ごとの外観が設定値を使う()
    {
        var appearance = new OuterDecoAppearance
        {
            SubLineFontSize = 12.0,
            SubLineColor = DrawingColor.Red,
            SubLineBackgroundColor = DrawingColor.FromArgb(179, 0, 0, 139),
            InfoLabelFontSize = 10.0,
            InfoLabelColor = DrawingColor.Lime,
            InfoLabelBackgroundColor = DrawingColor.FromArgb(77, 139, 0, 0),
        };

        var lines = appearance.BuildSubLines("確定: Enter", "メモ帳");

        Assert.Equal(12.0, lines[0].FontSize);
        Assert.Equal(DrawingColor.Red, lines[0].Color);
        Assert.Equal(DrawingColor.FromArgb(179, 0, 0, 139), lines[0].BackgroundColor);
        Assert.Equal(10.0, lines[1].FontSize);
        Assert.Equal(DrawingColor.Lime, lines[1].Color);
        Assert.Equal(DrawingColor.FromArgb(77, 139, 0, 0), lines[1].BackgroundColor);
    }
}

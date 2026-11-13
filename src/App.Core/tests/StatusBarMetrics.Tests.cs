using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="StatusBarMetrics"/> の仕様テスト（ステータス バーの寸法）。</summary>
/// <remarks>
/// ステータス バーの文字サイズは 4 つの UI 版で共通に使うため、値を固定する。<br/>
/// <b>経緯</b>: AvaloniaUI 版だけがテーマの既定（14 DIP）を使い、他の UI 版より文字が大きく見えた。
/// </remarks>
public class StatusBarMetricsTests
{
    [Fact]
    internal void FontSize_は4UI版で共通の12()
        => Assert.Equal(12, StatusBarMetrics.FontSize);

    [Fact]
    internal void FontSize_は設定ページの本文より小さい()
        => Assert.True(StatusBarMetrics.FontSize < SettingsMetrics.FontSize);

    /// <summary>幅の基準が「8888 x 8888」と「WW」であることを検証する（値の長さで幅が変わらないようにする）。</summary>
    [Fact]
    internal void 基準の文字列は9文字と2文字()
    {
        Assert.Equal("8888 x 8888", StatusBarMetrics.PositionWidthReferenceText);
        Assert.Equal("8888 x 8888", StatusBarMetrics.ImageSizeWidthReferenceText);
        Assert.Equal("WW", StatusBarMetrics.RightPaddingReferenceText);
    }

    /// <summary>幅を基準の文字列で測ることを検証する（測る関数は UI が与える）。</summary>
    [Fact]
    internal void ResolveFieldWidths_基準の文字列で測る()
    {
        // 1 文字 10 DIP の等幅フォントとみなす（測る関数をテストが差し替えられる）
        static double Measure(string text) => text.Length * 10;

        var widths = StatusBarMetrics.ResolveFieldWidths(Measure);

        Assert.Equal(110, widths.Position);
        Assert.Equal(110, widths.ImageSize);
        Assert.Equal(20, widths.RightPadding);
    }

    /// <summary>測る関数が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_測る関数がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => StatusBarMetrics.ResolveFieldWidths(null!));
}

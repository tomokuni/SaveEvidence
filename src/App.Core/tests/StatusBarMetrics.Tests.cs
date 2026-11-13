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

    /// <summary>幅は推奨幅、右の余白は「WW」から内部余白を引いた値になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_幅は推奨幅で余白はWWから内部余白を引く()
    {
        // 1 文字 10 DIP の等幅フォントとみなし、内部余白を 8 DIP とする（Label を模す）
        static double Advance(string text) => text.Length * 10;
        static double Box(string text) => (text.Length * 10) + 8;

        var widths = StatusBarMetrics.ResolveFieldWidths(Advance, Box);

        // 幅は推奨幅: 「8888 x 8888」(11 文字 = 110) + 内部余白 8 = 118
        Assert.Equal(118, widths.Position);
        Assert.Equal(118, widths.ImageSize);
        // 右の余白は「WW」(20) - 内部余白 8 = 12。 幅 + 余白 = 130 で一定
        Assert.Equal(12, widths.Gap);
        Assert.Equal(130, widths.Position + widths.Gap);
    }

    /// <summary>内部余白を持たないコントロールでは、右の余白が「WW」の送り幅になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_内部余白が無ければ余白はWWの送り幅()
    {
        static double Advance(string text) => text.Length * 10;

        var widths = StatusBarMetrics.ResolveFieldWidths(Advance, Advance);

        Assert.Equal(110, widths.Position);
        Assert.Equal(20, widths.Gap);
    }
    /// <summary>測る関数が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_測る関数がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => StatusBarMetrics.ResolveFieldWidths(null!, null!));
}

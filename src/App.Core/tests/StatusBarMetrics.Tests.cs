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

    /// <summary>幅が「基準の推奨幅 + WW」になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_基準の推奨幅にWWを足す()
    {
        // 1 文字 10 DIP の等幅フォントとみなし、内部余白を 8 DIP とする（Label を模す）
        static double Advance(string text) => text.Length * 10;
        static double Box(string text) => (text.Length * 10) + 8;

        var widths = StatusBarMetrics.ResolveFieldWidths(Advance, Box);

        // 推奨幅（「9999, 9999」= 10 文字 = 100 + 内部余白 8 = 108）+ 「WW」(20) = 128
        Assert.Equal(128, widths.Position);
        // 推奨幅（「9999 x 9999」= 11 文字 = 110 + 内部余白 8 = 118）+ 「WW」(20) = 138
        Assert.Equal(138, widths.ImageSize);
        // 空きの幅はそのまま「WW」（幅には内部余白の分だけ余分が乗る）
        Assert.Equal(20, widths.Gap);
    }

    /// <summary>基準の文字列が書式どおりであることを検証する（左寄せで最も長い値が入る）。</summary>
    [Fact]
    internal void 基準の文字列は各書式の最も長い値()
    {
        Assert.Equal("9999, 9999", StatusBarMetrics.PositionWidthReferenceText);
        Assert.Equal("9999 x 9999", StatusBarMetrics.ImageSizeWidthReferenceText);
        Assert.Equal("WW", StatusBarMetrics.RightPaddingReferenceText);
    }
    /// <summary>測る関数が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_測る関数がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => StatusBarMetrics.ResolveFieldWidths(null!, null!));
}

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

    /// <summary>幅が「基準の文字列の送り幅 + W」になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_基準の文字列の送り幅にWを足す()
    {
        // 1 文字 10 DIP のフォントとみなす
        static double Advance(string text) => text.Length * 10;

        var widths = StatusBarMetrics.ResolveFieldWidths(Advance);

        // 「pt: 8888, 8888」(14 文字 = 140) + 「W」(10) = 150
        Assert.Equal(150, widths.Position);
        // 「size: 8888 x 8888」(17 文字 = 170) + 「W」(10) = 180
        Assert.Equal(180, widths.ImageSize);
        Assert.Equal(10, widths.Gap);
    }

    /// <summary>右の空きが「W」の実測幅で決まることを検証する（文字数では決めない）。</summary>
    [Fact]
    internal void ResolveFieldWidths_右の空きはWの実測幅になる()
    {
        // プロポーショナル フォントを模し、「W」だけを他の文字より広く測る
        static double Advance(string text) => text == "W" ? 13 : text.Length * 10;

        var widths = StatusBarMetrics.ResolveFieldWidths(Advance);

        Assert.Equal(13, widths.Gap);
        Assert.Equal(140 + 13, widths.Position);
        Assert.Equal(170 + 13, widths.ImageSize);
    }

    /// <summary>基準の文字列が書式どおりであることを検証する（左寄せで最も長い値が入る）。</summary>
    [Fact]
    internal void 基準の文字列は各書式の最も長い値()
    {
        Assert.Equal("pt: 8888, 8888", StatusBarMetrics.PositionWidthReferenceText);
        Assert.Equal("size: 8888 x 8888", StatusBarMetrics.ImageSizeWidthReferenceText);
        Assert.Equal("W", StatusBarMetrics.RightPaddingReferenceText);
    }
    /// <summary>測る関数が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void ResolveFieldWidths_測る関数がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => StatusBarMetrics.ResolveFieldWidths(null!));
}

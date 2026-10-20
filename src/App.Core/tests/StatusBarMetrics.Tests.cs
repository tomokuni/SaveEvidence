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
}

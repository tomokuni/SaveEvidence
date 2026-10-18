using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="SettingsMetrics"/> の仕様テスト（設定ページの寸法と共通文言）。</summary>
/// <remarks>
/// 文字サイズと補足の文言は 4 つの UI 版で共通に使うため、値を固定する。<br/>
/// 文字サイズは AvaloniaUI 版（Fluent テーマの既定）に合わせている。
/// </remarks>
public class SettingsMetricsTests
{
    [Fact]
    internal void FontSize_はAvaloniaUI版の既定と同じ14()
        => Assert.Equal(14, SettingsMetrics.FontSize);

    [Fact]
    internal void HintFontSize_は本文より小さい()
        => Assert.True(SettingsMetrics.HintFontSize < SettingsMetrics.FontSize);

    [Fact]
    internal void SaveFolderHint_は例とプレースホルダの説明を含む()
    {
        Assert.StartsWith("例:", SettingsMetrics.SaveFolderHint, StringComparison.Ordinal);
        Assert.Contains("{MyPictures}", SettingsMetrics.SaveFolderHint, StringComparison.Ordinal);
    }
}

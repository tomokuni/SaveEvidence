using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="EnumDisplayNames"/> の仕様テスト（表示名の解決）。</summary>
/// <remarks>
/// 表示名は列挙の <c>DescriptionAttribute</c> が単一所有する。 4 つの UI 版が同じ文言を出すため、
/// 設定画面の選択肢の文言をここで固定する。
/// </remarks>
public class EnumDisplayNamesTests
{
    [Fact]
    internal void Get_は説明属性の文言を返す()
    {
        Assert.Equal("グラデーション画像", EnumDisplayNames.Get(StartupPreviewMode.Gradient));
        Assert.Equal("空白", EnumDisplayNames.Get(StartupPreviewMode.Blank));
    }

    [Fact]
    internal void Get_はテーマモードの文言を返す()
    {
        Assert.Equal("ライト", EnumDisplayNames.Get(ThemeMode.Light));
        Assert.Equal("ダーク", EnumDisplayNames.Get(ThemeMode.Dark));
        Assert.Equal("システム", EnumDisplayNames.Get(ThemeMode.System));
    }

    [Fact]
    internal void Get_は説明属性が無ければ列挙の名前を返す()
        => Assert.Equal("Bold", EnumDisplayNames.Get(FontWeightSample.Bold));
}

/// <summary>説明属性を持たない列挙（<see cref="EnumDisplayNames"/> のフォールバック検証用）。</summary>
internal enum FontWeightSample
{
    Bold,
}

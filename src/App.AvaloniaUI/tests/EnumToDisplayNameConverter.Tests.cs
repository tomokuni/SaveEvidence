using System.Globalization;
using EsUtil.SaveEvidence.App.AvaloniaUI.Converters;
using EsUtil.SaveEvidence.App.Core.Enum;
using Xunit;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Tests;

/// <summary>enum 値を表示名（<see cref="System.ComponentModel.DescriptionAttribute"/>）へ変換するコンバーターの単体テスト。</summary>
/// <remarks>
/// <b>目的</b>: Native AOT / トリミング配布でも表示名が解決できることを守る。<br/>
/// 変換対象の enum はコンバーターの switch へ静的に登録する（実行時の型から
/// <see cref="Type.GetMember(string)"/> を呼ぶ形は IL2070 になる）。 登録の漏れは
/// <b>例外ではなく「enum 名がそのまま表示される」</b>という静かな劣化になるため、
/// 表示名を持つ各メンバーをここで固定する。
/// </remarks>
public class EnumToDisplayNameConverterTests
{
    private static readonly EnumToDisplayNameConverter s_converter = new();

    /// <summary>表示名（Description 属性）を持つ enum は表示名へ変換される。</summary>
    /// <param name="value">対象の enum 値。</param>
    /// <param name="expected">期待する表示名。</param>
    [Theory]
    [InlineData(ThemeMode.Light, "ライト")]
    [InlineData(ThemeMode.Dark, "ダーク")]
    [InlineData(ThemeMode.System, "システム")]
    internal void Convert_表示名を持つenumは表示名へ変換される(ThemeMode value, string expected)
        => Assert.Equal(expected, s_converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture));

    /// <summary>同じ値の 2 回目の変換はキャッシュから返る（同じ表示名になる）。</summary>
    [Fact]
    internal void Convert_同じ値の再変換は同じ結果になる()
    {
        var first = s_converter.Convert(ThemeMode.Light, typeof(string), null, CultureInfo.InvariantCulture);
        var second = s_converter.Convert(ThemeMode.Light, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(first, second);
        Assert.Equal("ライト", second);
    }

    /// <summary>表示名を持たない enum はメンバー名をそのまま返す（例外にしない）。</summary>
    [Fact]
    internal void Convert_表示名を持たないenumはメンバー名を返す()
        => Assert.Equal("Settings", s_converter.Convert(AppPage.Settings, typeof(string), null, CultureInfo.InvariantCulture));

    /// <summary>enum 以外の値も例外にせず文字列化して返す。</summary>
    [Fact]
    internal void Convert_enum以外の値は文字列化して返す()
        => Assert.Equal("text", s_converter.Convert("text", typeof(string), null, CultureInfo.InvariantCulture));

    /// <summary>null は空文字列になる（バインディングの初期化で例外にしない）。</summary>
    [Fact]
    internal void Convert_nullは空文字列を返す()
        => Assert.Equal("", s_converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));

    /// <summary>逆変換は未サポート（明示的に例外にする）。</summary>
    [Fact]
    internal void ConvertBack_未サポートの例外になる()
        => Assert.Throws<NotSupportedException>(
            () => s_converter.ConvertBack("ライト", typeof(ThemeMode), null, CultureInfo.InvariantCulture));
}

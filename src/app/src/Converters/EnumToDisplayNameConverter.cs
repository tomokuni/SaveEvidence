using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Estable.SaveEvidence.Converters;

/// <summary>enum 値を <see cref="System.ComponentModel.DescriptionAttribute"/> の表示名へ変換するコンバーター。</summary>
/// <remarks>
/// Description 属性が未定義の enum 値は、そのまま enum 名（ToString()）を返す。<br/>
/// 設定画面の ComboBox（テーマモード等）の表示に使用する。
/// </remarks>
public sealed class EnumToDisplayNameConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), string?> s_displayNameCache = new();

    /// <summary>enum → 表示名</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return "";
        var type = value.GetType();
        if (!type.IsEnum) return value.ToString() ?? "";

        var name = value.ToString()!;
        return s_displayNameCache.GetOrAdd((type, name), static key => ResolveDisplayName(key.Type, key.Name));
    }

    /// <summary>enum メンバー名から表示名を解決する（Description 属性 → 未定義はメンバー名）。</summary>
    private static string? ResolveDisplayName(Type type, string name)
    {
        var member = type.GetMember(name).FirstOrDefault();
        var description = member?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?.Description;
        return description ?? name;
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System.ComponentModel;
using System.Reflection;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>列挙値の表示名（<see cref="DescriptionAttribute"/>）を取得する。</summary>
/// <remarks>
/// 表示名は列挙の定義側が単一所有する（UI ごとに文言を書き分けない）。 属性が無い場合は列挙の名前を返す。
/// </remarks>
public static class EnumDisplayNames
{
    /// <summary>列挙値の表示名を取得する。</summary>
    /// <param name="value">対象の列挙値。</param>
    /// <returns>表示名。 属性が無い場合は列挙の名前。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が null の場合。</exception>
    public static string Get<T>(T value)
        where T : struct, Enum
        => Get((Enum)(object)value);

    /// <summary>列挙値の表示名を取得する。</summary>
    /// <param name="value">対象の列挙値。</param>
    /// <returns>表示名。 属性が無い場合は列挙の名前。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が null の場合。</exception>
    public static string Get(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
    }
}

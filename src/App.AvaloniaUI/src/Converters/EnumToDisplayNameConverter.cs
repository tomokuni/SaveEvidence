using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data.Converters;
using EsUtil.SaveEvidence.App.Core.Enums;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Converters;

/// <summary>enum 値を <see cref="System.ComponentModel.DescriptionAttribute"/> の表示名へ変換するコンバーター。</summary>
/// <remarks>
/// Description 属性が未定義の enum 値は、そのまま enum 名（ToString()）を返す。<br/>
/// 設定画面の ComboBox（テーマモード等）の表示に使用する。<br/>
/// <br/>
/// <b>リフレクションの対象型は静的に確定させる</b>: 実行時の型（<c>value.GetType()</c>）から
/// <see cref="Type.GetMember(string)"/> を呼ぶと、Native AOT / トリミングで IL2070
/// （メンバーのメタデータを保持できない）になる。 表示名を持つ enum は
/// <see cref="Convert(object?, Type, object?, CultureInfo)"/> の switch へ登録し、
/// <c>typeof(対象)</c> を <see cref="DynamicallyAccessedMembersAttribute"/> 付きの引数へ渡す<br/>
/// （トリマーがその enum のメンバーを保持し、警告も出ない）。
/// </remarks>
public sealed class EnumToDisplayNameConverter : IValueConverter
{
    /// <summary>（enum 型, メンバー名）→ 表示名 のキャッシュ。</summary>
    private static readonly ConcurrentDictionary<(Type Type, string Name), string> s_displayNameCache = new();

    /// <summary>enum → 表示名</summary>
    /// <param name="value">変換元の値（enum 値）。</param>
    /// <param name="targetType">変換先の型（未使用）。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>表示名（Description 属性の値。 未定義ならメンバー名）。</returns>
    /// <remarks>
    /// <b>enum のパターン照合（<c>switch</c> 式）を使わない</b>: コンパイラが生成する照合コードが
    /// <c>Enum.GetValues(Type)</c>（AOT 非対応。 IL3050）を要求するため、型判定（<c>is</c>）で分岐する。<br/>
    /// 表示名を持つ enum はここへ登録する（対象型を静的に確定させ、トリミングでメタデータが残るようにする）。
    /// </remarks>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return "";

        if (value is ThemeMode themeMode)
        {
            return ResolveDisplayName(typeof(ThemeMode), themeMode.ToString());
        }

        // 表示名を持たない値はメンバー名をそのまま返す
        return value.ToString() ?? "";
    }

    /// <summary>enum メンバー名から表示名を解決する（Description 属性 → 未定義はメンバー名）。</summary>
    /// <param name="type">対象の enum 型（<c>typeof</c> で静的に渡す）。</param>
    /// <param name="name">enum メンバー名。</param>
    /// <returns>表示名。</returns>
    /// <remarks>
    /// <b><see cref="Type.GetField(string)"/> を使う</b>（<see cref="Type.GetMember(string)"/> ではなく）。<br/>
    /// enum のメンバーは public の静的フィールドであり、<c>GetField</c> が要求するのは
    /// <see cref="DynamicallyAccessedMemberTypes.PublicFields"/> だけである（<c>GetMember</c> は 5 種類を要求し、
    /// <see cref="DynamicallyAccessedMemberTypes.All"/> で満たすと AOT で動的コード（<c>Enum.GetValues(Type)</c>）を要求してしまう）。
    /// </remarks>
    private static string ResolveDisplayName(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        string name)
    {
        var key = (type, name);
        if (s_displayNameCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var description = type.GetField(name)?
            .GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?
            .Description;

        var displayName = description ?? name;
        s_displayNameCache[key] = displayName;
        return displayName;
    }

    /// <summary>逆変換は未サポート。</summary>
    /// <param name="value">変換元の値（未使用）。</param>
    /// <param name="targetType">変換先の型（未使用）。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>返しません。</returns>
    /// <exception cref="NotSupportedException">常に送出します。</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

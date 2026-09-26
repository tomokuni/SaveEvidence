using EsUtil.Platform.Core;
using AvaloniaKey = Avalonia.Input.Key;

namespace EsUtil.Platform.AvaloniaUI.Interop;

/// <summary>UI フレームワーク非依存の <see cref="EsKey"/> と Avalonia の <see cref="AvaloniaKey"/> を相互変換する。</summary>
/// <remarks>
/// 両者はメンバー名が一致するように設計されているため、数値の一致には依存せず名前ベースで変換する。<br/>
/// </remarks>
public static class EsKeyAvaloniaInterop
{
    /// <summary><see cref="EsKey"/> を Avalonia の <see cref="AvaloniaKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する Avalonia のキー。名前が一致しない場合は <see cref="AvaloniaKey.None"/>。</returns>
    public static AvaloniaKey ToAvaloniaKey(this EsKey key) =>
        System.Enum.TryParse<AvaloniaKey>(key.ToString(), out var result) ? result : AvaloniaKey.None;

    /// <summary>Avalonia の <see cref="AvaloniaKey"/> を <see cref="EsKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する <see cref="EsKey"/>。名前が一致しない場合は <see cref="EsKey.None"/>。</returns>
    public static EsKey ToEsKey(this AvaloniaKey key) =>
        System.Enum.TryParse<EsKey>(key.ToString(), out var result) ? result : EsKey.None;
}

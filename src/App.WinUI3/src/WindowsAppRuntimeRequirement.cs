using SdkRuntimeVersion = Microsoft.WindowsAppSDK.Runtime.Version;

namespace EsUtil.SaveEvidence.App.WinUI3;

/// <summary>このアプリが必要とする Windows App Runtime の版。</summary>
/// <remarks>
/// <b>何のためか</b>: EsUtil（<c>EsUtil.Ui.WinUI3.Interop</c>）は自分をビルドしたときの
/// <c>Microsoft.WindowsAppSDK</c> の版を要求版として焼き込む。 本アプリがそれより新しい SDK で
/// ビルドされている場合、EsUtil の版だけで初期化すると<b>アプリのマネージド アセンブリより古い
/// ネイティブ ランタイム</b>が読み込まれる。 実測 2026-11-10: 2.6.5 でビルドしたアプリが 2.6.4 の
/// ランタイムで初期化を通り、レイアウト・画像・アイコン フォントは正常なまま本文のグリフだけが描かれなかった。<br/>
/// <b>版の出どころ</b>: <c>WindowsAppSdkIncludeVersionInfo</c>（<c>App.WinUI3.csproj</c>）が有効なとき
/// 生成される <c>Microsoft.WindowsAppSDK.Runtime.Version</c>（internal）である。 値の所有者は
/// <c>Directory.Packages.props</c> の <c>Microsoft.WindowsAppSDK</c> であり、ここには書かない。<br/>
/// <b>ライブラリとの関係</b>: 版数が異なること自体は問題ではない。 契約は
/// 「アプリの要求版 ≧ ライブラリの下限」であり、実際に使う版は
/// <c>WindowsAppRuntimeBootstrap.GetRequiredVersion</c> が両者の高い方を採って決める。
/// </remarks>
internal static class WindowsAppRuntimeRequirement
{
    /// <summary>このアプリが要求する Windows App Runtime の版を取得する。</summary>
    /// <value>ビルドに使った <c>Microsoft.WindowsAppSDK</c> の版（例: <c>2.6.5.0</c>）。</value>
    public static Version Version { get; } = new(
        SdkRuntimeVersion.Major, SdkRuntimeVersion.Minor, SdkRuntimeVersion.Build, SdkRuntimeVersion.Revision);
}


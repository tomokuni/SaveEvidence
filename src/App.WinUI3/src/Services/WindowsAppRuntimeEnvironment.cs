using EsUtil.Platform.Core;
using EsUtil.Ui.WinUI3.Interop.Bootstrapping;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>Windows App Runtime の版を診断用の値として取得する。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: 「入っている版」と「実際に使っている版」は別であり、要求版より古いランタイムが選ばれると
/// レイアウトは正常なのに本文のグリフだけ描かれない等の分かりにくい不具合になる（実測 2026-11-10）。<br/>
/// OS・.NET・画面・GPU の値は <see cref="EsUtil.SaveEvidence.App.Core.Services.RenderEnvironment"/> が単一所有する
/// （4 つの UI 版で同じ書式にするため）。 本クラスは Windows App Runtime に依存する値だけを持つ。
/// </remarks>
internal static class WindowsAppRuntimeEnvironment
{
    /// <summary>このビルドが要求する Windows App Runtime の版を取得する。</summary>
    /// <value>要求する版の文字列（例: <c>2.6.5</c>）。</value>
    public static string RequiredVersion => WindowsAppRuntimeRequirement.Version.ToString(3);

    /// <summary>インストール済みの Windows App Runtime の版を取得する。</summary>
    /// <returns>「<c>2.6.5.0, 2.6.4.0</c>」の形（新しい順）。 1 件も無い場合は <c>(なし)</c>。</returns>
    /// <remarks>
    /// 自己完結の配置（Windows App Runtime を同封）では 1 件も無いことがある（同封物を使うため）。
    /// 実際に使った版は <see cref="DescribeLoadedVersion"/> が表す。
    /// </remarks>
    public static string DescribeInstalledVersion()
    {
        try
        {
            var versions = WindowsAppRuntimeInventory.GetInstalledVersions();
            return versions.Count == 0
                ? "(なし)"
                : string.Join(", ", versions.Select(version => version.ToString(4)));
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 診断用の値であり、取得できなくても描画を妨げない
            return "(不明)";
        }
    }

    /// <summary>このプロセスが実際に読み込んでいる Windows App Runtime の版を取得する。</summary>
    /// <returns>「<c>2.6.5.0</c>」の形。 取得できない場合は <c>(不明)</c>。</returns>
    public static string DescribeLoadedVersion()
        => WindowsAppRuntimeInventory.TryGetLoadedVersion(out var loaded)
            ? loaded.ToString(4)
            : "(不明)";
}
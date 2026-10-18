using System.Reflection;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>アプリのバージョン表記（ナビゲーション パネルのフッターなどに表示する文字列）。</summary>
/// <remarks>
/// バージョンの出所は <c>Directory.Build.props</c> の <c>&lt;Version&gt;</c>（全プロジェクト共通の単一所有者）であり、<br/>
/// 実行時はアセンブリのバージョンから読む。 <c>0.1.0.0</c> のように末尾が 0 の桁は表示しない（<c>0.1.0</c>）。<br/>
/// <br/>
/// <b>UI 名を含める理由</b>: 4 つの UI 版は設定ファイルとログを共有するため、どの版を起動しているかを<br/>
/// 画面から判別できるようにする（フッターは各 UI 版が自分の名前を渡す）。
/// </remarks>
public static class AppVersion
{
    /// <summary>バージョン文字列を取得する（例: <c>0.1.0</c>）。</summary>
    /// <returns>バージョン文字列。 取得できない場合は <c>unknown</c>。</returns>
    public static string Current => Format(typeof(AppVersion).Assembly);

    /// <summary>UI 名とバージョンを並べた表示用の文字列を取得する（例: <c>AvaloniaUI  v0.1.0</c>）。</summary>
    /// <param name="uiName">UI の名前（<c>AvaloniaUI</c> / <c>MewUI</c> / <c>WinUI3</c> / <c>WinForms</c>）。</param>
    /// <returns>UI 名・空白 2 つ・<c>v</c> 付きのバージョンを並べた文字列。</returns>
    /// <remarks>
    /// 表記（UI 名とバージョンの区切り・<see cref="VersionPrefix"/>）は本メソッドが単一所有する<br/>
    /// （UI ごとに書式がずれないようにする）。 UI 名が空の場合はバージョンだけを返す。
    /// </remarks>
    public static string For(string uiName)
        => string.IsNullOrWhiteSpace(uiName) ? VersionPrefix + Current : $"{uiName}  {VersionPrefix}{Current}";

    /// <summary>バージョン番号の前に付ける接頭辞。</summary>
    public const string VersionPrefix = "v";

    /// <summary>アセンブリのバージョンを表示用の文字列へ整形する。</summary>
    /// <param name="assembly">対象のアセンブリ。</param>
    /// <returns>整形したバージョン文字列。</returns>
    /// <remarks>
    /// 4 桁のうち末尾の 0 を取り除く（<c>0.1.0.0</c> → <c>0.1.0</c>）。<br/>
    /// すべて 0 になった場合は 1 桁だけ残す（空文字を返さない）。
    /// </remarks>
    private static string Format(Assembly assembly)
    {
        if (assembly.GetName().Version is not { } version)
        {
            return "unknown";
        }

        var parts = new List<int> { version.Major, version.Minor, version.Build, version.Revision };
        // 3 桁（Directory.Build.props の <Version> と同じ桁数）は必ず残し、4 桁目以降の 0 だけを省く
        while (parts.Count > 3 && parts[^1] == 0)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        return string.Join('.', parts);
    }
}

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>アプリの同一性（名前・識別子・保存先）の唯一の定義。</summary>
/// <remarks>
/// <b>4 つの UI 版が同じ値を使う</b>ため、名前・タスク バーの識別子・アイコン・保存先をここ 1 か所で決める<br/>
/// （UI 版ごとに文字列を書くと、綴りの違いで設定・ログ・タスク バーのグループ分けがずれる）。<br/>
/// 値は<b>事実のみ</b>を持ち、UI フレームワークに依存しない。<br/>
/// <br/>
/// <b>設定は共有し、ログは分ける</b>: 実行ファイルは 4 つで、設定は 1 つ（どの UI 版を起動しても同じ設定で動く）。<br/>
/// ログは実行ファイルごとに別のファイルへ書く（<see cref="GetLogFilePath"/>。 どの UI 版の記録かをファイル名で判別できる）。
/// </remarks>
public static class AppIdentity
{
    /// <summary>製品名（ウィンドウ タイトル・フォルダ名・ログ ファイル名に使う）。</summary>
    public const string ProductName = "SaveEvidence";

    /// <summary>タスク バーのグループ分けに使うアプリ ユーザー モデル ID。</summary>
    /// <remarks>
    /// 4 つの UI 版が同じ値を使うため、タスク バーでは 1 つのアプリとしてまとまる<br/>
    /// （<see cref="TaskbarIdentity"/> がプロセスへ設定する）。<br/>
    /// 逆ドメイン形式にする（Windows の規則。 他社のアプリと衝突しない名前空間にする）。
    /// </remarks>
    public const string AppUserModelId = "EsUtil.SaveEvidence";

    /// <summary>ウィンドウ（タスク バー）アイコンに使う SVG ファイル名。</summary>
    /// <remarks>4 つの UI 版が同じアイコンを使う（各 UI 版のアイコン ローダーが読み込む）。</remarks>
    public const string IconFileName = "ic_fluent_scan_table_24_regular.svg";

    /// <summary>設定・ログを置くフォルダ（<c>%LOCALAPPDATA%\SaveEvidence</c>）を取得する。</summary>
    public static string LocalAppDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductName);

    /// <summary>ログを置くフォルダ（<c>%LOCALAPPDATA%\SaveEvidence\logs</c>）を取得する。</summary>
    public static string LogsFolder { get; } = Path.Combine(LocalAppDataFolder, "logs");

    /// <summary>指定されたアセンブリのログ ファイルのパス（日次ローテーションの基点）を取得する。</summary>
    /// <param name="assemblyName">ログを書くアセンブリ名（例: <c>SaveEvidence.WinUI3</c>）。</param>
    /// <returns>ログ シンクへ渡すパス。</returns>
    /// <remarks>
    /// <b>実際のファイル名は <c>&lt;アセンブリ名&gt;.&lt;yyyyMMdd&gt;.log</c></b>（例: <c>SaveEvidence.WinUI3.20261109.log</c>）。
    /// Serilog は拡張子の直前に日付を挿入するため、<b>基点の名前は <c>&lt;アセンブリ名&gt;..log</c></b> とし、
    /// 日付の直前へ <c>.</c> を残す（<c>&lt;アセンブリ名&gt;.log</c> を渡すと <c>&lt;アセンブリ名&gt;20261109.log</c> になり、
    /// 区切りが無くなる。 実測 2026-11-09）。<br/>
    /// <b>ログは実行ファイルごとに分ける</b>（設定ファイルは 4 つの UI 版で共有する）。 命名の規則は
    /// <c>.github/命名規約.md</c>「ファイル」、出力先とローテーションは <c>.github/ログ規約.md</c> §1.1 が定める。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="assemblyName"/> が空または空白のみの場合。</exception>
    public static string GetLogFilePath(string assemblyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        return Path.Combine(LogsFolder, assemblyName + "..log");
    }

    /// <summary>設定ファイル（<c>%LOCALAPPDATA%\SaveEvidence\settings.json</c>）を取得する。</summary>
    public static string SettingsFilePath { get; } = Path.Combine(LocalAppDataFolder, "settings.json");

    /// <summary>一時ファイルを置くフォルダ（<c>%TEMP%\SaveEvidence</c>）を取得する。</summary>
    /// <remarks>ウィンドウ アイコンの ICO など、実行時に組み立てるファイルを置く。</remarks>
    public static string TempFolder { get; } = Path.Combine(Path.GetTempPath(), ProductName);
}

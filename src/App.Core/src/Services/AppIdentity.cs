namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>アプリの同一性（名前・識別子・保存先）の唯一の定義。</summary>
/// <remarks>
/// <b>4 つの UI 版が同じ値を使う</b>ため、名前・タスク バーの識別子・アイコン・保存先をここ 1 か所で決める<br/>
/// （UI 版ごとに文字列を書くと、綴りの違いで設定・ログ・タスク バーのグループ分けがずれる）。<br/>
/// 値は<b>事実のみ</b>を持ち、UI フレームワークに依存しない。<br/>
/// <br/>
/// <b>保存先を共有する理由</b>: 実行ファイルは 4 つだが、設定とログは 1 つである<br/>
/// （どの UI 版を起動しても同じ設定で動く）。
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

    /// <summary>ログ ファイル（<c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log</c>）を取得する。</summary>
    /// <remarks>日次ローテーションのため、実際のファイル名には日付が付く（Serilog が付ける）。</remarks>
    public static string LogFilePath { get; } = Path.Combine(LogsFolder, ProductName + ".log");

    /// <summary>設定ファイル（<c>%LOCALAPPDATA%\SaveEvidence\settings.json</c>）を取得する。</summary>
    public static string SettingsFilePath { get; } = Path.Combine(LocalAppDataFolder, "settings.json");

    /// <summary>一時ファイルを置くフォルダ（<c>%TEMP%\SaveEvidence</c>）を取得する。</summary>
    /// <remarks>ウィンドウ アイコンの ICO など、実行時に組み立てるファイルを置く。</remarks>
    public static string TempFolder { get; } = Path.Combine(Path.GetTempPath(), ProductName);
}

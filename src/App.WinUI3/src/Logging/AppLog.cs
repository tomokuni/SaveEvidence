using System.Globalization;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;
using Serilog;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する。
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EsUtil.SaveEvidence.App.WinUI3.Logging;

/// <summary>App.WinUI3 プロジェクトのトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <see cref="LoggerMessageAttribute"/> による事前コンパイル済みログで、<br/>
/// 文字列補間による不要なアロケーションを回避しつつ、一貫したフォーマットで出力する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する。<br/>
/// 出力先は他の UI 版と同一の書式の Serilog 構成（<c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.WinUI3.&lt;yyyyMMdd&gt;.log</c>、<br/>
/// 日次ローテーション・7 日で自動削除）とし、UI 版ごとにファイルを分けて追跡できるようにする。<br/>
/// ロガーは <see cref="CreateLogger"/> で生成し、各クラスの static フィールドで保持する。
/// </remarks>
internal static partial class AppLog
{
    /// <summary>接頭辞（固定。 アプリのログは DLL 名で読み分けるため、ホストからの設定対象外）。</summary>
    private const string Prefix = "[App.WinUI3]";

    /// <summary>このアセンブリの名前（ログ ファイル名の先頭に使う）。</summary>
    /// <remarks>
    /// ログは実行ファイルごとに分かれる（UI 版の区別をファイル名で付ける）。 命名は
    /// <c>.github/命名規約.md</c>「ファイル」、出力先は <c>.github/ログ規約.md</c> §1.1 が定める。
    /// </remarks>
    private static readonly string s_assemblyName =
        typeof(AppLog).Assembly.GetName().Name ?? AppIdentity.ProductName;

    /// <summary>アプリケーション共有の Serilog ロガー（静的 <see cref="Log.Logger"/>）。</summary>
    /// <remarks>
    /// グローバル例外ハンドラと <c>ILoggerFactory</c>（MEL ブリッジ）の両方が<br/>
    /// 同一の Serilog パイプラインを共有するため、静的ロガーを初期化してからファクトリを構築する。
    /// </remarks>
    private static readonly ILoggerFactory s_factory = CreateFactory();

    /// <summary>Serilog ロガーを初期化し、MEL (Microsoft.Extensions.Logging) ブリッジのファクトリを構築する。</summary>
    /// <returns>Serilog へ転送する <see cref="ILoggerFactory"/>。</returns>
    /// <remarks>
    /// 静的 <see cref="Log.Logger"/> を設定し、<c>AddSerilog()</c> がグローバルロガーを MEL へ転送する。<br/>
    /// 複数箇所で <c>LoggerFactory.Create</c> しないよう、本クラスに集約する（ログの二重出力防止）。
    /// </remarks>
    private static ILoggerFactory CreateFactory()
    {
        Log.Logger = CreateLoggingConfiguration().CreateLogger();
        return LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
    }

    /// <summary>Serilog のファイル出力構成を作成する。</summary>
    /// <returns>ログ出力を <c>%LOCALAPPDATA%\SaveEvidence\logs\&lt;アセンブリ名&gt;.&lt;yyyyMMdd&gt;.log</c>（日次ローテーション・7 日保持）へ書き込む構成。</returns>
    /// <remarks>
    /// ログカテゴリは <c>{SourceContext}</c> として出力し、例外はスタックトレース込みで改行して書き出す。<br/>
    /// 書式は他の UI 版と同一にする（ファイルは UI 版ごとに分かれるが、行の書式は共通）。
    /// </remarks>
    private static LoggerConfiguration CreateLoggingConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            // レベルは 3 文字（[TRC] 等）で表す。 {Level:u3} は Verbose を VRB と表すため使わない
            // （規則の所有者は .github/ログ規約.md §4.1）
            .Enrich.With(new LogLevelTextEnricher())
            .WriteTo.File(
                AppIdentity.GetLogFilePath(s_assemblyName),
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{LogLevelText}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                encoding: System.Text.Encoding.UTF8);
    }

    /// <summary>ログ イベントへ 3 文字のレベル表記（<c>TRC</c> 等）を追加するエンリッチャー。</summary>
    /// <remarks>
    /// 写像の規則は <see cref="LogLevelText"/>（コア）が単一所有する。 本クラスは Serilog のレベル名を渡すだけである<br/>
    /// （コアは Serilog に依存しない。 4 つの UI 版が同じ表記を使う）。
    /// </remarks>
    private sealed class LogLevelTextEnricher : Serilog.Core.ILogEventEnricher
    {
        /// <summary>ログ イベントへ <c>LogLevelText</c> プロパティを追加する。</summary>
        /// <param name="logEvent">対象のログ イベント。</param>
        /// <param name="propertyFactory">プロパティの生成元。</param>
        public void Enrich(Serilog.Events.LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
            => logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                LogLevelText.PropertyName, LogLevelText.Abbreviate(logEvent.Level.ToString())));
    }

    /// <summary>指定カテゴリ名のロガーを生成する。</summary>
    /// <param name="categoryName">ロガーカテゴリ名（例: "CapturePage"）。</param>
    /// <returns>生成したロガー。</returns>
    internal static ILogger CreateLogger(string categoryName) => s_factory.CreateLogger(categoryName);

    // ====================================================================
    // アプリライフサイクル
    // ====================================================================

    /// <summary>アプリケーション起動のログ。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>アプリケーション起動 (version={Version})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="version">アプリケーションのバージョン。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix} アプリケーション起動 (version={Version})")]
    private static partial void LogAppStartCore(this ILogger logger, string prefix, string version);

    /// <summary>アプリケーション起動を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="version">アプリケーションのバージョン。</param>
    public static void LogAppStart(this ILogger logger, string version)
        => logger.LogAppStartCore(Prefix, version);

    /// <summary>アプリケーション終了のログ。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>アプリケーション終了 (exitCode={ExitCode})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="exitCode">終了コード。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix} アプリケーション終了 (exitCode={ExitCode})")]
    private static partial void LogAppExitCore(this ILogger logger, string prefix, int exitCode);

    /// <summary>アプリケーション終了を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="exitCode">終了コード。</param>
    public static void LogAppExit(this ILogger logger, int exitCode)
        => logger.LogAppExitCore(Prefix, exitCode);

    /// <summary>.NET ホスト（<c>dotnet</c>）から起動されたことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>.NET ホストから起動されました（実行ファイルから起動してください）</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} .NET ホストから起動されました（実行ファイルから起動してください）")]
    private static partial void LogStartedFromDotnetHostCore(this ILogger logger, string prefix);

    /// <summary>.NET ホスト（<c>dotnet</c>）から起動されたことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    public static void LogStartedFromDotnetHost(this ILogger logger)
        => logger.LogStartedFromDotnetHostCore(Prefix);

    /// <summary>Windows App Runtime を用意できなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>Windows App Runtime を用意できませんでした (required={RequiredVersion}, before={InstalledBefore}, after={InstalledAfter})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="requiredVersion">必要とする Windows App Runtime の版（例: 2.6.4）。</param>
    /// <param name="installedBefore">案内を出す前のインストール済みの版（1 行）。</param>
    /// <param name="installedAfter">案内を出した後のインストール済みの版（1 行）。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} Windows App Runtime を用意できませんでした (required={RequiredVersion}, before={InstalledBefore}, after={InstalledAfter})")]
    private static partial void LogWindowsAppRuntimeUnavailableCore(
        this ILogger logger, string prefix, string requiredVersion,
        string installedBefore, string installedAfter);

    /// <summary>Windows App Runtime を用意できなかったことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="requiredVersion">必要とする Windows App Runtime の版（例: 2.6.4）。</param>
    /// <param name="installedBefore">案内を出す前のインストール済みの版（1 行）。</param>
    /// <param name="installedAfter">案内を出した後のインストール済みの版（1 行）。</param>
    public static void LogWindowsAppRuntimeUnavailable(
        this ILogger logger, string requiredVersion, string installedBefore, string installedAfter)
        => logger.LogWindowsAppRuntimeUnavailableCore(Prefix, requiredVersion, installedBefore, installedAfter);

    /// <summary>Windows App Runtime を用意できたことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>Windows App Runtime を用意しました (required={RequiredVersion}, before={InstalledBefore},
    /// after={InstalledAfter}, initialized={Initialized})</c><br/>
    /// <b>成功も記録する理由</b>: 「既に入っていたのか」「今回導入したのか」が分からないと、環境の切り分けができない<br/>
    /// （実測 2026-11-09: Sandbox で導入に成功していたが記録が無く、環境側の問題と誤認した）。<br/>
    /// 自己完結の診断ビルドでは <c>before</c> / <c>after</c> が <c>(なし)</c> のまま初期化も実行されない
    /// （<c>initialized</c> が true で、同梱したランタイムで動いていることを表す）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="requiredVersion">必要とする Windows App Runtime の版（例: 2.6.4）。</param>
    /// <param name="installedBefore">用意する前のインストール済みの版（1 行）。</param>
    /// <param name="installedAfter">用意した後のインストール済みの版（1 行）。</param>
    /// <param name="initialized">Bootstrapper の初期化が済んでいる（または不要）かどうか。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix} Windows App Runtime を用意しました (required={RequiredVersion}, before={InstalledBefore}, after={InstalledAfter}, initialized={Initialized})")]
    private static partial void LogWindowsAppRuntimeReadyCore(
        this ILogger logger, string prefix, string requiredVersion,
        string installedBefore, string installedAfter, bool initialized);

    /// <summary>Windows App Runtime を用意できたことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="requiredVersion">必要とする Windows App Runtime の版（例: 2.6.4）。</param>
    /// <param name="installedBefore">用意する前のインストール済みの版（1 行）。</param>
    /// <param name="installedAfter">用意した後のインストール済みの版（1 行）。</param>
    /// <param name="initialized">Bootstrapper の初期化が済んでいる（または不要）かどうか。</param>
    public static void LogWindowsAppRuntimeReady(
        this ILogger logger, string requiredVersion, string installedBefore, string installedAfter, bool initialized)
        => logger.LogWindowsAppRuntimeReadyCore(Prefix, requiredVersion, installedBefore, installedAfter, initialized);

    // ====================================================================
    // 撮影セッション / 切出しセッションのログは撮影スタック（Parts.CapturePreview）が単一所有する
    // （CapturePreviewLogMessage。 接頭辞は [CapturePreview]）。
    // アプリ側で同じ操作を二重に記録しない（同じ操作のログに 2 つの所有者を作らない）。
    // ====================================================================

    /// <summary>設定変更の撮影セッションへの反映で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>設定変更の撮影セッション反映で例外が発生: {PropertyName}</c>（例外はスタックトレース込み）<br/>
    /// 設定の検証（範囲外の値）で例外になっても UI を壊さないため、反映は例外保護付きで行う。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} 設定変更の撮影セッション反映で例外が発生: {PropertyName}")]
    private static partial void LogSettingsApplyErrorCore(this ILogger logger, string prefix, Exception ex, string propertyName);

    /// <summary>設定変更の撮影セッションへの反映失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    public static void LogSettingsApplyError(this ILogger logger, Exception ex, string propertyName)
        => logger.LogSettingsApplyErrorCore(Prefix, ex, propertyName);

    /// <summary>終了時のウィンドウ配置の保存で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>ウィンドウ配置の保存で例外が発生</c>（例外はスタックトレース込み）<br/>
    /// 終了処理はウィンドウ メッセージの境界であり、例外がそのまま伝播すると CLR がプロセスを<br/>
    /// 異常終了させ、終了ログも保存結果も残らない。 保存に失敗しても終了は妨げない。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} ウィンドウ配置の保存で例外が発生")]
    private static partial void LogWindowPlacementSaveErrorCore(this ILogger logger, string prefix, Exception ex);

    /// <summary>ウィンドウ配置の保存失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    public static void LogWindowPlacementSaveError(this ILogger logger, Exception ex)
        => logger.LogWindowPlacementSaveErrorCore(Prefix, ex);

    // ====================================================================
    // フォルダビュー / Undo 一覧
    // ====================================================================

    /// <summary>ファイル一覧の読み込みで例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>一覧の読み込みで例外が発生: {FolderPath}</c>（例外はスタックトレース込み）<br/>
    /// 一覧が空になる原因（未解決のパス・アクセス拒否等）を追跡できるようにする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} 一覧の読み込みで例外が発生: {FolderPath}")]
    private static partial void LogFolderLoadErrorCore(this ILogger logger, string prefix, Exception ex, string folderPath);

    /// <summary>一覧の読み込み失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    public static void LogFolderLoadError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogFolderLoadErrorCore(Prefix, ex, folderPath);

    /// <summary>選択した画像をごみ箱へ移動できなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>画像をごみ箱へ移動できませんでした: {FilePath}</c>（例外はスタックトレース込み）<br/>
    /// 移動できなかったファイルは一覧に残したまま継続する。 Avalonia 版と同じ書式にする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 画像をごみ箱へ移動できませんでした: {FilePath}")]
    private static partial void LogFileDeleteErrorCore(this ILogger logger, string prefix, Exception ex, string filePath);

    public static void LogFileDeleteError(this ILogger logger, Exception ex, string filePath)
        => logger.LogFileDeleteErrorCore(Prefix, ex, filePath);

    /// <summary>選択した画像をクリップボードへコピーできなかったことを記録する。</summary>
    /// <remarks>LogLevel: Warning <br/>出力書式: <c>画像をクリップボードへコピーできませんでした: {FilePath}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 画像をクリップボードへコピーできませんでした: {FilePath}")]
    private static partial void LogClipboardCopyErrorCore(this ILogger logger, string prefix, Exception ex, string filePath);

    public static void LogClipboardCopyError(this ILogger logger, Exception ex, string filePath)
        => logger.LogClipboardCopyErrorCore(Prefix, ex, filePath);

    /// <summary>保存先フォルダをエクスプローラーで開けなかったことを記録する。</summary>
    /// <remarks>LogLevel: Warning <br/>出力書式: <c>エクスプローラーを起動できませんでした: {FolderPath}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} エクスプローラーを起動できませんでした: {FolderPath}")]
    private static partial void LogOpenFolderErrorCore(this ILogger logger, string prefix, Exception ex, string folderPath);

    public static void LogOpenFolderError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogOpenFolderErrorCore(Prefix, ex, folderPath);

    /// <summary>背景のサムネイル生成で想定外の例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>サムネイルの生成で例外が発生</c>（例外はスタックトレース込み）<br/>
    /// 生成は背景で行うため、失敗しても一覧の表示は継続する（握りつぶさず記録する）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} サムネイルの生成で例外が発生")]
    private static partial void LogThumbnailGenerateErrorCore(this ILogger logger, string prefix, Exception ex);

    /// <summary>サムネイル生成の失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    public static void LogThumbnailGenerateError(this ILogger logger, Exception ex)
        => logger.LogThumbnailGenerateErrorCore(Prefix, ex);

    /// <summary>ページの切り替えにかかった時間を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>ページ切り替え: {Page} 表示={ShowMs} ms / 活性化={ActivateMs} ms</c><br/>
    /// 切り替えの内訳（要素の可視性の切り替えと、ページの活性化（一覧の再構築など））を分けて残す。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="page">切り替え先のページ名。</param>
    /// <param name="showMs">要素の可視性の切り替えにかかった時間（ミリ秒）。</param>
    /// <param name="activateMs">ページの活性化（一覧の再構築など）にかかった時間（ミリ秒）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} ページ切り替え: {Page} 表示={ShowMs} ms / 活性化={ActivateMs} ms")]
    private static partial void LogPageSwitchCore(this ILogger logger, string prefix, string page, double showMs, double activateMs);

    /// <summary>ページの切り替え時間を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="page">切り替え先のページ名。</param>
    /// <param name="showMs">要素の可視性の切り替えにかかった時間（ミリ秒）。</param>
    /// <param name="activateMs">ページの活性化にかかった時間（ミリ秒）。</param>
    public static void LogPageSwitch(this ILogger logger, string page, double showMs, double activateMs)
        => logger.LogPageSwitchCore(Prefix, page, showMs, activateMs);

    /// <summary>ページの最初の描画フレームまでの時間を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>ページ描画: {Page} {RenderMs} ms</c><br/>
    /// 体感の遅さは描画（要素の構築と合成）に出るため、切り替えの処理時間とは別に残す。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="page">表示したページ名。</param>
    /// <param name="renderMs">最初の描画フレームまでの時間（ミリ秒）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} ページ描画: {Page} {RenderMs} ms")]
    private static partial void LogPageRenderedCore(this ILogger logger, string prefix, string page, double renderMs);

    /// <summary>ページの最初の描画フレームまでの時間を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="page">表示したページ名。</param>
    /// <param name="renderMs">最初の描画フレームまでの時間（ミリ秒）。</param>
    public static void LogPageRendered(this ILogger logger, string page, double renderMs)
        => logger.LogPageRenderedCore(Prefix, page, renderMs);

    /// <summary>起動後のサムネイルの先読みを開始したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>サムネイルの先読みを開始: {Target} 対象={Count} 件</c><br/>
    /// 起動後の 1 回だけ出力する（ページを開く前に低負荷で生成しておき、切り替えを待たせない）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} サムネイルの先読みを開始: {Target} 対象={Count} 件")]
    private static partial void LogThumbnailWarmUpCore(this ILogger logger, string prefix, string target, int count);

    /// <summary>サムネイルの先読み開始を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    public static void LogThumbnailWarmUp(this ILogger logger, string target, int count)
        => logger.LogThumbnailWarmUpCore(Prefix, target, count);

    // ====================================================================
    // 画像の保存とクリップボード
    // ====================================================================

    /// <summary>クリップボード操作で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>クリップボード操作で例外が発生: {Operation}</c>（例外はスタックトレース込み）<br/>
    /// クリップボードは他のプロセスと共有する資源であり、使用中に失敗し得る。<br/>
    /// 失敗してもアプリを継続し、利用者へは通知メッセージで知らせる。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="operation">操作の名前（コピー / 貼り付け）。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} クリップボード操作で例外が発生: {Operation}")]
    private static partial void LogClipboardErrorCore(this ILogger logger, string prefix, Exception ex, string operation);

    /// <summary>クリップボード操作の失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="operation">操作の名前（コピー / 貼り付け）。</param>
    public static void LogClipboardError(this ILogger logger, Exception ex, string operation)
        => logger.LogClipboardErrorCore(Prefix, ex, operation);

    // ====================================================================
    // 未処理の例外
    // ====================================================================

    /// <summary>未処理の例外（AppDomain / UI スレッド / TaskScheduler）を記録する。</summary>
    /// <remarks>
    /// LogLevel: Critical <br/>
    /// 出力書式: <c>未処理の例外: {Source}</c>（例外はスタックトレース込みで出力される）<br/>
    /// アプリがクラッシュする直前の例外を確実に記録する（原因特定用）。 他の UI 版と同じ書式にする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源（例: "AppDomain.UnhandledException"）。</param>
    [LoggerMessage(LogLevel.Critical, "{Prefix} 未処理の例外: {Source}")]
    private static partial void LogUnhandledExceptionCore(this ILogger logger, string prefix, Exception ex, string source);

    /// <summary>未処理の例外を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源。</param>
    public static void LogUnhandledException(this ILogger logger, Exception ex, string source)
        => logger.LogUnhandledExceptionCore(Prefix, ex, source);

    /// <summary>ホットキーを登録できなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>ホットキーを登録できませんでした: {CaptureType}, key={Key}</c><br/>
    /// 他のアプリが同じ組み合わせを使用している場合にも起こる（そのホットキーが機能しないだけでアプリは継続する）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="captureType">登録しようとしたキャプチャ種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} ホットキーを登録できませんでした: {CaptureType}, key={Key}")]
    private static partial void LogHotKeyRegisterFailedCore(this ILogger logger, string prefix, CaptureType captureType, EsKey key);

    /// <summary>ホットキーの登録失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">登録しようとしたキャプチャ種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    public static void LogHotKeyRegisterFailed(this ILogger logger, CaptureType captureType, EsKey key)
        => logger.LogHotKeyRegisterFailedCore(Prefix, captureType, key);

    /// <summary>ホットキーの受信を開始できなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>ホットキーの受信を開始できませんでした</c>（例外はスタックトレース込み）<br/>
    /// ウィンドウ プロシージャを差し替えられない場合に起こる。 ホットキーが使えないだけで<br/>
    /// 他の機能は動作するため、アプリを止めずに原因だけを残す。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} ホットキーの受信を開始できませんでした")]
    private static partial void LogHotKeyInitializeFailedCore(this ILogger logger, string prefix, Exception ex);

    /// <summary>ホットキーの受信開始の失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    public static void LogHotKeyInitializeFailed(this ILogger logger, Exception ex)
        => logger.LogHotKeyInitializeFailedCore(Prefix, ex);

    // ====================================================================
    // 保存（設定・画像）
    // ====================================================================

    /// <summary>設定の保存に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>設定を保存できませんでした: {FilePath}</c>（例外はスタックトレース込み）<br/>
    /// 設定は次回起動時の復元に使うため、書けないと「設定が保存されない」という症状だけが残る。<br/>
    /// 失敗しても操作は継続する（原因の追跡だけを目的とする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">保存しようとしたファイルのパス。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 設定を保存できませんでした: {FilePath}")]
    private static partial void LogSettingsSaveFailedCore(this ILogger logger, string prefix, Exception ex, string filePath);

    /// <summary>設定の保存失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">保存しようとしたファイルのパス。</param>
    public static void LogSettingsSaveFailed(this ILogger logger, Exception ex, string filePath)
        => logger.LogSettingsSaveFailedCore(Prefix, ex, filePath);

    /// <summary>画像の保存に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>画像を保存できませんでした: {FilePath}</c>（例外はスタックトレース込み）<br/>
    /// 保存の失敗はステータス バーの文言でしか利用者へ伝わらないため、原因（アクセス拒否・容量不足等）を<br/>
    /// ファイルへ残す。 失敗しても操作は継続する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">保存しようとしたファイルのパス（特定できない場合は空文字）。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 画像を保存できませんでした: {FilePath}")]
    private static partial void LogImageSaveFailedCore(this ILogger logger, string prefix, Exception ex, string filePath);

    /// <summary>画像の保存失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">保存しようとしたファイルのパス。</param>
    public static void LogImageSaveFailed(this ILogger logger, Exception ex, string filePath)
        => logger.LogImageSaveFailedCore(Prefix, ex, filePath);

    // ====================================================================
    // 環境情報の診断
    // ====================================================================

    /// <summary>実行環境（OS・ランタイム・表示経路・倍率・GPU）を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>環境情報: OS={OperatingSystem} / .NET={DotNet} / Windows App Runtime={RuntimeInstalled}
    /// (required={RuntimeRequired}) / セッション={SessionName} / クライアント={ClientName}
    /// / 仮想スクリーン={VirtualScreen} / ウィンドウ倍率={RasterizationScale} / モニター倍率={MonitorScales}
    /// / アダプター={Adapters}</c><br/>
    /// 描画の不具合はアプリの値ではなく環境（リモート セッション・GPU・倍率・ランタイムの版）で決まることがある。<br/>
    /// 実測 2026-11-08: Windows Sandbox（Indirect Display・VRAM 128MB・GPU パーティション）だけ文字が描かれず、<br/>
    /// ホスト機では正常だった。 実測 2026-11-10: アプリを 2.6.5 でビルドしながら 2.6.4 のランタイムで動くと<br/>
    /// 本文のグリフだけが描かれなかった。 この 1 行で環境の差と版の不一致を判別できるようにする。<br/>
    /// <b>起動ごとに 1 回だけ出す</b>（値は起動中に変わらない）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="operatingSystem">OS の版とアーキテクチャ。</param>
    /// <param name="dotNet">実行している .NET の版。</param>
    /// <param name="runtimeInstalled">インストール済みの Windows App Runtime の版（新しい順）。</param>
    /// <param name="runtimeRequired">このビルドが要求する Windows App Runtime の版。</param>
    /// <param name="runtimeLoaded">実際に読み込んでいる Windows App Runtime の版。</param>
    /// <param name="sessionName">セッション名（<c>SESSIONNAME</c>。 例: Console / RDP-Tcp#3）。</param>
    /// <param name="clientName">接続元の名前（<c>CLIENTNAME</c>。 ローカル実行では "(ローカル)"）。</param>
    /// <param name="virtualScreen">仮想スクリーンの矩形（物理ピクセル）。</param>
    /// <param name="rasterizationScale">ウィンドウの表示倍率（1.0 = 100%）。</param>
    /// <param name="monitorScales">仮想スクリーンの位置から解決したモニターの倍率。</param>
    /// <param name="adapters">表示アダプター（ドライバーの説明と VRAM）。 取得できない場合はその旨。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix} 環境情報: OS={OperatingSystem} / .NET={DotNet} / Windows App Runtime={RuntimeInstalled} (required={RuntimeRequired}, loaded={RuntimeLoaded}) / セッション={SessionName} / クライアント={ClientName} / 仮想スクリーン={VirtualScreen} / ウィンドウ倍率={RasterizationScale} / モニター倍率={MonitorScales} / アダプター={Adapters}")]
    private static partial void LogEnvironmentCore(
        this ILogger logger, string prefix, string operatingSystem, string dotNet,
        string runtimeInstalled, string runtimeRequired, string runtimeLoaded,
        string sessionName, string clientName,
        string virtualScreen, double rasterizationScale, string monitorScales, string adapters);

    /// <summary>要求する Windows App Runtime の版が指定されていないことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>要求する Windows App Runtime の版が指定されていません (library=…) — アプリは自分のビルド版を指定してください</c><br/>
    /// <b>未指定は本アセンブリ（EsUtil）のビルド版へフォールバックする</b>ため、アプリが EsUtil より新しい
    /// <c>Microsoft.WindowsAppSDK</c> でビルドされていると、アプリのマネージド アセンブリより古いネイティブ
    /// ランタイムで初期化が成功し得る（実測 2026-11-10: 本文のグリフだけが描かれなかった）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="libraryVersion">EsUtil のビルド版（フォールバック先）。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 要求する Windows App Runtime の版が指定されていません (library={LibraryVersion}) — アプリは自分のビルド版を指定してください")]
    private static partial void LogWindowsAppRuntimeVersionNotSpecifiedCore(
        this ILogger logger, string prefix, string libraryVersion);

    /// <summary>要求する Windows App Runtime の版が指定されていないことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="libraryVersion">EsUtil のビルド版（フォールバック先）。</param>
    public static void LogWindowsAppRuntimeVersionNotSpecified(this ILogger logger, string libraryVersion)
        => logger.LogWindowsAppRuntimeVersionNotSpecifiedCore(Prefix, libraryVersion);

    /// <summary>実行環境を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="operatingSystem">OS の版とアーキテクチャ。</param>
    /// <param name="dotNet">実行している .NET の版。</param>
    /// <param name="runtimeInstalled">インストール済みの Windows App Runtime の版。</param>
    /// <param name="runtimeRequired">要求する Windows App Runtime の版。</param>
    /// <param name="runtimeLoaded">実際に読み込んでいる Windows App Runtime の版。</param>
    /// <param name="sessionName">セッション名。</param>
    /// <param name="clientName">接続元の名前。</param>
    /// <param name="virtualScreen">仮想スクリーンの矩形。</param>
    /// <param name="rasterizationScale">ウィンドウの表示倍率。</param>
    /// <param name="monitorScales">モニターの倍率。</param>
    /// <param name="adapters">表示アダプター。</param>
    public static void LogEnvironment(
        this ILogger logger, string operatingSystem, string dotNet,
        string runtimeInstalled, string runtimeRequired, string runtimeLoaded,
        string sessionName, string clientName,
        string virtualScreen, double rasterizationScale, string monitorScales, string adapters)
        => logger.LogEnvironmentCore(
            Prefix, operatingSystem, dotNet, runtimeInstalled, runtimeRequired, runtimeLoaded,
            sessionName, clientName, virtualScreen, rasterizationScale, monitorScales, adapters);

    /// <summary>文字が描かれない環境かを切り分けるための値を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>文字の描画: 文字倍率={TextScaleFactor} / 実測={ProbeSize} / テーマ={Theme}</c><br/>
    /// <b>「レイアウトが 0 か」「描画されないだけか」を分けるため</b>: 実測は 14px の <c>TextBlock</c> を
    /// 1 行ぶん計測した大きさである（既定のフォントと <c>Segoe UI</c> の 2 通り）。 0 ならフォント解決
    /// （文字倍率・フォント）の問題、正常な大きさなのに見えないなら描画（ラスタライズ）の問題として切り分けられる。<br/>
    /// 実測 2026-11-09: Windows Sandbox で文字だけが描かれない現象の切り分けに使う。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="textScaleFactor">OS の文字の大きさ（1.0 = 100%）。</param>
    /// <param name="probeSize">計測した <c>TextBlock</c> の大きさ（フォントごとの幅x高さ。 装置非依存ピクセル）。</param>
    /// <param name="theme">実際に適用されているテーマ（Light / Dark）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} 文字の描画: 文字倍率={TextScaleFactor} / 実測={ProbeSize} / テーマ={Theme}")]
    private static partial void LogTextRenderProbeCore(
        this ILogger logger, string prefix, double textScaleFactor, string probeSize, string theme);

    /// <summary>文字が描かれない環境かを切り分けるための値を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="textScaleFactor">OS の文字の大きさ（1.0 = 100%）。</param>
    /// <param name="probeSize">計測した <c>TextBlock</c> の大きさ。</param>
    /// <param name="theme">実際に適用されているテーマ。</param>
    public static void LogTextRenderProbe(
        this ILogger logger, double textScaleFactor, string probeSize, string theme)
        => logger.LogTextRenderProbeCore(Prefix, textScaleFactor, probeSize, theme);
}

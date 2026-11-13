using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using EsUtil.SaveEvidence.App.Core.Logging;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する。
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EsUtil.SaveEvidence.App.WinForms.Logging;

/// <summary>App.WinForms プロジェクトのトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <see cref="LoggerMessageAttribute"/> による事前コンパイル済みログで、<br/>
/// 文字列補間による不要なアロケーションを回避しつつ、一貫したフォーマットで出力する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する。<br/>
/// 出力先は他の実装と同一の書式の Serilog 構成<br/>
/// （<c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.WinForms.&lt;yyyyMMdd&gt;.log</c>、日次ローテーション・7 日で自動削除）とし、<br/>
/// UI 版ごとにファイルを分けて追跡できるようにする。<br/>
/// ロガーは <see cref="CreateLogger"/> で生成し、各クラスの static フィールドで保持する。
/// </remarks>
internal static partial class AppLog
{
    /// <summary>接頭辞（固定。 アプリのログは DLL 名で読み分けるため、ホストからの設定対象外）。</summary>
    private const string Prefix = "[App.WinForms]";

    /// <summary>このアセンブリの名前（ログ ファイル名の先頭に使う）。</summary>
    /// <remarks>
    /// ログは実行ファイルごとに分かれる（UI 版の区別をファイル名で付ける）。 命名は
    /// <c>.github/命名規約.md</c>「ファイル」、出力先は <c>.github/ログ規約.md</c> §1.1 が定める。
    /// </remarks>
    private static readonly string s_assemblyName =
        typeof(AppLog).Assembly.GetName().Name ?? AppIdentity.ProductName;

    /// <summary>アプリ共有の Serilog ロガー（出力先と行の書式はコアの <c>AppLogging</c> が単一所有する）。</summary>
    private static readonly ILoggerFactory s_factory = AppLogging.CreateFactory(s_assemblyName);

    /// <summary>指定カテゴリ名のロガーを生成する。</summary>
    /// <param name="categoryName">ロガーカテゴリ名（例: "CapturePage"）。</param>
    /// <returns>生成したロガー。</returns>
    internal static ILogger CreateLogger(string categoryName) => s_factory.CreateLogger(categoryName);


    /// <summary>アプリケーション起動を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="version">アプリケーションのバージョン。</param>
    public static void LogAppStart(this ILogger logger, string version)
        => logger.LogAppStartCore(Prefix, version);


    /// <summary>アプリケーション終了を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="exitCode">終了コード。</param>
    public static void LogAppExit(this ILogger logger, int exitCode)
        => logger.LogAppExitCore(Prefix, exitCode);

    // ====================================================================
    // 撮影セッション / 切出しセッションのログは撮影スタック（Parts.CapturePreview）が単一所有する
    // （CapturePreviewLogMessage。 接頭辞は [CapturePreview]）。
    // アプリ側で同じ操作を二重に記録しない（同じ操作のログに 2 つの所有者を作らない）。
    // ====================================================================


    /// <summary>設定変更の反映失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    public static void LogSettingsApplyError(this ILogger logger, Exception ex, string propertyName)
        => logger.LogSettingsApplyErrorCore(Prefix, ex, propertyName);


    /// <summary>ウィンドウ配置の保存失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    public static void LogWindowPlacementSaveError(this ILogger logger, Exception ex)
        => logger.LogWindowPlacementSaveErrorCore(Prefix, ex);


    /// <summary>グローバル ホットキーの登録の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">登録しようとした撮影の種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    public static void LogHotKeyRegisterFailed(this ILogger logger, CaptureType captureType, EsKey key)
        => logger.LogHotKeyRegisterFailedCore(Prefix, captureType, key);

    // ====================================================================
    // フォルダビュー / Undo 一覧
    // ====================================================================


    /// <summary>一覧の読み込み失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    public static void LogFolderLoadError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogFolderLoadErrorCore(Prefix, ex, folderPath);


    /// <summary>選択した画像をごみ箱へ移動できなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    public static void LogFileDeleteError(this ILogger logger, Exception ex, string filePath)
        => logger.LogFileDeleteErrorCore(Prefix, ex, filePath);


    /// <summary>選択した画像をクリップボードへコピーできなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外（無い場合は null）。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    public static void LogClipboardCopyError(this ILogger logger, Exception ex, string filePath)
        => logger.LogClipboardCopyErrorCore(Prefix, ex, filePath);


    /// <summary>保存先フォルダをエクスプローラーで開けなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    public static void LogOpenFolderError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogOpenFolderErrorCore(Prefix, ex, folderPath);


    /// <summary>サムネイル生成の失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    public static void LogThumbnailGenerateError(this ILogger logger, Exception ex)
        => logger.LogThumbnailGenerateErrorCore(Prefix, ex);


    /// <summary>サムネイルの先読み開始を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    public static void LogThumbnailWarmUp(this ILogger logger, string target, int count)
        => logger.LogThumbnailWarmUpCore(Prefix, target, count);

    /// <summary>外部ツールによるファイル表示の失敗を記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>[App.WinForms] ファイルを外部ツールで表示できません: {FilePath}</c>（例外はスタックトレース込み）<br/>
    /// 関連付けが無い・対象が消えている等の失敗を追跡できるようにする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} ファイルを外部ツールで表示できません: {FilePath}")]
    private static partial void LogOpenFileErrorCore(this ILogger logger, string prefix, Exception ex, string filePath);

    /// <summary>外部ツールによる表示の失敗を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    public static void LogOpenFileError(this ILogger logger, Exception ex, string filePath)
        => logger.LogOpenFileErrorCore(Prefix, ex, filePath);

    // ====================================================================
    // 未処理の例外
    // ====================================================================


    /// <summary>未処理の例外を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源。</param>
    public static void LogUnhandledException(this ILogger logger, Exception ex, string source)
        => logger.LogUnhandledExceptionCore(Prefix, ex, source);
}

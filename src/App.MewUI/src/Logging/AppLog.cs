using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using EsUtil.SaveEvidence.App.Core.Logging;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する。
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EsUtil.SaveEvidence.App.MewUI.Logging;

/// <summary>App.MewUI プロジェクトのトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <see cref="LoggerMessageAttribute"/> による事前コンパイル済みログで、<br/>
/// 文字列補間による不要なアロケーションを回避しつつ、一貫したフォーマットで出力する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する。<br/>
/// 出力先は Avalonia 版と同一の書式の Serilog 構成（<c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.MewUI.&lt;yyyyMMdd&gt;.log</c>、<br/>
/// 日次ローテーション・7 日で自動削除）とし、UI 版ごとにファイルを分けて追跡できるようにする。<br/>
/// ロガーは <see cref="CreateLogger"/> で生成し、各クラスの static フィールドで保持する。
/// </remarks>
internal static partial class AppLog
{
    /// <summary>接頭辞（固定。 アプリのログは DLL 名で読み分けるため、ホストからの設定対象外）。</summary>
    private const string Prefix = "[App.MewUI]";

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
    /// <param name="categoryName">ロガーカテゴリ名（例: "CaptureOverlayWindow"）。</param>
    /// <returns>生成したロガー。</returns>
    internal static ILogger CreateLogger(string categoryName) => s_factory.CreateLogger(categoryName);

    // ====================================================================
    // アプリライフサイクル
    // ====================================================================


    /// <summary>アプリケーション起動を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="version">アプリケーションのバージョン。</param>
    public static void LogAppStart(this ILogger logger, string version)
        => logger.LogAppStartCore(Prefix, version);


    /// <summary>アプリケーション終了を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="exitCode">終了コード。</param>
    public static void LogAppExit(this ILogger logger, int exitCode)
        => logger.LogAppExitCore(Prefix, exitCode);

    // ====================================================================
    // 撮影セッション / 切出しセッションのログは撮影スタック（Parts.CapturePreview）が単一所有する
    // （CapturePreviewLogMessage。 接頭辞は [CapturePreview]）。
    // アプリ側で同じ操作を二重に記録しない（同じ操作のログに 2 つの所有者を作らない）。
    // ====================================================================


    /// <summary>設定変更の撮影セッションへの反映の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    public static void LogSettingsApplyError(this ILogger logger, Exception ex, string propertyName)
        => logger.LogSettingsApplyErrorCore(Prefix, ex, propertyName);


    /// <summary>終了時のウィンドウ配置の保存の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    public static void LogWindowPlacementSaveError(this ILogger logger, Exception ex)
        => logger.LogWindowPlacementSaveErrorCore(Prefix, ex);

    // ====================================================================
    // フォルダビュー
    // ====================================================================


    /// <summary>一覧のファイル列挙の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
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
    /// <param name="filePath">対象のファイルパス。</param>
    public static void LogClipboardCopyError(this ILogger logger, string filePath)
        => logger.LogClipboardCopyErrorCore(Prefix, null, filePath);


    /// <summary>履歴の画像をクリップボードへコピーできなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    public static void LogHistoryImageCopyError(this ILogger logger)
        => logger.LogHistoryImageCopyErrorCore(Prefix);


    /// <summary>保存先フォルダをエクスプローラーで開けなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    public static void LogOpenFolderError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogOpenFolderErrorCore(Prefix, ex, folderPath);


    /// <summary>背景のサムネイル生成の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    public static void LogThumbnailGenerateError(this ILogger logger, Exception ex)
        => logger.LogThumbnailGenerateErrorCore(Prefix, ex);


    /// <summary>サムネイルの先読みの開始を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    public static void LogThumbnailWarmUp(this ILogger logger, string target, int count)
        => logger.LogThumbnailWarmUpCore(Prefix, target, count);

    // ====================================================================
    // 未処理の例外
    // ====================================================================


    /// <summary>未処理の例外を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源。</param>
    public static void LogUnhandledException(this ILogger logger, Exception ex, string source)
        => logger.LogUnhandledExceptionCore(Prefix, ex, source);


    /// <summary>グローバル ホットキーの登録の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">登録しようとした撮影の種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    public static void LogHotKeyRegisterFailed(this ILogger logger, CaptureType captureType, EsKey key)
        => logger.LogHotKeyRegisterFailedCore(Prefix, captureType, key);
}

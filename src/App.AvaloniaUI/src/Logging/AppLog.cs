using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using EsUtil.SaveEvidence.App.Core.Logging;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する。
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Logging;

/// <summary>app プロジェクトのトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <see cref="LoggerMessageAttribute"/> による事前コンパイル済みログで、<br/>
/// 文字列補間による不要なアロケーションを回避しつつ、一貫したフォーマットで出力する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する（例: <c>_logger.LogZoomIn(current, target)</c>）。<br/>
/// 出力先は Serilog（<c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.AvaloniaUI.&lt;yyyyMMdd&gt;.log</c>、日次ローテーション・7日で自動削除）。<br/>
/// ロガーは <see cref="CreateLogger"/> で生成し、各クラスの static フィールドで保持する。<br/>
/// ログメッセージは日本語で記述する（英語は使用しない）。
/// </remarks>
internal static partial class AppLog
{
    /// <summary>接頭辞（固定。 アプリのログは DLL 名で読み分けるため、ホストからの設定対象外）。</summary>
    private const string Prefix = "[App.AvaloniaUI]";

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

    // ====================================================================
    // グローバル例外補足（ルート）・アプリライフサイクル
    // ====================================================================


    /// <summary>未処理の例外を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源。</param>
    internal static void LogUnhandledException(this ILogger logger, Exception ex, string source)
        => logger.LogUnhandledExceptionCore(Prefix, ex, source);

    /// <summary>例外が最初に投げられた時点を記録する（FirstChanceException）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">最初に投げられた例外。</param>
    /// <param name="message">記録する例外メッセージ（<paramref name="ex"/> の <c>Message</c>）。</param>
    /// <remarks>
    /// <b>注意</b>: すべての例外（握りつぶされるものも含む）が対象のため、ログ量が非常に多くなる。<br/>
    /// 次回の不具合調査時にのみ有効化することを推奨する（既定では「記録しない」フラグで制御する）。
    /// LogLevel: Trace<br/>
    /// 出力書式: <c>FirstChanceException: {message}</c>
    /// </remarks>
    [LoggerMessage(LogLevel.Trace, "{Prefix} FirstChanceException: {message}")]
    private static partial void LogFirstChanceExceptionCore(this ILogger logger, string prefix, Exception ex, string message);

    internal static void LogFirstChanceException(this ILogger logger, Exception ex, string message)
        => logger.LogFirstChanceExceptionCore(Prefix, ex, message);


    /// <summary>アプリケーション起動を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="version">アプリケーションのバージョン。</param>
    internal static void LogAppStart(this ILogger logger, string version)
        => logger.LogAppStartCore(Prefix, version);


    /// <summary>アプリケーション終了を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="exitCode">終了コード。</param>
    internal static void LogAppExit(this ILogger logger, int exitCode)
        => logger.LogAppExitCore(Prefix, exitCode);

    // ====================================================================
    // CapturePage（キャプチャメニュー）
    // ====================================================================

    /// <summary>キャプチャメニュー「全画面」のクリックを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>メニュー「全画面」クリック</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} メニュー「全画面」クリック")]
    private static partial void LogCaptureMenuScreenCore(this ILogger logger, string prefix);

    internal static void LogCaptureMenuScreen(this ILogger logger)
        => logger.LogCaptureMenuScreenCore(Prefix);

    /// <summary>キャプチャメニュー「ウィンドウ」のクリックを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>メニュー「ウィンドウ」クリック</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} メニュー「ウィンドウ」クリック")]
    private static partial void LogCaptureMenuWindowCore(this ILogger logger, string prefix);

    internal static void LogCaptureMenuWindow(this ILogger logger)
        => logger.LogCaptureMenuWindowCore(Prefix);

    /// <summary>キャプチャメニュー「範囲選択」のクリックを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>メニュー「範囲選択」クリック</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} メニュー「範囲選択」クリック")]
    private static partial void LogCaptureMenuAreaCore(this ILogger logger, string prefix);

    internal static void LogCaptureMenuArea(this ILogger logger)
        => logger.LogCaptureMenuAreaCore(Prefix);

    /// <summary>プレビュー上のポインタ押下（右クリック等）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="isRight">右ボタンかどうか。</param>
    /// <param name="hasCropTask">切出しセッション実行中かどうか。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>プレビューでポインタ押下: 右ボタン={isRight}, 切出し中={hasCropTask}</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} プレビューでポインタ押下: 右ボタン={isRight}, 切出し中={hasCropTask}")]
    private static partial void LogPreviewPointerPressedCore(this ILogger logger, string prefix, bool isRight, bool hasCropTask);

    internal static void LogPreviewPointerPressed(this ILogger logger, bool isRight, bool hasCropTask)
        => logger.LogPreviewPointerPressedCore(Prefix, isRight, hasCropTask);


    /// <summary>一覧のファイル列挙の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    internal static void LogFolderLoadError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogFolderLoadErrorCore(Prefix, ex, folderPath);


    /// <summary>選択した画像をごみ箱へ移動できなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    internal static void LogFileDeleteError(this ILogger logger, Exception ex, string filePath)
        => logger.LogFileDeleteErrorCore(Prefix, ex, filePath);


    /// <summary>選択した画像をクリップボードへコピーできなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    internal static void LogClipboardCopyError(this ILogger logger, string filePath)
        => logger.LogClipboardCopyErrorCore(Prefix, null, filePath);

    /// <summary>Fluent の確認ダイアログを表示できず、標準のダイアログへ切り替えたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <remarks>LogLevel: Warning<br/>出力書式: <c>確認ダイアログを表示できませんでした（標準のダイアログで表示します）</c></remarks>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 確認ダイアログを表示できませんでした（標準のダイアログで表示します）")]
    private static partial void LogConfirmDialogFallbackCore(this ILogger logger, string prefix, Exception ex);

    internal static void LogConfirmDialogFallback(this ILogger logger, Exception ex)
        => logger.LogConfirmDialogFallbackCore(Prefix, ex);

    /// <summary>履歴（Undo / Redo）の画像をクリップボードへコピーできなかったことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>LogLevel: Warning<br/>出力書式: <c>履歴の画像をクリップボードへコピーできませんでした</c><br/>
    /// 他のアプリがクリップボードを使用中の場合に起こる（操作は継続する）。</remarks>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 履歴の画像をクリップボードへコピーできませんでした")]
    private static partial void LogClipboardCopyUnavailableCore(this ILogger logger, string prefix);

    internal static void LogClipboardCopyUnavailable(this ILogger logger)
        => logger.LogClipboardCopyUnavailableCore(Prefix);


    /// <summary>保存先フォルダをエクスプローラーで開けなかったことを記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    internal static void LogOpenFolderError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogOpenFolderErrorCore(Prefix, ex, folderPath);

    /// <summary>サムネイルの復号で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>サムネイルの復号で例外が発生: {filePath}</c><br/>復号できない画像はサムネイルを出さないが、一覧は継続する（握りつぶさず記録する）。</remarks>
    [LoggerMessage(LogLevel.Debug, "{Prefix} サムネイルの復号で例外が発生: {filePath}")]
    private static partial void LogThumbnailLoadErrorCore(this ILogger logger, string prefix, Exception ex, string filePath);

    internal static void LogThumbnailLoadError(this ILogger logger, Exception ex, string filePath)
        => logger.LogThumbnailLoadErrorCore(Prefix, ex, filePath);


    /// <summary>背景のサムネイル生成の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    internal static void LogThumbnailGenerateError(this ILogger logger, Exception ex)
        => logger.LogThumbnailGenerateErrorCore(Prefix, ex);


    /// <summary>サムネイルの先読みの開始を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    internal static void LogThumbnailWarmUp(this ILogger logger, string target, int count)
        => logger.LogThumbnailWarmUpCore(Prefix, target, count);

    // ====================================================================
    // MainWindow（設定・ホットキー・キャプチャ開始）
    // ====================================================================


    /// <summary>設定変更の撮影セッションへの反映の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    internal static void LogSettingsApplyError(this ILogger logger, Exception ex, string propertyName)
        => logger.LogSettingsApplyErrorCore(Prefix, ex, propertyName);

    /// <summary>WM_HOTKEY メッセージを受信したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>WndProc: WM_HOTKEY received</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} WndProc: WM_HOTKEY received")]
    private static partial void LogHotKeyReceivedCore(this ILogger logger, string prefix);

    internal static void LogHotKeyReceived(this ILogger logger)
        => logger.LogHotKeyReceivedCore(Prefix);

    /// <summary>ホットキーに対応するキャプチャ種別が決定されたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="captureType">決定されたキャプチャ種別。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>WndProc: hotkey -> {captureType}</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} WndProc: hotkey -> {captureType}")]
    private static partial void LogHotKeyTriggeredCore(this ILogger logger, string prefix, CaptureType captureType);

    internal static void LogHotKeyTriggered(this ILogger logger, CaptureType captureType)
        => logger.LogHotKeyTriggeredCore(Prefix, captureType);

    /// <summary>ウィンドウがアクティブ化されたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>Activated</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} Activated")]
    private static partial void LogActivatedCore(this ILogger logger, string prefix);

    internal static void LogActivated(this ILogger logger)
        => logger.LogActivatedCore(Prefix);

    /// <summary>ウィンドウが非アクティブ化されたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>Deactivated</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} Deactivated")]
    private static partial void LogDeactivatedCore(this ILogger logger, string prefix);

    internal static void LogDeactivated(this ILogger logger)
        => logger.LogDeactivatedCore(Prefix);

    /// <summary>選択モード（キャプチャ）の開始を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="captureType">開始するキャプチャ種別。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>StartSelectionMode: {captureType}</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} StartSelectionMode: {captureType}")]
    private static partial void LogStartSelectionModeCore(this ILogger logger, string prefix, CaptureType captureType);

    internal static void LogStartSelectionMode(this ILogger logger, CaptureType captureType)
        => logger.LogStartSelectionModeCore(Prefix, captureType);

    /// <summary>キャプチャセッションの戻り値を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="imageInfo">キャプチャ画像サイズ（null の場合は "null"）。</param>
    /// <param name="bounds">キャプチャ元矩形（null の場合は "null"）。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>StartSelectionMode session returned bitmap={imageInfo}, bounds={bounds}</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} StartSelectionMode session returned bitmap={imageInfo}, bounds={bounds}")]
    private static partial void LogSessionReturnedCore(this ILogger logger, string prefix, string imageInfo, string bounds);

    internal static void LogSessionReturned(this ILogger logger, string imageInfo, string bounds)
        => logger.LogSessionReturnedCore(Prefix, imageInfo, bounds);

    /// <summary>キャプチャ開始で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="captureType">開始しようとしたキャプチャ種別。</param>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>StartSelectionMode で例外が発生: {captureType}</c></remarks>
    [LoggerMessage(LogLevel.Error, "{Prefix} StartSelectionMode で例外が発生: {captureType}")]
    private static partial void LogCaptureStartErrorCore(this ILogger logger, string prefix, Exception ex, CaptureType captureType);

    internal static void LogCaptureStartError(this ILogger logger, Exception ex, CaptureType captureType)
        => logger.LogCaptureStartErrorCore(Prefix, ex, captureType);


    /// <summary>グローバル ホットキーの登録の失敗を記録する。</summary>
    /// <remarks>文言は <see cref="AppLogMessage"/>、出力先と行の書式は <see cref="AppLogging"/> が単一所有する。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">登録しようとした撮影の種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    internal static void LogHotKeyRegisterFailed(this ILogger logger, CaptureType captureType, EsKey key)
        => logger.LogHotKeyRegisterFailedCore(Prefix, captureType, key);
}

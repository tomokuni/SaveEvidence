using System.Globalization;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;
using Serilog;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する。
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Logging;

/// <summary>app プロジェクトのトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <see cref="LoggerMessageAttribute"/> による事前コンパイル済みログで、<br/>
/// 文字列補間による不要なアロケーションを回避しつつ、一貫したフォーマットで出力する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する（例: <c>_logger.LogZoomIn(current, target)</c>）。<br/>
/// 出力先は Serilog（%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log、日次ローテーション・7日で自動削除）。<br/>
/// ロガーは <see cref="CreateLogger"/> で生成し、各クラスの static フィールドで保持する。<br/>
/// ログメッセージは日本語で記述する（英語は使用しない）。
/// </remarks>
internal static partial class AppLog
{
    /// <summary>接頭辞（固定。 アプリのログは DLL 名で読み分けるため、ホストからの設定対象外）。</summary>
    private const string Prefix = "[App.AvaloniaUI]";

    /// <summary>アプリケーション共有の Serilog ロガー（静的 <see cref="Log.Logger"/>）。</summary>
    /// <remarks>
    /// グローバル例外ハンドラ（<c>Log.Fatal</c>）と <c>ILoggerFactory</c>（MEL ブリッジ）の両方が<br/>
    /// 同一の Serilog パイプラインを共有するため、静的ロガーを初期化してからファクトリを構築する。
    /// </remarks>
    private static readonly ILoggerFactory s_factory = CreateFactory();

    /// <summary>Serilog ロガーを初期化し、MEL (Microsoft.Extensions.Logging) ブリッジのファクトリを構築する。</summary>
    /// <returns>Serilog へ転送する <see cref="ILoggerFactory"/>。</returns>
    /// <remarks>
    /// 静的 <see cref="Log.Logger"/> を設定し（<c>Log.Fatal</c> 等の Serilog 直接呼び出し用）、<br/>
    /// <c>AddSerilog()</c> がグローバルロガーを MEL へ転送する。<br/>
    /// 複数箇所で LoggerFactory.Create しないよう、本クラスに集約する（ログの二重出力防止）。
    /// </remarks>
    private static ILoggerFactory CreateFactory()
    {
        Log.Logger = CreateLoggingConfiguration().CreateLogger();
        return LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
    }

    /// <summary>Serilog のファイル出力構成を作成する。</summary>
    /// <returns>ログ出力を %LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log（日次ローテーション・7日保持）へ書き込む構成。</returns>
    /// <remarks>
    /// ログカテゴリは <c>{SourceContext}</c> として出力し、例外はスタックトレース込みで改行して書き出す。
    /// </remarks>
    private static LoggerConfiguration CreateLoggingConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.File(
                AppIdentity.LogFilePath,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                encoding: System.Text.Encoding.UTF8);
    }

    /// <summary>指定カテゴリ名のロガーを生成する。</summary>
    /// <param name="categoryName">ロガーカテゴリ名（例: "CapturePage"）。</param>
    /// <returns>生成したロガー。</returns>
    internal static ILogger CreateLogger(string categoryName) => s_factory.CreateLogger(categoryName);

    // ====================================================================
    // グローバル例外補足（ルート）・アプリライフサイクル
    // ====================================================================

    /// <summary>未処理の例外（AppDomain / UI スレッド / TaskScheduler）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源（例: "AppDomain.UnhandledException", "Dispatcher.UIThread"）。</param>
    /// <remarks>
    /// アプリがクラッシュする直前の例外を確実に記録する（クラッシュ原因の特定用）。<br/>
    /// <c>Fatal</c> レベルで記録し、スタックトレースを含む。
    /// LogLevel: Critical<br/>
    /// 出力書式: <c>未処理の例外: {source}</c>
    /// </remarks>
    [LoggerMessage(LogLevel.Critical, "{Prefix} 未処理の例外: {source}")]
    private static partial void LogUnhandledExceptionCore(this ILogger logger, string prefix, Exception ex, string source);

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

    /// <summary>アプリケーションの起動を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="version">アプリのバージョン。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>アプリケーション起動 (version={version})</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} アプリケーション起動 (version={version})")]
    private static partial void LogAppStartupCore(this ILogger logger, string prefix, string version);

    internal static void LogAppStartup(this ILogger logger, string version)
        => logger.LogAppStartupCore(Prefix, version);

    /// <summary>アプリケーションの終了を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="exitCode">終了コード。</param>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>アプリケーション終了 (exitCode={exitCode})</c></remarks>
    [LoggerMessage(LogLevel.Information, "{Prefix} アプリケーション終了 (exitCode={exitCode})")]
    private static partial void LogAppExitCore(this ILogger logger, string prefix, int exitCode);

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

    /// <summary>フォルダビューのファイル一覧読み込みで例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    /// LogLevel: Error<br/>
    /// 出力書式: <c>フォルダビュー一覧の読み込みで例外が発生: {folderPath}</c>
    /// <remarks>ナビゲーション中断（クラッシュ）を防ぐため、一覧は空にして継続する。</remarks>
    [LoggerMessage(LogLevel.Error, "{Prefix} フォルダビュー一覧の読み込みで例外が発生: {folderPath}")]
    private static partial void LogFolderViewLoadErrorCore(this ILogger logger, string prefix, Exception ex, string folderPath);

    internal static void LogFolderViewLoadError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogFolderViewLoadErrorCore(Prefix, ex, folderPath);

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

    /// <summary>背景のサムネイル生成で想定外の例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <remarks>LogLevel: Warning<br/>出力書式: <c>サムネイルの生成で例外が発生</c><br/>
    /// 生成は背景で行うため、失敗しても一覧の表示は継続する（握りつぶさず記録する。 対象のファイルは
    /// 個別の復号失敗として <see cref="LogThumbnailLoadError"/> が記録する）。</remarks>
    [LoggerMessage(LogLevel.Warning, "{Prefix} サムネイルの生成で例外が発生")]
    private static partial void LogThumbnailGenerateErrorCore(this ILogger logger, string prefix, Exception ex);

    internal static void LogThumbnailGenerateError(this ILogger logger, Exception ex)
        => logger.LogThumbnailGenerateErrorCore(Prefix, ex);

    /// <summary>起動後のサムネイルの先読みを開始したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>サムネイルの先読みを開始: {target} 対象={count} 件</c><br/>
    /// 起動後の 1 回だけ出力する（ページを開く前に低負荷で生成しておき、切り替えを待たせない）。
    /// 起動が遅くなっていないことは、この記録の時刻が起動ログより後であることで確認できる。</remarks>
    [LoggerMessage(LogLevel.Debug, "{Prefix} サムネイルの先読みを開始: {target} 対象={count} 件")]
    private static partial void LogThumbnailWarmUpCore(this ILogger logger, string prefix, string target, int count);

    internal static void LogThumbnailWarmUp(this ILogger logger, string target, int count)
        => logger.LogThumbnailWarmUpCore(Prefix, target, count);

    // ====================================================================
    // MainWindow（設定・ホットキー・キャプチャ開始）
    // ====================================================================

    /// <summary>設定変更の CaptureSessionControl 反映で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>設定変更の CaptureSessionControl 反映で例外が発生: {propertyName}</c></remarks>
    [LoggerMessage(LogLevel.Error, "{Prefix} 設定変更の CaptureSessionControl 反映で例外が発生: {propertyName}")]
    private static partial void LogSettingsApplyErrorCore(this ILogger logger, string prefix, Exception ex, string propertyName);

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

    /// <summary>ホットキーを登録できなかったことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="captureType">登録しようとしたキャプチャ種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    /// <remarks>
    /// LogLevel: Warning<br/>
    /// 出力書式: <c>ホットキーを登録できませんでした: {captureType}, key={key}</c><br/>
    /// 他のアプリが同じ組み合わせを使用している場合にも起こる（そのホットキーが機能しないだけでアプリは継続する）。
    /// </remarks>
    [LoggerMessage(LogLevel.Warning, "{Prefix} ホットキーを登録できませんでした: {captureType}, key={key}")]
    private static partial void LogHotKeyRegisterFailedCore(this ILogger logger, string prefix, CaptureType captureType, EsKey key);

    internal static void LogHotKeyRegisterFailed(this ILogger logger, CaptureType captureType, EsKey key)
        => logger.LogHotKeyRegisterFailedCore(Prefix, captureType, key);
}

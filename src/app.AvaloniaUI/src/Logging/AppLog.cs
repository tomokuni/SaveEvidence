using Estable.Platform;
using Microsoft.Extensions.Logging;
using Serilog;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する。
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Estable.SaveEvidence.Logging;

/// <summary>
/// app プロジェクトのトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。
/// </summary>
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
    /// <summary>
    /// アプリケーション共有の Serilog ロガー（静的 <see cref="Log.Logger"/>）。
    /// </summary>
    /// <remarks>
    /// グローバル例外ハンドラ（<c>Log.Fatal</c>）と <c>ILoggerFactory</c>（MEL ブリッジ）の両方が<br/>
    /// 同一の Serilog パイプラインを共有するため、静的ロガーを初期化してからファクトリを構築する。
    /// </remarks>
    private static readonly ILoggerFactory Factory = CreateFactory();

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
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SaveEvidence",
            "logs");

        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDir, "SaveEvidence.log"),
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                encoding: System.Text.Encoding.UTF8);
    }

    /// <summary>指定カテゴリ名のロガーを生成する。</summary>
    /// <param name="categoryName">ロガーカテゴリ名（例: "CapturePage"）。</param>
    /// <returns>生成したロガー。</returns>
    internal static ILogger CreateLogger(string categoryName) => Factory.CreateLogger(categoryName);

    // ====================================================================
    // グローバル例外補足（ルート）・アプリライフサイクル
    // ====================================================================

    /// <summary>未処理の例外（AppDomain / UI スレッド / TaskScheduler）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源（例: "AppDomain.UnhandledException", "Dispatcher.UIThread"）。</param>
    /// <remarks>
    /// アプリがクラッシュする直前の例外を確実に記録する（クラッシュ原因の特定用）。<br/>
    /// <c>Fatal</c> レベルで記録し、スタックトレースを含む。
    /// </remarks>
    [LoggerMessage(LogLevel.Critical, "未処理の例外: {source}")]
    internal static partial void LogUnhandledException(this ILogger logger, Exception ex, string source);

    /// <summary>例外が最初に投げられた時点を記録する（FirstChanceException）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="ex">最初に投げられた例外。</param>
    /// <remarks>
    /// <b>注意</b>: すべての例外（握りつぶされるものも含む）が対象のため、ログ量が非常に多くなる。<br/>
    /// 次回の不具合調査時にのみ有効化することを推奨する（既定では「記録しない」フラグで制御する）。
    /// </remarks>
    [LoggerMessage(LogLevel.Trace, "FirstChanceException: {message}")]
    internal static partial void LogFirstChanceException(this ILogger logger, Exception ex, string message);

    /// <summary>アプリケーションの起動を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="version">アプリのバージョン。</param>
    [LoggerMessage(LogLevel.Information, "アプリケーション起動 (version={version})")]
    internal static partial void LogAppStartup(this ILogger logger, string version);

    /// <summary>アプリケーションの終了を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="exitCode">終了コード。</param>
    [LoggerMessage(LogLevel.Information, "アプリケーション終了 (exitCode={exitCode})")]
    internal static partial void LogAppExit(this ILogger logger, int exitCode);

    // ====================================================================
    // CapturePage（キャプチャメニュー）
    // ====================================================================

    /// <summary>キャプチャメニュー「全画面」のクリックを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Information, "メニュー「全画面」クリック")]
    internal static partial void LogCaptureMenuScreen(this ILogger logger);

    /// <summary>キャプチャメニュー「ウィンドウ」のクリックを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Information, "メニュー「ウィンドウ」クリック")]
    internal static partial void LogCaptureMenuWindow(this ILogger logger);

    /// <summary>キャプチャメニュー「範囲選択」のクリックを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Information, "メニュー「範囲選択」クリック")]
    internal static partial void LogCaptureMenuArea(this ILogger logger);

    /// <summary>プレビュー上のポインタ押下（右クリック等）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="isRight">右ボタンかどうか。</param>
    /// <param name="hasCropTask">切出しセッション実行中かどうか。</param>
    [LoggerMessage(LogLevel.Information, "プレビューでポインタ押下: 右ボタン={isRight}, 切出し中={hasCropTask}")]
    internal static partial void LogPreviewPointerPressed(this ILogger logger, bool isRight, bool hasCropTask);

    /// <summary>フォルダビューのファイル一覧読み込みで例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    /// <remarks>ナビゲーション中断（クラッシュ）を防ぐため、一覧は空にして継続する。</remarks>
    [LoggerMessage(LogLevel.Error, "フォルダビュー一覧の読み込みで例外が発生: {folderPath}")]
    internal static partial void LogFolderViewLoadError(this ILogger logger, Exception ex, string folderPath);

    // ====================================================================
    // MainWindow（設定・ホットキー・キャプチャ開始）
    // ====================================================================

    /// <summary>設定変更の CaptureSessionControl 反映で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    [LoggerMessage(LogLevel.Error, "設定変更の CaptureSessionControl 反映で例外が発生: {propertyName}")]
    internal static partial void LogSettingsApplyError(this ILogger logger, Exception ex, string propertyName);

    /// <summary>WM_HOTKEY メッセージを受信したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Information, "WndProc: WM_HOTKEY received")]
    internal static partial void LogHotKeyReceived(this ILogger logger);

    /// <summary>ホットキーに対応するキャプチャ種別が決定されたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="captureType">決定されたキャプチャ種別。</param>
    [LoggerMessage(LogLevel.Information, "WndProc: hotkey -> {captureType}")]
    internal static partial void LogHotKeyTriggered(this ILogger logger, CaptureType captureType);

    /// <summary>ウィンドウがアクティブ化されたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Information, "Activated")]
    internal static partial void LogActivated(this ILogger logger);

    /// <summary>ウィンドウが非アクティブ化されたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Information, "Deactivated")]
    internal static partial void LogDeactivated(this ILogger logger);

    /// <summary>選択モード（キャプチャ）の開始を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="captureType">開始するキャプチャ種別。</param>
    [LoggerMessage(LogLevel.Information, "StartSelectionMode: {captureType}")]
    internal static partial void LogStartSelectionMode(this ILogger logger, CaptureType captureType);

    /// <summary>キャプチャセッションの戻り値を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="imageInfo">キャプチャ画像サイズ（null の場合は "null"）。</param>
    /// <param name="bounds">キャプチャ元矩形（null の場合は "null"）。</param>
    [LoggerMessage(LogLevel.Information, "StartSelectionMode session returned bitmap={imageInfo}, bounds={bounds}")]
    internal static partial void LogSessionReturned(this ILogger logger, string imageInfo, string bounds);
}

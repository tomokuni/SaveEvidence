using Microsoft.Extensions.Logging;
using Serilog;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する。
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EsUtil.SaveEvidence.App.MewUI.Logging;

/// <summary>App.MewUI プロジェクトのトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <see cref="LoggerMessageAttribute"/> による事前コンパイル済みログで、<br/>
/// 文字列補間による不要なアロケーションを回避しつつ、一貫したフォーマットで出力する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する。<br/>
/// 出力先は Avalonia 版と同一の Serilog 構成（<c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log</c>、<br/>
/// 日次ローテーション・7 日で自動削除）とし、UI が違っても同じログで追跡できるようにする。<br/>
/// ロガーは <see cref="CreateLogger"/> で生成し、各クラスの static フィールドで保持する。
/// </remarks>
internal static partial class AppLog
{
    /// <summary>アプリケーション共有の Serilog ロガー（静的 <see cref="Log.Logger"/>）。</summary>
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
    /// 複数箇所で <c>LoggerFactory.Create</c> しないよう、本クラスに集約する（ログの二重出力防止）。
    /// </remarks>
    private static ILoggerFactory CreateFactory()
    {
        Log.Logger = CreateLoggingConfiguration().CreateLogger();
        return LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
    }

    /// <summary>Serilog のファイル出力構成を作成する。</summary>
    /// <returns>ログ出力を <c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log</c>（日次ローテーション・7 日保持）へ書き込む構成。</returns>
    /// <remarks>
    /// ログカテゴリは <c>{SourceContext}</c> として出力し、例外はスタックトレース込みで改行して書き出す。<br/>
    /// 書式は Avalonia 版と同一にする（同じログファイルを両 UI で共有するため）。
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
    /// <param name="categoryName">ロガーカテゴリ名（例: "CaptureOverlayWindow"）。</param>
    /// <returns>生成したロガー。</returns>
    internal static ILogger CreateLogger(string categoryName) => Factory.CreateLogger(categoryName);

    // ====================================================================
    // アプリライフサイクル
    // ====================================================================

    /// <summary>アプリケーション起動のログ。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>アプリケーション起動 (version={Version})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="version">アプリケーションのバージョン。</param>
    [LoggerMessage(LogLevel.Information, "アプリケーション起動 (version={Version})")]
    public static partial void LogAppStart(this ILogger logger, string version);

    /// <summary>アプリケーション終了のログ。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>アプリケーション終了 (exitCode={ExitCode})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exitCode">終了コード。</param>
    [LoggerMessage(LogLevel.Information, "アプリケーション終了 (exitCode={ExitCode})")]
    public static partial void LogAppExit(this ILogger logger, int exitCode);

    // ====================================================================
    // 撮影セッション
    // ====================================================================

    /// <summary>撮影セッション開始のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>撮影開始: type={Type}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="type">撮影種別。</param>
    [LoggerMessage(LogLevel.Debug, "撮影開始: type={Type}")]
    public static partial void LogCaptureStart(this ILogger logger, string type);

    /// <summary>撮影確定のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>撮影確定: rect={Rect}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="rect">確定した矩形（画面物理ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "撮影確定: rect={Rect}")]
    public static partial void LogCaptureCompleted(this ILogger logger, string rect);

    /// <summary>撮影キャンセルのログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>撮影キャンセル</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "撮影キャンセル")]
    public static partial void LogCaptureCancelled(this ILogger logger);

    // ====================================================================
    // 切出しセッション
    // ====================================================================

    /// <summary>切出しセッション開始のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>切出し開始: image={Width}x{Height}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="width">元画像の幅（ピクセル）。</param>
    /// <param name="height">元画像の高さ（ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "切出し開始: image={Width}x{Height}")]
    public static partial void LogCropStart(this ILogger logger, int width, int height);

    /// <summary>切出し確定のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>切出し確定: rect={Rect}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="rect">切り出す矩形（画像ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "切出し確定: rect={Rect}")]
    public static partial void LogCropCompleted(this ILogger logger, string rect);

    /// <summary>切出しキャンセルのログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>切出しキャンセル</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "切出しキャンセル")]
    public static partial void LogCropCancelled(this ILogger logger);

    /// <summary>設定変更の CaptureSessionControl 反映で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>設定変更の CaptureSessionControl 反映で例外が発生: {PropertyName}</c>（例外はスタックトレース込み）<br/>
    /// 設定の検証（範囲外の値）で例外になっても UI を壊さないため、反映は例外保護付きで行う（Avalonia 版と同じ書式）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    [LoggerMessage(LogLevel.Error, "設定変更の CaptureSessionControl 反映で例外が発生: {PropertyName}")]
    public static partial void LogSettingsApplyError(this ILogger logger, Exception ex, string propertyName);

    // ====================================================================
    // フォルダビュー
    // ====================================================================

    /// <summary>フォルダビューのファイル一覧の読み込みで例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>フォルダビュー一覧の読み込みで例外が発生: {FolderPath}</c>（例外はスタックトレース込み）<br/>
    /// 一覧が空になる原因（未解決のパス・アクセス拒否等）を追跡できるようにする。 Avalonia 版と同じ書式にする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    [LoggerMessage(LogLevel.Error, "フォルダビュー一覧の読み込みで例外が発生: {FolderPath}")]
    public static partial void LogFolderLoadError(this ILogger logger, Exception ex, string folderPath);

    // ====================================================================
    // 未処理の例外
    // ====================================================================

    /// <summary>未処理の例外（AppDomain / UI スレッド / TaskScheduler）を記録する。</summary>
    /// <remarks>
    /// LogLevel: Critical <br/>
    /// 出力書式: <c>未処理の例外: {Source}</c>（例外はスタックトレース込みで出力される）<br/>
    /// アプリがクラッシュする直前の例外を確実に記録する（原因特定用）。 Avalonia 版と同じ書式にする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源（例: "AppDomain.UnhandledException"）。</param>
    [LoggerMessage(LogLevel.Critical, "未処理の例外: {Source}")]
    public static partial void LogUnhandledException(this ILogger logger, Exception ex, string source);
}

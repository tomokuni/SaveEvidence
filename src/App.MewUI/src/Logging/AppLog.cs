using System.Globalization;
using EsUtil.Platform.Core;
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
    /// <summary>接頭辞（固定。 アプリのログは DLL 名で読み分けるため、ホストからの設定対象外）。</summary>
    private const string Prefix = "[App.MewUI]";

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
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                encoding: System.Text.Encoding.UTF8);
    }

    /// <summary>指定カテゴリ名のロガーを生成する。</summary>
    /// <param name="categoryName">ロガーカテゴリ名（例: "CaptureOverlayWindow"）。</param>
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

    public static void LogAppExit(this ILogger logger, int exitCode)
        => logger.LogAppExitCore(Prefix, exitCode);

    // ====================================================================
    // 撮影セッション / 切出しセッションのログは撮影スタック（Parts.CapturePreview）が単一所有する
    // （CapturePreviewLogMessage。 接頭辞は [CapturePreview]）。
    // アプリ側で同じ操作を二重に記録しない（同じ操作のログに 2 つの所有者を作らない）。
    // ====================================================================

    /// <summary>設定変更の CaptureSessionControl 反映で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>設定変更の CaptureSessionControl 反映で例外が発生: {PropertyName}</c>（例外はスタックトレース込み）<br/>
    /// 設定の検証（範囲外の値）で例外になっても UI を壊さないため、反映は例外保護付きで行う（Avalonia 版と同じ書式）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} 設定変更の CaptureSessionControl 反映で例外が発生: {PropertyName}")]
    private static partial void LogSettingsApplyErrorCore(this ILogger logger, string prefix, Exception ex, string propertyName);

    public static void LogSettingsApplyError(this ILogger logger, Exception ex, string propertyName)
        => logger.LogSettingsApplyErrorCore(Prefix, ex, propertyName);

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
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} フォルダビュー一覧の読み込みで例外が発生: {FolderPath}")]
    private static partial void LogFolderLoadErrorCore(this ILogger logger, string prefix, Exception ex, string folderPath);

    public static void LogFolderLoadError(this ILogger logger, Exception ex, string folderPath)
        => logger.LogFolderLoadErrorCore(Prefix, ex, folderPath);

    /// <summary>背景のサムネイル生成で想定外の例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>サムネイルの生成で例外が発生</c>（例外はスタックトレース込み）<br/>
    /// 生成は背景で行うため、失敗しても一覧の表示は継続する（握りつぶさず記録する）。 Avalonia 版と同じ書式にする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} サムネイルの生成で例外が発生")]
    private static partial void LogThumbnailGenerateErrorCore(this ILogger logger, string prefix, Exception ex);

    public static void LogThumbnailGenerateError(this ILogger logger, Exception ex)
        => logger.LogThumbnailGenerateErrorCore(Prefix, ex);

    /// <summary>起動後のサムネイルの先読みを開始したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>サムネイルの先読みを開始: {Target} 対象={Count} 件</c><br/>
    /// 起動後の 1 回だけ出力する（ページを開く前に低負荷で生成しておき、切り替えを待たせない）。
    /// 起動が遅くなっていないことは、この記録の時刻が起動ログより後であることで確認できる。 Avalonia 版と同じ書式にする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} サムネイルの先読みを開始: {Target} 対象={Count} 件")]
    private static partial void LogThumbnailWarmUpCore(this ILogger logger, string prefix, string target, int count);

    public static void LogThumbnailWarmUp(this ILogger logger, string target, int count)
        => logger.LogThumbnailWarmUpCore(Prefix, target, count);

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
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源（例: "AppDomain.UnhandledException"）。</param>
    [LoggerMessage(LogLevel.Critical, "{Prefix} 未処理の例外: {Source}")]
    private static partial void LogUnhandledExceptionCore(this ILogger logger, string prefix, Exception ex, string source);

    public static void LogUnhandledException(this ILogger logger, Exception ex, string source)
        => logger.LogUnhandledExceptionCore(Prefix, ex, source);

    /// <summary>ホットキーを登録できなかったことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="captureType">登録しようとしたキャプチャ種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    /// <remarks>
    /// LogLevel: Warning<br/>
    /// 出力書式: <c>ホットキーを登録できませんでした: {CaptureType}, key={Key}</c><br/>
    /// 他のアプリが同じ組み合わせを使用している場合にも起こる（そのホットキーが機能しないだけでアプリは継続する）。
    /// </remarks>
    [LoggerMessage(LogLevel.Warning, "{Prefix} ホットキーを登録できませんでした: {CaptureType}, key={Key}")]
    private static partial void LogHotKeyRegisterFailedCore(this ILogger logger, string prefix, CaptureType captureType, EsKey key);

    public static void LogHotKeyRegisterFailed(this ILogger logger, CaptureType captureType, EsKey key)
        => logger.LogHotKeyRegisterFailedCore(Prefix, captureType, key);
}

using Estable.AvaloniaUI.SnipCaptureUI;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace app.Logging;

/// <summary>
/// app プロジェクトのトレースログ定義（LoggerMessage ソース生成）。
/// </summary>
/// <remarks>
/// <see cref="LoggerMessageAttribute"/> による事前コンパイル済みログで、<br/>
/// 文字列補間による不要なアロケーションを回避しつつ、一貫したフォーマットで出力する。<br/>
/// 出力先は NLog（%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log、7日で自動アーカイブ・削除）。<br/>
/// ロガーは <see cref="CreateLogger"/> で生成し、各クラスの static フィールドで保持する。<br/>
/// ログメッセージは日本語で記述する（英語は使用しない）。
/// </remarks>
internal static partial class AppLog
{
    /// <summary>
    /// アプリケーション共有のロガーファクトリ（NLog へ転送）。
    /// </summary>
    /// <remarks>
    /// AddNLog() が nlog.config を自動読み込みするため、コード内の設定は不要。<br/>
    /// 複数箇所で LoggerFactory.Create しないよう、本クラスに集約する（ログの二重出力防止）。
    /// </remarks>
    private static readonly ILoggerFactory Factory = LoggerFactory.Create(builder => builder.AddNLog());

    /// <summary>指定カテゴリ名のロガーを生成する。</summary>
    /// <param name="categoryName">ロガーカテゴリ名（例: "ZoomManager"）。</param>
    /// <returns>生成したロガー。</returns>
    internal static ILogger CreateLogger(string categoryName) => Factory.CreateLogger(categoryName);

    // ====================================================================
    // ZoomManager（ズーム操作）
    // ====================================================================

    /// <summary>ズームイン（ボタン）の実行前後を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomIn] {current}% -> {target}%")]
    internal static partial void LogZoomIn(ILogger logger, int current, int target);

    /// <summary>ズームアウト（ボタン）の実行前後を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomOut] {current}% -> {target}%")]
    internal static partial void LogZoomOut(ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールズームインの実行前後を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomInWheel] {current}% -> {target}%")]
    internal static partial void LogZoomInWheel(ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールズームアウトの実行前後を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomOutWheel] {current}% -> {target}%")]
    internal static partial void LogZoomOutWheel(ILogger logger, int current, int target);

    // ====================================================================
    // CapturePage（キャプチャメニュー）
    // ====================================================================

    /// <summary>キャプチャメニュー「全画面」のクリックを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Information, "メニュー「全画面」クリック")]
    internal static partial void LogCaptureMenuScreen(ILogger logger);

    /// <summary>キャプチャメニュー「ウィンドウ」のクリックを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Information, "メニュー「ウィンドウ」クリック")]
    internal static partial void LogCaptureMenuWindow(ILogger logger);

    /// <summary>キャプチャメニュー「範囲選択」のクリックを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Information, "メニュー「範囲選択」クリック")]
    internal static partial void LogCaptureMenuArea(ILogger logger);

    /// <summary>プレビュー上のポインタ押下（右クリック等）を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="isRight">右ボタンかどうか。</param>
    /// <param name="hasCropTask">切出しセッション実行中かどうか。</param>
    [LoggerMessage(LogLevel.Information, "プレビューでポインタ押下: 右ボタン={isRight}, 切出し中={hasCropTask}")]
    internal static partial void LogPreviewPointerPressed(ILogger logger, bool isRight, bool hasCropTask);

    // ====================================================================
    // MainWindow（設定・ホットキー・キャプチャ開始）
    // ====================================================================

    /// <summary>設定変更の SnipCapture 反映で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    [LoggerMessage(LogLevel.Error, "設定変更の SnipCapture 反映で例外が発生: {propertyName}")]
    internal static partial void LogSettingsApplyError(ILogger logger, Exception ex, string propertyName);

    /// <summary>WM_HOTKEY メッセージを受信したことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Information, "WndProc: WM_HOTKEY received")]
    internal static partial void LogHotKeyReceived(ILogger logger);

    /// <summary>ホットキーに対応するキャプチャ種別が決定されたことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">決定されたキャプチャ種別。</param>
    [LoggerMessage(LogLevel.Information, "WndProc: hotkey -> {captureType}")]
    internal static partial void LogHotKeyTriggered(ILogger logger, CaptureType captureType);

    /// <summary>ウィンドウがアクティブ化されたことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Information, "Activated")]
    internal static partial void LogActivated(ILogger logger);

    /// <summary>ウィンドウが非アクティブ化されたことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Information, "Deactivated")]
    internal static partial void LogDeactivated(ILogger logger);

    /// <summary>選択モード（キャプチャ）の開始を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">開始するキャプチャ種別。</param>
    [LoggerMessage(LogLevel.Information, "StartSelectionMode: {captureType}")]
    internal static partial void LogStartSelectionMode(ILogger logger, CaptureType captureType);

    /// <summary>キャプチャセッションの戻り値を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="imageInfo">キャプチャ画像サイズ（null の場合は "null"）。</param>
    /// <param name="bounds">キャプチャ元矩形（null の場合は "null"）。</param>
    [LoggerMessage(LogLevel.Information, "StartSelectionMode session returned bitmap={imageInfo}, bounds={bounds}")]
    internal static partial void LogSessionReturned(ILogger logger, string imageInfo, string bounds);
}

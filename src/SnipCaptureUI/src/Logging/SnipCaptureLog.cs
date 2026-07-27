using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.SnipCaptureUI.Logging;

/// <summary>SnipCaptureUI の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// キャプチャセッション（開始・完了・確定・キャンセル）と Crop セッションのログを出力する。<br/>
/// マウス操作・キー操作・確定/キャンセルをトレースログで記録し、挙動解析に利用する。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class SnipCaptureLog
{
    /// <summary>キャプチャセッション開始のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] CaptureStart: type={Type}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="type">キャプチャ種別。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] CaptureStart: type={Type}")]
    public static partial void LogCaptureStart(ILogger logger, string type);

    /// <summary>キャプチャ完了（確定）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] CaptureCompleted: bounds={Bounds}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="bounds">キャプチャ矩形（画面物理座標）。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] CaptureCompleted: bounds={Bounds}")]
    public static partial void LogCaptureCompleted(ILogger logger, string bounds);

    /// <summary>キャプチャキャンセルのログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] CaptureCancelled</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] CaptureCancelled")]
    public static partial void LogCaptureCancelled(ILogger logger);

    /// <summary>キャプチャ結果サイズのログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] Result: bitmap={Width}x{Height} bounds={Bounds}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="width">キャプチャ画像幅。</param>
    /// <param name="height">キャプチャ画像高さ。</param>
    /// <param name="bounds">キャプチャ矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] Result: bitmap={Width}x{Height} bounds={Bounds}")]
    public static partial void LogResult(ILogger logger, int width, int height, string bounds);

    /// <summary>オーバーレイの確定要求（Enter キー・ダブルクリック等）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] OverlayConfirm: rect={Rect}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] OverlayConfirm: rect={Rect}")]
    public static partial void LogOverlayConfirm(ILogger logger, string rect);

    /// <summary>オーバーレイのキャンセル要求（Esc キー・ダブルクリック外等）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] OverlayCancel</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] OverlayCancel")]
    public static partial void LogOverlayCancel(ILogger logger);

    /// <summary>オーバーレイの仮想スクリーン設定（キャプチャ完了）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">仮想スクリーン原点 X。</param>
    /// <param name="y">仮想スクリーン原点 Y。</param>
    /// <param name="width">仮想スクリーン幅。</param>
    /// <param name="height">仮想スクリーン高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}")]
    public static partial void LogVirtualScreen(ILogger logger, int x, int y, int width, int height);

    /// <summary>Crop セッション開始のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] CropStart: source={Width}x{Height}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="width">元画像幅。</param>
    /// <param name="height">元画像高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] CropStart: source={Width}x{Height}")]
    public static partial void LogCropStart(ILogger logger, int width, int height);

    /// <summary>Crop 確定のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] CropCompleted: rect={Rect}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="rect">切り出し範囲。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] CropCompleted: rect={Rect}")]
    public static partial void LogCropCompleted(ILogger logger, string rect);

    /// <summary>Crop キャンセルのログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] CropCancelled</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] CropCancelled")]
    public static partial void LogCropCancelled(ILogger logger);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[SnipCapture] Setting: {Name} {Old} → {New}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[SnipCapture] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(ILogger logger, string name, string oldValue, string newValue);
}

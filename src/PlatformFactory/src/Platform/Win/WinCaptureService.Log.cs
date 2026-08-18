using System.Drawing;
using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.Platform.Win;

/// <summary>WinCaptureService の LoggerMessage 定義（ソースジェネレーターによるログ呼び出しの最適化）。</summary>
/// <remarks>
/// ログの定義は本ファイル（partial 分割）に集約し、メソッド本体から分離する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、
/// 呼び出し側では logger 引数を省略する（例: <c>_log.LogVirtualScreenBounds(vx, vy, vw, vh)</c>）。
/// </remarks>
internal static partial class WinCaptureServiceLog
{
    /// <summary>仮想スクリーン境界のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureVirtualScreen: bounds=({X},{Y},{Width},{Height})</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureVirtualScreen: bounds=(1920,0,3840,1080)</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">仮想スクリーン原点X。</param>
    /// <param name="y">仮想スクリーン原点Y。</param>
    /// <param name="width">仮想スクリーン幅。</param>
    /// <param name="height">仮想スクリーン高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureVirtualScreen: bounds=({X},{Y},{Width},{Height})")]
    internal static partial void LogVirtualScreenBounds(this ILogger logger, int x, int y, int width, int height);

    /// <summary>ウィンドウキャプチャ開始のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureWindow: hWnd=0x{Hwnd}, mode={Mode}</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureWindow: hWnd=0x1A2B3C, mode=RedrawDirect</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="hwnd">ウィンドウハンドル（16進文字列）。</param>
    /// <param name="mode">キャプチャ方式。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureWindow: hWnd=0x{Hwnd}, mode={Mode}")]
    internal static partial void LogCaptureWindow(this ILogger logger, string hwnd, WindowCaptureMode mode);

    /// <summary>PrintWindow 成功のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureWindow: PrintWindow成功, fullBitmap={Width}x{Height}</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureWindow: PrintWindow成功, fullBitmap=1920x1080</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="width">Bitmap 幅。</param>
    /// <param name="height">Bitmap 高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureWindow: PrintWindow成功, fullBitmap={Width}x{Height}")]
    internal static partial void LogPrintWindowSuccess(this ILogger logger, int width, int height);

    /// <summary>可視矩形への切り出し成功のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureWindow: cropped to visibleRect, bitmap={Width}x{Height}</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureWindow: cropped to visibleRect, bitmap=1920x1040</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="width">Bitmap 幅。</param>
    /// <param name="height">Bitmap 高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureWindow: cropped to visibleRect, bitmap={Width}x{Height}")]
    internal static partial void LogCroppedToVisibleRect(this ILogger logger, int width, int height);

    /// <summary>PrintWindow 全フラグ失敗の警告ログ。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[CaptureService] CaptureWindow: PrintWindow全フラグ失敗。null を返す（仕様 2.4.3）</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureWindow: PrintWindow全フラグ失敗。null を返す（仕様 2.4.3）</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Warning, "[CaptureService] CaptureWindow: PrintWindow全フラグ失敗。null を返す（仕様 2.4.3）")]
    internal static partial void LogPrintWindowAllFlagsFailed(this ILogger logger);

    /// <summary>CopyFromScreen 方式キャプチャのデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureWindow: CopyFromScreenで visibleRect={VisibleRect}</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureWindow: CopyFromScreenで visibleRect={X=0,Y=0,Width=1920,Height=1080}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="visibleRect">可視矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureWindow: CopyFromScreenで visibleRect={VisibleRect}")]
    internal static partial void LogCopyFromScreen(this ILogger logger, Rectangle visibleRect);

    /// <summary>画面領域キャプチャ開始のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureSdBitmap: bounds={Bounds}</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureSdBitmap: bounds={X=0,Y=0,Width=1920,Height=1080}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="bounds">キャプチャ矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureSdBitmap: bounds={Bounds}")]
    internal static partial void LogCaptureSdBitmap(this ILogger logger, Rectangle bounds);

    /// <summary>画面領域キャプチャ完了のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureSdBitmap: done, bitmap={Width}x{Height}</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureSdBitmap: done, bitmap=1920x1080</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="width">Bitmap 幅。</param>
    /// <param name="height">Bitmap 高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureSdBitmap: done, bitmap={Width}x{Height}")]
    internal static partial void LogCaptureSdBitmapDone(this ILogger logger, int width, int height);

    /// <summary>PrintWindow 試行成功のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] TryPrintWindow: flags={Flags} success, bitmap={Width}x{Height}</c> <br/>
    /// 出力例: <c>[CaptureService] TryPrintWindow: flags=0 success, bitmap=1920x1080</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="flags">PrintWindow フラグ。</param>
    /// <param name="width">Bitmap 幅。</param>
    /// <param name="height">Bitmap 高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] TryPrintWindow: flags={Flags} success, bitmap={Width}x{Height}")]
    internal static partial void LogTryPrintWindowSuccess(this ILogger logger, uint flags, int width, int height);

    /// <summary>PrintWindow 試行失敗・スキップのデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] TryPrintWindow: flags={Flags} failed/skip (success={Success})</c> <br/>
    /// 出力例: <c>[CaptureService] TryPrintWindow: flags=2 failed/skip (success=False)</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="flags">PrintWindow フラグ。</param>
    /// <param name="success">成功したか。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] TryPrintWindow: flags={Flags} failed/skip (success={Success})")]
    internal static partial void LogTryPrintWindowFailed(this ILogger logger, uint flags, bool success);

    /// <summary>PrintWindow 試行の例外エラーログ。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>[CaptureService] TryPrintWindow: flags={Flags}</c> <br/>
    /// 出力例: <c>[CaptureService] TryPrintWindow: flags=0</c>（例外スタックトレース付き）
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <param name="flags">PrintWindow フラグ。</param>
    [LoggerMessage(LogLevel.Error, "[CaptureService] TryPrintWindow: flags={Flags}")]
    internal static partial void LogTryPrintWindowError(this ILogger logger, Exception exception, uint flags);
}

using Microsoft.Extensions.Logging;
using RectInt = System.Drawing.Rectangle;

namespace Estable.Platform.Win;

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

    /// <summary>画面領域キャプチャ開始のデバッグログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[CaptureService] CaptureSdBitmap: bounds={Bounds}</c> <br/>
    /// 出力例: <c>[CaptureService] CaptureSdBitmap: bounds={X=0,Y=0,Width=1920,Height=1080}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="bounds">キャプチャ矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[CaptureService] CaptureSdBitmap: bounds={Bounds}")]
    internal static partial void LogCaptureSdBitmap(this ILogger logger, RectInt bounds);

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

}

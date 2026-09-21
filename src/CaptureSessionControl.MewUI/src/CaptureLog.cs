using Microsoft.Extensions.Logging;

namespace Estable.MewUI.CaptureSession;

/// <summary>撮影・切り出しセッションのログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// ライブラリ自身はログ基盤（ファイル出力・レベル制御）を所有せず、注入された <see cref="ILogger"/> へ出力する。<br/>
/// 出力書式はホスト（アプリ）のログと揃え、ログファイル上で区別できないようにする。
/// </remarks>
internal static partial class CaptureLog
{
    /// <summary>撮影セッション開始のログ（LogLevel: Debug / <c>撮影開始: type={Type}</c>）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="type">撮影種別。</param>
    [LoggerMessage(LogLevel.Debug, "撮影開始: type={Type}")]
    public static partial void LogCaptureStart(this ILogger logger, string type);

    /// <summary>撮影確定のログ（LogLevel: Debug / <c>撮影確定: rect={Rect}</c>）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="rect">確定した矩形（画面物理ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "撮影確定: rect={Rect}")]
    public static partial void LogCaptureCompleted(this ILogger logger, string rect);

    /// <summary>撮影キャンセルのログ（LogLevel: Debug / <c>撮影キャンセル</c>）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "撮影キャンセル")]
    public static partial void LogCaptureCancelled(this ILogger logger);

    /// <summary>切出しセッション開始のログ（LogLevel: Debug / <c>切出し開始: image={Width}x{Height}</c>）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="width">元画像の幅（ピクセル）。</param>
    /// <param name="height">元画像の高さ（ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "切出し開始: image={Width}x{Height}")]
    public static partial void LogCropStart(this ILogger logger, int width, int height);

    /// <summary>切出し確定のログ（LogLevel: Debug / <c>切出し確定: rect={Rect}</c>）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="rect">切り出す矩形（画像ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "切出し確定: rect={Rect}")]
    public static partial void LogCropCompleted(this ILogger logger, string rect);

    /// <summary>切出しキャンセルのログ（LogLevel: Debug / <c>切出しキャンセル</c>）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "切出しキャンセル")]
    public static partial void LogCropCancelled(this ILogger logger);
}

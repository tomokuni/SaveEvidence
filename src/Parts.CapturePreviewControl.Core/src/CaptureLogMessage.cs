using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>キャプチャセッションのログ定義（LoggerMessage ソース生成・<c>this ILogger</c> 拡張メソッド）。</summary>
/// <remarks>
/// 撮影と切り出しの開始・確定・キャンセルは<b>両 UI で同じ事象</b>であるため、書式とレベルを本クラスが単一所有する。<br/>
/// 診断用のログ（検出結果・仮想スクリーン・結果サイズ・確定／キャンセル要求）も<b>本クラスが所有する</b><br/>
/// （書式が「どの UI の実装か」に依存しないため、UI 層に定義を置く理由がない）。<br/>
/// <b>呼び出し側でゲートしない</b>（ログ本文の出力可否は生成コードの <c>IsEnabled</c> が判定する）。<br/>
/// <b>ただし引数の評価が高価な場合は呼び出し側で <c>IsEnabled</c> でゲートする</b>（引数は呼び出し前に評価されるため、<br/>
/// 生成コードの <c>IsEnabled</c> では防げない。 検出結果の整形が該当する）。
/// </remarks>
public static partial class CaptureLogMessage
{
    /// <summary>撮影セッションの開始を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">キャプチャ種別（文字列化済み）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] CaptureStart: type={CaptureType}</c></remarks>
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "[CaptureSession] CaptureStart: type={CaptureType}")]
    public static partial void LogCaptureStart(this ILogger logger, string captureType);

    /// <summary>撮影セッションの終了を記録する（確定は 2 件、キャンセルは 1 件）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="result">完了した結果。 キャンセルの場合は null。</param>
    /// <remarks>
    /// 「結果が確定した場合にどのログをどの順序で出すか」を単一所有する<br/>
    /// （UI が結果の有無を分岐して個別のログを呼ぶと、UI ごとに出力の組が違って比較できない）。<br/>
    /// 確定: <c>[CaptureSession] CaptureCompleted: bounds=…</c> → <c>[CaptureSession] Result: bitmap=…x… bounds=…</c><br/>
    /// キャンセル: <c>[CaptureSession] CaptureCancelled</c>
    /// </remarks>
    public static void LogCaptureFinished(this ILogger logger, CaptureResult? result)
    {
        if (result is { Image: { } image, SourceBounds: { } bounds })
        {
            var rect = CaptureLogFormat.Rect(bounds);
            logger.LogCaptureCompleted(rect);
            logger.LogResult(image.Size.Width, image.Size.Height, rect);
            return;
        }

        logger.LogCaptureCancelled();
    }

    /// <summary>撮影セッションの確定を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="bounds">確定した領域（<see cref="CaptureLogFormat.Rect(EsRectInt)"/> の出力）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] CaptureCompleted: bounds={Bounds}</c></remarks>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "[CaptureSession] CaptureCompleted: bounds={Bounds}")]
    private static partial void LogCaptureCompleted(this ILogger logger, string bounds);
    /// <summary>撮影セッションのキャンセルを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] CaptureCancelled</c></remarks>
    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "[CaptureSession] CaptureCancelled")]
    private static partial void LogCaptureCancelled(this ILogger logger);

    /// <summary>切り出しセッションの開始を記録する（元画像のサイズ）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="image">切り出し元の画像（サイズのみを使う）。</param>
    /// <remarks>
    /// ログレベル: Debug。 出力書式: <c>[CaptureSession] CropStart: source={Width}x{Height}</c><br/>
    /// 元画像からサイズを取り出す入口を単一所有する（UI が独自に整数へ丸めない）。
    /// </remarks>
    public static void LogCropStart(this ILogger logger, EsBitmap image)
        => LogCropStartCore(logger, image.Width, image.Height);

    /// <summary>切り出しセッションの開始を記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="width">元画像の幅（ピクセル）。</param>
    /// <param name="height">元画像の高さ（ピクセル）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] CropStart: source={Width}x{Height}</c></remarks>
    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "[CaptureSession] CropStart: source={Width}x{Height}")]
    private static partial void LogCropStartCore(this ILogger logger, int width, int height);
    /// <summary>切り出しセッションの終了を記録する（退化した選択はキャンセルとして記録する）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="bounds">切り出し範囲（元画像ピクセル）。 空の場合はキャンセルとして記録する。</param>
    /// <remarks>
    /// 「切り出せる矩形が無い場合はキャンセルとして記録する」規則を単一所有する<br/>
    /// （UI が空判定とログの呼び分けを書くと、同じ操作で UI ごとに異なるログになる）。<br/>
    /// 確定: <c>[CaptureSession] CropCompleted: bounds=…</c> / キャンセル: <c>[CaptureSession] CropCancelled</c>
    /// </remarks>
    public static void LogCropFinished(this ILogger logger, EsRectInt bounds)
    {
        if (bounds.IsEmpty)
        {
            logger.LogCropCancelled();
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogCropCompleted(CaptureLogFormat.Rect(bounds));
        }
    }

    /// <summary>切り出しセッションの確定を記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="bounds">確定した切り出し範囲（元画像ピクセル。 空を渡さない）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] CropCompleted: bounds={Bounds}</c></remarks>
    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "[CaptureSession] CropCompleted: bounds={Bounds}")]
    private static partial void LogCropCompleted(this ILogger logger, string bounds);

    /// <summary>切り出しセッションのキャンセルを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] CropCancelled</c></remarks>
    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "[CaptureSession] CropCancelled")]
    public static partial void LogCropCancelled(this ILogger logger);

    /// <summary>撮影結果の画像サイズと領域を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <param name="bounds">撮影領域（<see cref="CaptureLogFormat.Rect(EsRectInt)"/> の出力）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] Result: bitmap={Width}x{Height} bounds={Bounds}</c></remarks>
    [LoggerMessage(EventId = 8, Level = LogLevel.Debug,
        Message = "[CaptureSession] Result: bitmap={Width}x{Height} bounds={Bounds}")]
    private static partial void LogResult(this ILogger logger, int width, int height, string bounds);

    /// <summary>選択の確定要求（Enter・ダブルクリック等）を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="rect">要求時点の選択矩形（対象座標）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] ConfirmRequested: rect={Rect}</c></remarks>
    [LoggerMessage(EventId = 9, Level = LogLevel.Debug,
        Message = "[CaptureSession] ConfirmRequested: rect={Rect}")]
    public static partial void LogConfirmRequested(this ILogger logger, string rect);

    /// <summary>選択のキャンセル要求（Esc・非アクティブ化等）を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] CancelRequested</c></remarks>
    [LoggerMessage(EventId = 10, Level = LogLevel.Debug,
        Message = "[CaptureSession] CancelRequested")]
    public static partial void LogCancelRequested(this ILogger logger);

    /// <summary>仮想スクリーンの原点とサイズを記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="origin">仮想スクリーンの原点（画面物理ピクセル）。</param>
    /// <param name="bitmap">取得した仮想スクリーン画像。 未取得の場合は null（サイズは 0 として記録する）。</param>
    /// <remarks>
    /// ログレベル: Debug。 出力書式: <c>[CaptureSession] VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}</c><br/>
    /// 「未取得は 0」という正規化を単一所有する（呼び出し側で null 判定を書かない）。
    /// </remarks>
    public static void LogVirtualScreen(this ILogger logger, EsPointInt origin, EsBitmap? bitmap)
        => LogVirtualScreenCore(logger, origin.X, origin.Y, bitmap?.Width ?? 0, bitmap?.Height ?? 0);

    /// <summary>仮想スクリーンの原点とサイズを記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">原点の X 座標（画面物理ピクセル）。</param>
    /// <param name="y">原点の Y 座標（画面物理ピクセル）。</param>
    /// <param name="width">仮想スクリーンの幅（ピクセル）。</param>
    /// <param name="height">仮想スクリーンの高さ（ピクセル）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}</c></remarks>
    [LoggerMessage(EventId = 11, Level = LogLevel.Debug,
        Message = "[CaptureSession] VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}")]
    private static partial void LogVirtualScreenCore(this ILogger logger, int x, int y, int width, int height);

    /// <summary>ハイライトの検出結果（スクリーン選択 / ウィンドウ選択）を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">キャプチャ種別（文字列化済み）。</param>
    /// <param name="detected">検出されたかどうか。</param>
    /// <param name="rect">検出矩形（対象座標。 未検出時は空文字）。</param>
    /// <param name="title">検出対象のタイトル（スクリーン番号 / ウィンドウタイトル。 未検出時は空文字）。</param>
    /// <param name="className">検出対象のクラス名（ウィンドウ検出時のみ。 未検出時は空文字）。</param>
    /// <remarks>
    /// ログレベル: Trace <br/>
    /// 出力書式: <c>[CaptureSession] Highlight: type={CaptureType} detected={Detected} rect={Rect} title={Title} class={ClassName}</c><br/>
    /// 検出矩形と対象（タイトル・クラス名）を記録し、マルチモニタ・最大化ウィンドウの矩形検証と<br/>
    /// デスクトップウィンドウの特定に使用する（高頻度のため Trace）。
    /// </remarks>
    [LoggerMessage(EventId = 12, Level = LogLevel.Trace,
        Message = "[CaptureSession] Highlight: type={CaptureType} detected={Detected} rect={Rect} title={Title} class={ClassName}")]
    public static partial void LogHighlight(this ILogger logger, string captureType, bool detected, string rect, string title, string className);
}

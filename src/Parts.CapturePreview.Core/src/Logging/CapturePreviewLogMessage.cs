using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;

/// <summary>キャプチャセッションのログ定義（LoggerMessage ソース生成・<c>this ILogger</c> 拡張メソッド）。</summary>
/// <remarks>
/// 撮影と切り出しの開始・確定・キャンセルは<b>両 UI で同じ事象</b>であるため、書式とレベルを本クラスが単一所有する。<br/>
/// 診断用のログ（検出結果・仮想スクリーン・結果サイズ・確定／キャンセル要求）も<b>本クラスが所有する</b><br/>
/// （書式が「どの UI の実装か」に依存しないため、UI 層に定義を置く理由がない）。<br/>
/// <b>呼び出し側でゲートしない</b>（ログ本文の出力可否は生成コードの <c>IsEnabled</c> が判定する）。<br/>
/// <b>ただし引数の評価が高価な場合は呼び出し側で <c>IsEnabled</c> でゲートする</b>（引数は呼び出し前に評価されるため、<br/>
/// 生成コードの <c>IsEnabled</c> では防げない。 検出結果の整形が該当する）。
/// </remarks>
public static partial class CapturePreviewLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    public const string DefaultPrefix = "[CapturePreview]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    /// <value>設定された接頭辞。 変更は <see cref="SetPrefix"/> を通す。</value>
    public static string Prefix { get; private set; } = DefaultPrefix;

    /// <summary>接頭辞を設定する（null または空白は既定値へ戻す）。</summary>
    /// <param name="value">設定する接頭辞。 null または空白の場合は <see cref="DefaultPrefix"/>。</param>
    /// <remarks>
    /// 「null または空白は既定値へ戻す」という規則を単一所有する<br/>
    /// （UI の <c>LogPrefix</c> プロパティごとに同じ三項演算を書くと、規則が UI ごとにずれる）。
    /// </remarks>
    public static void SetPrefix(string? value)
        => Prefix = string.IsNullOrWhiteSpace(value) ? DefaultPrefix : value;

    // ====================================================================
    // ログに載せる値の整形（ログ用途のみ。 別クラスは作らない）
    // ====================================================================

    /// <summary>矩形を <c>x,y,w,h</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形。</param>
    /// <returns><c>{X},{Y},{Width},{Height}</c> 形式の文字列。</returns>
    internal static string Rect(EsRectInt rect) => $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";

    /// <summary>論理座標の矩形を <c>x,y,w,h</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形（論理座標）。</param>
    /// <returns><c>{X},{Y},{Width},{Height}</c> 形式の文字列。</returns>
    internal static string Rect(EsRect rect) => $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";

    /// <summary>未検出の場合に空文字へ正規化した矩形の文字列を返す。</summary>
    /// <param name="rect">整形する矩形（未検出の場合は null）。</param>
    /// <returns>矩形がある場合は <c>x,y,w,h</c> 形式、null の場合は空文字。</returns>
    internal static string RectOrEmpty(EsRectInt? rect) => rect is { } value ? Rect(value) : "";

    /// <summary>ズーム率（%）をログ用の整数へ丸める。</summary>
    /// <param name="zoomPercent">ズーム率（%）。</param>
    /// <returns>四捨五入した整数（%）。</returns>
    /// <remarks>ログは人が読む値のため、小数を持つズーム率（Fit 実効倍率・ホイール倍率の結果）は整数へ丸める。</remarks>
    internal static int Percent(double zoomPercent) => (int)Math.Round(zoomPercent);

    /// <summary>公開ファサードの操作で例外が発生したことを記録する（生成メソッド）。</summary>
    /// <remarks>ログレベル: Error。 出力書式: <c>[CapturePreview] {Operation} で例外が発生</c>（例外スタックトレース付き）</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="exception">発生した例外。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="operation">操作名（<c>nameof(操作)</c>）。</param>
    [LoggerMessage(EventId = 13, Level = LogLevel.Error,
        Message = "{Prefix} {Operation} で例外が発生")]
    private static partial void LogOperationErrorCore(this ILogger logger, Exception exception, string prefix, string operation);

    /// <summary>公開ファサードの操作で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="exception">発生した例外。</param>
    /// <param name="operation">操作名（<c>nameof(操作)</c>）。</param>
    /// <remarks>公開ファサードが例外を集約して記録し、上位へ伝搬するために使う（例外処理規約 §4）。</remarks>
    public static void LogOperationError(this ILogger logger, Exception exception, string operation)
        => logger.LogOperationErrorCore(exception, Prefix, operation);

    /// <summary>撮影セッションの開始を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <remarks>
    /// ログレベル: Debug。 出力書式: <c>[CapturePreview] CaptureStart: type={CaptureType}</c><br/>
    /// <b>呼び出し側で <c>IsEnabled</c> を書かない</b>（値の文字列化は生成コードが有効時だけ行う）。
    /// </remarks>
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "{Prefix} CaptureStart: type={CaptureType}")]
    private static partial void LogCaptureStartCore(this ILogger logger, string prefix, CaptureType captureType);

    /// <summary>撮影セッションの開始を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">キャプチャ種別。</param>
    public static void LogCaptureStart(this ILogger logger, CaptureType captureType)
        => logger.LogCaptureStartCore(Prefix, captureType);

    /// <summary>撮影セッションの終了を記録する（確定は 2 件、キャンセルは 1 件）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="result">完了した結果。 キャンセルの場合は null。</param>
    /// <remarks>
    /// 「結果が確定した場合にどのログをどの順序で出すか」を単一所有する<br/>
    /// （UI が結果の有無を分岐して個別のログを呼ぶと、UI ごとに出力の組が違って比較できない）。<br/>
    /// 確定: <c>[CapturePreview] CaptureCompleted: bounds=…</c> → <c>[CapturePreview] Result: bitmap=…x… bounds=…</c><br/>
    /// キャンセル: <c>[CapturePreview] CaptureCancelled</c><br/>
    /// <b>整形は本メソッドで行う</b>（呼び出し側は値を渡すだけ。 無効時の整形を避けるゲートも本メソッドが持つ）。
    /// </remarks>
    public static void LogCaptureFinished(this ILogger logger, CaptureResult? result)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        if (result is { Image: { } image, SourceBounds: { } bounds })
        {
            var rect = Rect(bounds);
            logger.LogCaptureCompleted(rect);
            logger.LogResult(image.Size.Width, image.Size.Height, rect);
            return;
        }

        logger.LogCaptureCancelled();
    }

    /// <summary>撮影セッションの確定を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="bounds">確定した領域（<see cref="Rect(EsRectInt)"/> の出力）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] CaptureCompleted: bounds={Bounds}</c></remarks>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "{Prefix} CaptureCompleted: bounds={Bounds}")]
    private static partial void LogCaptureCompletedCore(this ILogger logger, string prefix, string bounds);

    private static void LogCaptureCompleted(this ILogger logger, string bounds)
        => logger.LogCaptureCompletedCore(Prefix, bounds);
    /// <summary>撮影セッションのキャンセルを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] CaptureCancelled</c></remarks>
    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "{Prefix} CaptureCancelled")]
    private static partial void LogCaptureCancelledCore(this ILogger logger, string prefix);

    private static void LogCaptureCancelled(this ILogger logger)
        => logger.LogCaptureCancelledCore(Prefix);

    /// <summary>切り出しセッションの開始を記録する（元画像のサイズ）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="image">切り出し元の画像（サイズのみを使う）。</param>
    /// <remarks>
    /// ログレベル: Debug。 出力書式: <c>[CapturePreview] CropStart: source={Width}x{Height}</c><br/>
    /// 元画像からサイズを取り出す入口を単一所有する（UI が独自に整数へ丸めない）。
    /// </remarks>
    public static void LogCropStart(this ILogger logger, IEsBitmap image)
        => LogCropStartCore(logger, Prefix, image.Width, image.Height);

    /// <summary>切り出しセッションの開始を記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="width">元画像の幅（ピクセル）。</param>
    /// <param name="height">元画像の高さ（ピクセル）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] CropStart: source={Width}x{Height}</c></remarks>
    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "{Prefix} CropStart: source={Width}x{Height}")]
    private static partial void LogCropStartCore(this ILogger logger, string prefix, int width, int height);
    /// <summary>切り出しセッションの終了を記録する（退化した選択はキャンセルとして記録する）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="bounds">切り出し範囲（元画像ピクセル）。 空の場合はキャンセルとして記録する。</param>
    /// <remarks>
    /// 「切り出せる矩形が無い場合はキャンセルとして記録する」規則を単一所有する<br/>
    /// （UI が空判定とログの呼び分けを書くと、同じ操作で UI ごとに異なるログになる）。<br/>
    /// 確定: <c>[CapturePreview] CropCompleted: bounds=…</c> / キャンセル: <c>[CapturePreview] CropCancelled</c>
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
            logger.LogCropCompleted(Rect(bounds));
        }
    }

    /// <summary>切り出しセッションの確定を記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="bounds">確定した切り出し範囲（元画像ピクセル。 空を渡さない）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] CropCompleted: bounds={Bounds}</c></remarks>
    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "{Prefix} CropCompleted: bounds={Bounds}")]
    private static partial void LogCropCompletedCore(this ILogger logger, string prefix, string bounds);

    private static void LogCropCompleted(this ILogger logger, string bounds)
        => logger.LogCropCompletedCore(Prefix, bounds);

    /// <summary>切り出しセッションのキャンセルを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] CropCancelled</c></remarks>
    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "{Prefix} CropCancelled")]
    private static partial void LogCropCancelledCore(this ILogger logger, string prefix);

    /// <summary>切り出しセッションのキャンセルを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    public static void LogCropCancelled(this ILogger logger)
        => logger.LogCropCancelledCore(Prefix);

    /// <summary>撮影結果の画像サイズと領域を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <param name="bounds">撮影領域（<see cref="Rect(EsRectInt)"/> の出力）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] Result: bitmap={Width}x{Height} bounds={Bounds}</c></remarks>
    [LoggerMessage(EventId = 8, Level = LogLevel.Debug,
        Message = "{Prefix} Result: bitmap={Width}x{Height} bounds={Bounds}")]
    private static partial void LogResultCore(this ILogger logger, string prefix, int width, int height, string bounds);

    private static void LogResult(this ILogger logger, int width, int height, string bounds)
        => logger.LogResultCore(Prefix, width, height, bounds);

    /// <summary>選択の確定要求（Enter・ダブルクリック等）を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="rect">要求時点の選択矩形（対象座標）。</param>
    /// <remarks>
    /// ログレベル: Debug。 出力書式: <c>[CapturePreview] ConfirmRequested: rect={Rect}</c><br/>
    /// <b>整形は本メソッドで行う</b>（呼び出し側は矩形を渡すだけ。 UI は浮動小数と整数の両方の矩形を渡すためオーバーロードを 2 つ持つ）。
    /// </remarks>
    public static void LogConfirmRequested(this ILogger logger, EsRect rect)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogConfirmRequestedCore(Prefix, Rect(rect));
    }

    /// <summary>選択の確定要求（Enter・ダブルクリック等）を記録する（診断用。 整数矩形版）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="rect">要求時点の選択矩形（対象座標）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] ConfirmRequested: rect={Rect}</c></remarks>
    public static void LogConfirmRequested(this ILogger logger, EsRectInt rect)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogConfirmRequestedCore(Prefix, Rect(rect));
    }

    /// <summary>選択の確定要求（Enter・ダブルクリック等）を記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="rect">整形済みの選択矩形。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] ConfirmRequested: rect={Rect}</c></remarks>
    [LoggerMessage(EventId = 9, Level = LogLevel.Debug,
        Message = "{Prefix} ConfirmRequested: rect={Rect}")]
    private static partial void LogConfirmRequestedCore(this ILogger logger, string prefix, string rect);

    /// <summary>選択のキャンセル要求（Esc・非アクティブ化等）を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] CancelRequested</c></remarks>
    [LoggerMessage(EventId = 10, Level = LogLevel.Debug,
        Message = "{Prefix} CancelRequested")]
    private static partial void LogCancelRequestedCore(this ILogger logger, string prefix);

    /// <summary>選択のキャンセル要求（Esc・非アクティブ化等）を記録する。</summary>
    /// <param name="logger">ロガー。</param>
    public static void LogCancelRequested(this ILogger logger)
        => logger.LogCancelRequestedCore(Prefix);

    /// <summary>仮想スクリーンの原点とサイズを記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="origin">仮想スクリーンの原点（画面物理ピクセル）。</param>
    /// <param name="bitmap">取得した仮想スクリーン画像。 未取得の場合は null（サイズは 0 として記録する）。</param>
    /// <remarks>
    /// ログレベル: Debug。 出力書式: <c>[CapturePreview] VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}</c><br/>
    /// 「未取得は 0」という正規化を単一所有する（呼び出し側で null 判定を書かない）。
    /// </remarks>
    public static void LogVirtualScreen(this ILogger logger, EsPointInt origin, IEsBitmap? bitmap)
        => LogVirtualScreenCore(logger, Prefix, origin.X, origin.Y, bitmap?.Width ?? 0, bitmap?.Height ?? 0);

    /// <summary>仮想スクリーンの原点とサイズを記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">原点の X 座標（画面物理ピクセル）。</param>
    /// <param name="y">原点の Y 座標（画面物理ピクセル）。</param>
    /// <param name="width">仮想スクリーンの幅（ピクセル）。</param>
    /// <param name="height">仮想スクリーンの高さ（ピクセル）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}</c></remarks>
    [LoggerMessage(EventId = 11, Level = LogLevel.Debug,
        Message = "{Prefix} VirtualScreen: origin=({X}, {Y}) size={Width}x{Height}")]
    private static partial void LogVirtualScreenCore(this ILogger logger, string prefix, int x, int y, int width, int height);

    /// <summary>ハイライトの検出結果（スクリーン選択 / ウィンドウ選択）を記録する（診断用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="detected">検出されたかどうか。</param>
    /// <param name="rect">検出矩形（対象座標。 未検出時は null）。</param>
    /// <param name="title">検出対象のタイトル（スクリーン番号 / ウィンドウタイトル。 未検出時は null）。</param>
    /// <param name="className">検出対象のクラス名（ウィンドウ検出時のみ。 未検出時は null）。</param>
    /// <remarks>
    /// ログレベル: Trace <br/>
    /// 出力書式: <c>[CapturePreview] Highlight: type={CaptureType} detected={Detected} rect={Rect} title={Title} class={ClassName}</c><br/>
    /// 検出矩形と対象（タイトル・クラス名）を記録し、マルチモニタ・最大化ウィンドウの矩形検証と<br/>
    /// デスクトップウィンドウの特定に使用する（高頻度のため Trace）。<br/>
    /// <b>整形は本メソッドで行う</b>（呼び出し側は検出結果を渡すだけ。 高頻度のため呼び出し側でゲートしない）。
    /// </remarks>
    public static void LogHighlight(this ILogger logger, CaptureType captureType, bool detected, EsRectInt? rect, string? title, string? className)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogHighlightCore(
            Prefix,
            captureType,
            detected,
            RectOrEmpty(rect),
            TextNormalize.Normalize(title),
            TextNormalize.Normalize(className));
    }

    /// <summary>ハイライトの検出結果を記録する（内部用）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="detected">検出されたかどうか。</param>
    /// <param name="rect">整形済みの検出矩形（未検出時は空文字）。</param>
    /// <param name="title">整形済みのタイトル（未検出時は空文字）。</param>
    /// <param name="className">整形済みのクラス名（未検出時は空文字）。</param>
    /// <remarks>ログレベル: Trace。 出力書式: <c>[CapturePreview] Highlight: type={CaptureType} detected={Detected} rect={Rect} title={Title} class={ClassName}</c></remarks>
    [LoggerMessage(EventId = 12, Level = LogLevel.Trace,
        Message = "{Prefix} Highlight: type={CaptureType} detected={Detected} rect={Rect} title={Title} class={ClassName}")]
    private static partial void LogHighlightCore(this ILogger logger, string prefix, CaptureType captureType, bool detected, string rect, string title, string className);

    // ====================================================================
    // 設定の適用（旧 CaptureSettingsLogMessage）
    // ====================================================================

    /// <summary>設定の変更を適用したことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CapturePreview] SettingApplied</c></remarks>
    [LoggerMessage(EventId = 7, Level = LogLevel.Debug,
        Message = "{Prefix} SettingApplied")]
    private static partial void LogSettingAppliedCore(this ILogger logger, string prefix);

    /// <summary>設定の変更を適用したことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    public static void LogSettingApplied(this ILogger logger)
        => logger.LogSettingAppliedCore(Prefix);

    // ====================================================================
    // ズーム操作（旧 ZoomLogMessage）
    // ====================================================================

    /// <summary>ズームイン（⊖⊕ ボタン）の実行前後を記録する。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomIn: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    /// <remarks>丸め（四捨五入）は本メソッド（前段階）が単一所有する（呼び出し側は生値を渡す）。</remarks>
    public static void LogZoomIn(this ILogger logger, double current, double target)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogZoomInCore(Prefix, Percent(current), Percent(target));
    }

    /// <summary>ズームイン（⊖⊕ ボタン）の実行前後を記録する（生成メソッド）。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomIn: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="current">変更前の実効ズーム率（%。 丸め済み）。</param>
    /// <param name="target">変更後のズーム率（%。 丸め済み）。</param>
    [LoggerMessage(EventId = 14, Level = LogLevel.Information,
        Message = "{Prefix} ZoomIn: {Current}% -> {Target}%")]
    private static partial void LogZoomInCore(this ILogger logger, string prefix, int current, int target);

    /// <summary>ズームアウト（⊖⊕ ボタン）の実行前後を記録する。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomOut: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    public static void LogZoomOut(this ILogger logger, double current, double target)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogZoomOutCore(Prefix, Percent(current), Percent(target));
    }

    /// <summary>ズームアウト（⊖⊕ ボタン）の実行前後を記録する（生成メソッド）。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomOut: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="current">変更前の実効ズーム率（%。 丸め済み）。</param>
    /// <param name="target">変更後のズーム率（%。 丸め済み）。</param>
    [LoggerMessage(EventId = 15, Level = LogLevel.Information,
        Message = "{Prefix} ZoomOut: {Current}% -> {Target}%")]
    private static partial void LogZoomOutCore(this ILogger logger, string prefix, int current, int target);

    /// <summary>Ctrl+ホイールのズームインの実行前後を記録する。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomInWheel: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    public static void LogZoomInWheel(this ILogger logger, double current, double target)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogZoomInWheelCore(Prefix, Percent(current), Percent(target));
    }

    /// <summary>Ctrl+ホイールのズームインの実行前後を記録する（生成メソッド）。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomInWheel: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="current">変更前の実効ズーム率（%。 丸め済み）。</param>
    /// <param name="target">変更後のズーム率（%。 丸め済み）。</param>
    [LoggerMessage(EventId = 16, Level = LogLevel.Information,
        Message = "{Prefix} ZoomInWheel: {Current}% -> {Target}%")]
    private static partial void LogZoomInWheelCore(this ILogger logger, string prefix, int current, int target);

    /// <summary>Ctrl+ホイールのズームアウトの実行前後を記録する。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomOutWheel: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    public static void LogZoomOutWheel(this ILogger logger, double current, double target)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogZoomOutWheelCore(Prefix, Percent(current), Percent(target));
    }

    /// <summary>Ctrl+ホイールのズームアウトの実行前後を記録する（生成メソッド）。</summary>
    /// <remarks>ログレベル: Information。 出力書式: <c>[CapturePreview] ZoomOutWheel: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="current">変更前の実効ズーム率（%。 丸め済み）。</param>
    /// <param name="target">変更後のズーム率（%。 丸め済み）。</param>
    [LoggerMessage(EventId = 17, Level = LogLevel.Information,
        Message = "{Prefix} ZoomOutWheel: {Current}% -> {Target}%")]
    private static partial void LogZoomOutWheelCore(this ILogger logger, string prefix, int current, int target);
}


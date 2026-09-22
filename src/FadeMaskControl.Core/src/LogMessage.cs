using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace Estable.FadeMask;

/// <summary>FadeMask の描画状態変更が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 設定・描画状態は 1 つの値（<see cref="FadeMaskOptions"/>）にまとまっているため、UI 層は変更前後を渡すだけであり、<br/>
/// <b>どの項目が変わったかの判定（差分検出）は <see cref="LogOptionsChanged"/> が単一所有する</b>（UI 層に項目ごとの分岐を置かない）。<br/>
/// 矩形の文字列化（<c>(X, Y, W, H)</c>）と書式も本クラスが行い、UI 側に整形処理を置かない（両 UI で同一書式）。<br/>
/// FadeMaskControl は描画専用コントロールのため、マウス・キーイベントのログはない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class LogMessage
{
    /// <summary>設定・描画状態の変更ログ（変化した項目のみを個別の書式で出力する）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <see cref="LogSelectionRectChanged"/> / <see cref="LogHighlightRectChanged"/> に同じ。</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の設定・状態。</param>
    /// <param name="newValue">変更後の設定・状態。</param>
    public static void LogOptionsChanged(this ILogger logger, FadeMaskOptions oldValue, FadeMaskOptions newValue)
    {
        // 出力する項目は従来と同じ（選択矩形・ハイライト矩形）に限定する（外観・モードの変更は記録量が多く、従来も出力していない）
        if (oldValue.LogicalSelectionRect != newValue.LogicalSelectionRect)
        {
            logger.LogSelectionRectChanged(oldValue.LogicalSelectionRect, newValue.LogicalSelectionRect);
        }

        if (oldValue.LogicalHighlightRect != newValue.LogicalHighlightRect)
        {
            logger.LogHighlightRectChanged(oldValue.LogicalHighlightRect, newValue.LogicalHighlightRect);
        }
    }

    /// <summary>選択矩形（ドメイン座標）の変更ログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[FadeMask] SelectionRect: {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の選択矩形（未設定は null）。</param>
    /// <param name="newValue">変更後の選択矩形（未設定は null）。</param>
    public static void LogSelectionRectChanged(this ILogger logger, EsRect? oldValue, EsRect? newValue)
        => LogSelectionRectChangedCore(logger, FormatRect(oldValue), FormatRect(newValue));

    /// <summary>ハイライト矩形（ドメイン座標）の変更ログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[FadeMask] HighlightRect: {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前のハイライト矩形（未設定は null）。</param>
    /// <param name="newValue">変更後のハイライト矩形（未設定は null）。</param>
    public static void LogHighlightRectChanged(this ILogger logger, EsRect? oldValue, EsRect? newValue)
        => LogHighlightRectChangedCore(logger, FormatRect(oldValue), FormatRect(newValue));

    /// <summary>座標変換ポート設定のログ。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[FadeMask] CoordinateTransform: {Configured}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="configured">ポートが設定されたか（false = 解除・未設定）。</param>
    [LoggerMessage(LogLevel.Trace, "[FadeMask] CoordinateTransform: {Configured}")]
    public static partial void LogCoordinateTransform(this ILogger logger, bool configured);

    /// <summary>選択矩形の変更ログ（整形済みの文字列を受け取る生成コード）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="oldValue">変更前の選択矩形（未設定は <c>null</c>）。</param>
    /// <param name="newValue">変更後の選択矩形（未設定は <c>null</c>）。</param>
    [LoggerMessage(LogLevel.Debug, "[FadeMask] SelectionRect: {OldValue} → {NewValue}")]
    private static partial void LogSelectionRectChangedCore(ILogger logger, string oldValue, string newValue);

    /// <summary>ハイライト矩形の変更ログ（整形済みの文字列を受け取る生成コード）。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="oldValue">変更前のハイライト矩形（未設定は <c>null</c>）。</param>
    /// <param name="newValue">変更後のハイライト矩形（未設定は <c>null</c>）。</param>
    [LoggerMessage(LogLevel.Debug, "[FadeMask] HighlightRect: {OldValue} → {NewValue}")]
    private static partial void LogHighlightRectChangedCore(ILogger logger, string oldValue, string newValue);

    /// <summary>矩形をログ用の文字列へ整形する。</summary>
    /// <param name="rect">矩形（未設定は null）。</param>
    /// <returns><c>(X, Y, Width, Height)</c> 形式の文字列。未設定は <c>null</c>。</returns>
    private static string FormatRect(EsRect? rect)
        => rect is { } r ? $"({r.X}, {r.Y}, {r.Width}, {r.Height})" : "null";
}

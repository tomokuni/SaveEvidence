using Microsoft.Extensions.Logging;

namespace Estable.MewUI.FadeMask.Logging;

/// <summary>FadeMaskControl（MewUI 版）の LoggerMessage 定義（ソース生成ログ・拡張メソッド）。</summary>
/// <remarks>
/// 描画状態の変更（選択矩形・ハイライト・座標変換）をログ出力する（Avalonia 版と同一の書式）。<br/>
/// FadeMaskControl は描画専用コントロールのため、マウス・キーイベントのログはない。<br/>
/// ログ基盤はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class LogMessage
{
    /// <summary>選択矩形（ドメイン座標）の変更ログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[FadeMask] SelectionRect: {OldValue} → {NewValue}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の選択矩形（未設定は null）。</param>
    /// <param name="newValue">変更後の選択矩形（未設定は null）。</param>
    [LoggerMessage(LogLevel.Debug, "[FadeMask] SelectionRect: {OldValue} → {NewValue}")]
    public static partial void LogSelectionRectChanged(this ILogger logger, string oldValue, string newValue);

    /// <summary>ハイライト矩形（ドメイン座標）の変更ログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[FadeMask] HighlightRect: {OldValue} → {NewValue}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前のハイライト矩形（未設定は null）。</param>
    /// <param name="newValue">変更後のハイライト矩形（未設定は null）。</param>
    [LoggerMessage(LogLevel.Debug, "[FadeMask] HighlightRect: {OldValue} → {NewValue}")]
    public static partial void LogHighlightRectChanged(this ILogger logger, string oldValue, string newValue);

    /// <summary>座標変換ポート設定のログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[FadeMask] CoordinateTransform: {Configured}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="configured">ポートが設定されたか（false = 解除）。</param>
    [LoggerMessage(LogLevel.Trace, "[FadeMask] CoordinateTransform: {Configured}")]
    public static partial void LogCoordinateTransform(this ILogger logger, bool configured);
}

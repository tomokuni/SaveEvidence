using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.FadeMask.Logging;

/// <summary>FadeMaskControl の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 描画状態の変更（選択矩形・外部レンダラー・座標変換）と設定変更をログ出力する。<br/>
/// FadeMaskControl は描画専用コントロールのため、マウス・キーイベントのログはない。<br/>
/// ログ基盤はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class FadeMaskLog
{
    /// <summary>論理選択矩形（ドメイン座標）の変更ログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[FadeMask] LogicalSelectionRect: {Old} → {New}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="oldValue">変更前の選択矩形。</param>
    /// <param name="newValue">変更後の選択矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[FadeMask] LogicalSelectionRect: {OldValue} → {NewValue}")]
    public static partial void LogSelectionRectChanged(ILogger logger, string oldValue, string newValue);

    /// <summary>外部レンダラー設定のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[FadeMask] ExternalRenderer: {HasRenderer}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="hasRenderer">レンダラーが設定されたか（false = 解除）。</param>
    [LoggerMessage(LogLevel.Debug, "[FadeMask] ExternalRenderer: {HasRenderer}")]
    public static partial void LogExternalRenderer(ILogger logger, bool hasRenderer);

    /// <summary>座標変換ポート設定のログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[FadeMask] SetCoordinateTransform</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Trace, "[FadeMask] SetCoordinateTransform")]
    public static partial void LogCoordinateTransform(ILogger logger);
}

using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace Estable.FadeMask;

/// <summary>FadeMask の描画状態変更が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 選択矩形・ハイライト矩形・座標変換ポートの設定は、<b>両 UI で同一の書式・レベルにする必要がある</b>ため<br/>
/// コアが単一所有する（UI 実装ごとに持つと書式がずれ、片方だけ修正される）。<br/>
/// 矩形の文字列化（<c>(X, Y, W, H)</c>）も本クラスが行い、UI 側に整形処理を置かない。<br/>
/// FadeMaskControl は描画専用コントロールのため、マウス・キーイベントのログはない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class LogMessage
{
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

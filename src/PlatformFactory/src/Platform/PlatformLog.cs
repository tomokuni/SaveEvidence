using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.Platform;

/// <summary>PlatformFactory 内のコンポーネントが使用するログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// 各コンポーネント（Watchdog 等）の診断ログを一元的に定義する。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、
/// 呼び出し側では logger 引数を省略する（例: <c>_log.LogWatchdogEvacuate()</c>）。<br/>
/// ログ基盤（ファイル出力・レベル制御・非同期化）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// カーソル位置取得（GetCursorScreenPoint）やマウス移動に伴う高頻度呼び出しはログ対象外（ノイズ防止）。
/// </remarks>
internal static partial class PlatformLog
{
    /// <summary>UI ハング検知によるオーバーレイ退避の警告ログ。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[Watchdog] UI ハングを検知。オーバーレイを退避します</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Warning, "[Watchdog] UI ハングを検知。オーバーレイを退避します")]
    public static partial void LogWatchdogEvacuate(this ILogger logger);

    /// <summary>カーソル表示/非表示の状態変更を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 表示状態が変化したときのみ呼び出される（同じ状態の再設定はスキップされる）。<br/>
    /// 出力書式: <c>[Cursor] 表示状態変更: show={show}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="show">変更後の表示状態（true=表示, false=非表示）。</param>
    [LoggerMessage(LogLevel.Information, "[Cursor] 表示状態変更: show={Show}")]
    public static partial void LogCursorVisibilityChanged(this ILogger logger, bool show);

    /// <summary>ウィンドウをタスクバー / Alt+Tab から除外したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[WindowService] HideFromTaskSwitcher: hwnd=0x{Hwnd}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    [LoggerMessage(LogLevel.Information, "[WindowService] HideFromTaskSwitcher: hwnd=0x{Hwnd}")]
    public static partial void LogWindowHiddenFromTaskSwitcher(this ILogger logger, string hwnd);

    /// <summary>キャプチャ除外設定の変更を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[WindowService] SetCaptureExclusion: hwnd=0x{Hwnd}, exclude={Exclude}, success={Success}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    /// <param name="exclude">キャプチャ除外する場合は true。</param>
    /// <param name="success">API 呼び出しの成否。</param>
    [LoggerMessage(LogLevel.Information, "[WindowService] SetCaptureExclusion: hwnd=0x{Hwnd}, exclude={Exclude}, success={Success}")]
    public static partial void LogCaptureExclusionChanged(this ILogger logger, string hwnd, bool exclude, bool success);

    /// <summary>UI ハング時にウィンドウを退避（最前面解除・非表示・WM_CLOSE）したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[WindowService] EvacuateWindow: hwnd=0x{Hwnd}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    [LoggerMessage(LogLevel.Warning, "[WindowService] EvacuateWindow: hwnd=0x{Hwnd}")]
    public static partial void LogWindowEvacuated(this ILogger logger, string hwnd);
}

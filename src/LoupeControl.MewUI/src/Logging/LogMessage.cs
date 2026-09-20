using Microsoft.Extensions.Logging;

namespace Estable.MewUI.Loupe.Logging;

/// <summary>LoupeControl（MewUI 版）の LoggerMessage 定義（ソース生成ログ・拡張メソッド）。</summary>
/// <remarks>
/// 設定値の変更を Debug、描画の例外を Error で記録する（Avalonia 版と同一の書式）。<br/>
/// MewUI 版は中間ビットマップを作らず同期描画するため、Avalonia 版の再描画パイプライン<br/>
/// （RequestRender / RenderCore / GenerateAsyncDone の Trace）に相当するログは持たない。<br/>
/// ログ基盤はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class LogMessage
{
    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>ルーペ描画の例外エラーログ。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>[LoupeCtrl] Update exception</c>（例外スタックトレース付き）
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    [LoggerMessage(LogLevel.Error, "[LoupeCtrl] Update exception")]
    public static partial void LogUpdateError(this ILogger logger, Exception exception);
}

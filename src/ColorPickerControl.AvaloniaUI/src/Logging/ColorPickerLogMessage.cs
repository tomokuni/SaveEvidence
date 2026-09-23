using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.ColorPicker.Logging;

/// <summary>ColorPickerControl の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 色選択・Flyout 開閉・入力イベント・設定変更をログ出力する。<br/>
/// ログ基盤はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class ColorPickerLogMessage
{
    /// <summary>選択色変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorPicker] SelectedColor: {Old} → {New}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の色。</param>
    /// <param name="newValue">変更後の色。</param>
    [LoggerMessage(LogLevel.Debug, "[ColorPicker] SelectedColor: {OldValue} → {NewValue}")]
    public static partial void LogSelectedColorChanged(this ILogger logger, string oldValue, string newValue);

    /// <summary>カラーピッカー Flyout を開くボタンのクリックログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorPicker] OpenFlyout: color={Color}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="color">現在の選択色。</param>
    [LoggerMessage(LogLevel.Debug, "[ColorPicker] OpenFlyout: color={Color}")]
    public static partial void LogOpenFlyout(this ILogger logger, string color);

    /// <summary>Flyout の OK ボタンで色が確定されたログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorPicker] Flyout OK: color={Color}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="color">確定された色。</param>
    [LoggerMessage(LogLevel.Debug, "[ColorPicker] Flyout OK: color={Color}")]
    public static partial void LogFlyoutOk(this ILogger logger, string color);

    /// <summary>Flyout のキャンセルボタンでキャンセルされたログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorPicker] Flyout Cancel</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "[ColorPicker] Flyout Cancel")]
    public static partial void LogFlyoutCancel(this ILogger logger);

    /// <summary>フォーカス喪失時のログ（無効色の復帰等）。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[ColorPicker] LostFocus: color={Color} valid={Valid}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="color">現在の色。</param>
    /// <param name="valid">色が有効かどうか。</param>
    [LoggerMessage(LogLevel.Trace, "[ColorPicker] LostFocus: color={Color} valid={Valid}")]
    public static partial void LogLostFocus(this ILogger logger, string color, bool valid);
}

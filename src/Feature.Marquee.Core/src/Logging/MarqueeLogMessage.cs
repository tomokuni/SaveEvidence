using System.Globalization;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using EsUtil.Feature.Marquee.Core.Decoration;
using EsUtil.Feature.Marquee.Core.Setting;

namespace EsUtil.Feature.Marquee.Core.Logging;

/// <summary>Marquee の操作（入力の判断・設定変更）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 入力に伴う判断（ドラッグ開始・確定・キャンセル・クリック・キー処理）はコアの状態管理クラスが担うため、<br/>
/// その経過のログ定義もコアが所有する。<br/>
/// <b>ホイール要求のログは本層が所有しない</b>（ズーム・スクロールはコントロール化のための要求であり、<br/>
/// 入力連携層が単一所有する。本クラスは入力判断のログだけを持つ）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// <b>可視性</b>: 本定義を呼ぶのはコアの状態機械だけであり、UI 層・ホストはロガー（<see cref="Marquee.Log"/>）を渡すだけでよいため internal とする。
/// </remarks>
internal static partial class MarqueeLogMessage
{
    /// <summary>ポインター押下（マウス Down）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="clickCount">クリック数（2 = ダブルクリック）。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}")]
    internal static partial void LogMouseDown(this ILogger logger, double x, double y, int clickCount, bool hasSelection);

    /// <summary>ポインター解放（マウス Up）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="wasDragging">ドラッグ中だったかどうか。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}")]
    internal static partial void LogMouseUp(this ILogger logger, double x, double y, bool wasDragging, bool hasSelection);

    /// <summary>ダブルクリックのログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] DoubleClick: pos=({X}, {Y}) inside={Inside}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="inside">選択矩形の内側かどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] DoubleClick: pos=({X}, {Y}) inside={Inside}")]
    internal static partial void LogDoubleClick(this ILogger logger, double x, double y, bool inside);

    /// <summary>マウス押下直前の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Feature.Marquee.Core] Position before MouseDown: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[Feature.Marquee.Core] Position before MouseDown: pos=({X}, {Y})")]
    internal static partial void LogPositionBeforeDown(this ILogger logger, double x, double y);

    /// <summary>マウス解放直後の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Feature.Marquee.Core] Position after MouseUp: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[Feature.Marquee.Core] Position after MouseUp: pos=({X}, {Y})")]
    internal static partial void LogPositionAfterUp(this ILogger logger, double x, double y);

    /// <summary>マウスドラッグ開始（新規選択・ハンドル操作）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] DragStart: handle={Handle} pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="handle">操作対象のハンドル（None = 新規選択）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] DragStart: handle={Handle} pos=({X}, {Y})")]
    internal static partial void LogDragStart(this ILogger logger, string handle, double x, double y);

    /// <summary>キー入力（矢印キー nudge・Enter 確定・Escape キャンセル）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] KeyDown: key={Key} handled={Handled}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="key">押下されたキー。</param>
    /// <param name="handled">処理されたかどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] KeyDown: key={Key} handled={Handled}")]
    internal static partial void LogKeyDown(this ILogger logger, string key, bool handled);

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] ConfirmRequested: rect={Rect}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="rect">確定する選択矩形（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] ConfirmRequested: rect={Rect}")]
    internal static partial void LogConfirmRequested(this ILogger logger, string rect);

    /// <summary>キャンセル要求（Escape・枠外ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] CancelRequested</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] CancelRequested")]
    internal static partial void LogCancelRequested(this ILogger logger);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Core] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Core] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>整数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同じ書式で出力する。<br/>
    /// 「変化したときだけ出力する」判断と数値の文字列化は <see cref="LogSettingChangedIfChanged"/> が単一所有する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, int oldValue, int newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>実数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks><see cref="LogSettingChanged(ILogger, string, int, int)"/> の実数版（書式・規則とも同一）。</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, double oldValue, double newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>真偽設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// <see cref="LogSettingChanged(ILogger, string, int, int)"/> の真偽版。<br/>
    /// <c>bool</c> は <see cref="IFormattable"/> を実装しないため本体の汎用版へは委譲できない（書式は型の標準表記）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, bool oldValue, bool newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSettingChanged(name, oldValue.ToString(), newValue.ToString());
    }

    /// <summary>数値設定値の変更ログ（同値の再設定では出力しない）の本体。</summary>
    /// <typeparam name="T">数値の型（整数・実数）。</typeparam>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    /// <remarks>
    /// 「同値なら出力しない」判定と、カルチャに依存しない数値の文字列化を単一所有する<br/>
    /// （整数版・実数版が同じ規則を別々に持つと、片方だけ非カルチャ非依存のまま残る）。
    /// </remarks>
    private static void LogSettingChangedIfChanged<T>(this ILogger logger, string name, T oldValue, T newValue)
        where T : struct, IEquatable<T>, IFormattable
    {
        if (oldValue.Equals(newValue))
        {
            return;
        }

        logger.LogSettingChanged(
            name,
            oldValue.ToString(format: null, CultureInfo.InvariantCulture),
            newValue.ToString(format: null, CultureInfo.InvariantCulture));
    }

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）のログ（矩形の整形を含む）。</summary>
    /// <remarks>
    /// <see cref="LogConfirmRequested(ILogger, string)"/> と同じ書式で出力する。<br/>
    /// 矩形の整形（<c>(X, Y, W, H)</c>）は <see cref="FormatRect"/> が単一所有する
    /// （整形を呼び出し側へ置くと、同じ矩形が経路によって別の書式で出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="rect">確定する選択矩形。</param>
    internal static void LogConfirmRequested(this ILogger logger, EsRect rect)
        => logger.LogConfirmRequested(FormatRect(rect));

    /// <summary>設定（<see cref="MarqueeOptions"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Debug（項目ごとの書式は個別の変更ログと同一）<br/>
    /// 設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （UI 層は前後の値を渡さず、コアが保持する値の変化を知るだけでよい）。<br/>
    /// 外観の再適用で設定数ぶんのログが溢れないよう、各項目のログが同値判定を行う（変化した項目だけが出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の設定。</param>
    /// <param name="newValue">変更後の設定。</param>
    internal static void LogOptionsChanged(this ILogger logger, MarqueeOptions oldValue, MarqueeOptions newValue)
    {
        logger.LogSettingChanged(nameof(MarqueeOptions.ShowHandles), oldValue.ShowHandles, newValue.ShowHandles);
        logger.LogSettingChanged(nameof(MarqueeOptions.HandleSize), oldValue.HandleSize, newValue.HandleSize);
        logger.LogSettingChanged(nameof(MarqueeOptions.MarkerLength), oldValue.MarkerLength, newValue.MarkerLength);
        logger.LogSettingChanged(nameof(MarqueeOptions.ShowCoordLabel), oldValue.ShowCoordLabel, newValue.ShowCoordLabel);
        logger.LogSettingChanged(nameof(MarqueeOptions.ShowSizeLabel), oldValue.ShowSizeLabel, newValue.ShowSizeLabel);
        logger.LogSettingChanged(nameof(MarqueeOptions.HandleHitRadius), oldValue.HandleHitRadius, newValue.HandleHitRadius);
        logger.LogSettingChanged(nameof(MarqueeOptions.MinSelectionSize), oldValue.MinSelectionSize, newValue.MinSelectionSize);
        logger.LogSettingChanged(nameof(MarqueeOptions.ClampToDomainBounds), oldValue.ClampToDomainBounds, newValue.ClampToDomainBounds);
    }

    /// <summary>装飾の設定（<see cref="MarqueeDecoOptions"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Debug（書式は設定の変更ログと同一）<br/>
    /// 装飾も 1 つの値へ統合したため、変更された項目の判定（差分検出）を本メソッドが単一所有する。<br/>
    /// ハイライト矩形は値の同一性（設定の有無）だけを記録する（座標の変更は操作中に高頻度で起こりうるため）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の装飾の設定。</param>
    /// <param name="newValue">変更後の装飾の設定。</param>
    internal static void LogDecoOptionsChanged(
        this ILogger logger, MarqueeDecoOptions oldValue, MarqueeDecoOptions newValue)
    {
        logger.LogSettingChanged(nameof(MarqueeDecoOptions.ShowOverlay), oldValue.ShowOverlay, newValue.ShowOverlay);
        logger.LogSettingChanged(nameof(MarqueeDecoOptions.ShowOuterBorder), oldValue.ShowOuterBorder, newValue.ShowOuterBorder);
        logger.LogSettingChanged(nameof(MarqueeDecoOptions.OuterShowFullOverlay), oldValue.OuterShowFullOverlay, newValue.OuterShowFullOverlay);
        logger.LogSettingChanged(nameof(MarqueeDecoOptions.ShowInnerFill), oldValue.ShowInnerFill, newValue.ShowInnerFill);
        logger.LogSettingChanged(nameof(MarqueeDecoOptions.ShowInnerBorder), oldValue.ShowInnerBorder, newValue.ShowInnerBorder);
        logger.LogSettingChanged(
            $"{nameof(MarqueeDecoOptions.OuterAppearance)}.{nameof(MarqueeOuterDecoAppearance.BorderThickness)}",
            oldValue.OuterAppearance.BorderThickness,
            newValue.OuterAppearance.BorderThickness);
        logger.LogSettingChanged(
            $"{nameof(MarqueeDecoOptions.InnerAppearance)}.{nameof(MarqueeInnerDecoAppearance.BorderThickness)}",
            oldValue.InnerAppearance.BorderThickness,
            newValue.InnerAppearance.BorderThickness);
        logger.LogSettingChanged(
            nameof(MarqueeDecoOptions.OuterHighlightRect),
            oldValue.OuterHighlightRect is not null,
            newValue.OuterHighlightRect is not null);
    }

    /// <summary>矩形を <c>(X, Y, W, H)</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形。</param>
    /// <returns><c>(X, Y, W, H)</c> 形式の文字列。</returns>
    /// <remarks>数値の書式は現在のカルチャに依存しない（ログを環境非依存にする）。</remarks>
    private static string FormatRect(EsRect rect)
        => $"({rect.X.ToString(CultureInfo.InvariantCulture)}, {rect.Y.ToString(CultureInfo.InvariantCulture)}, {rect.Width.ToString(CultureInfo.InvariantCulture)}, {rect.Height.ToString(CultureInfo.InvariantCulture)})";
}

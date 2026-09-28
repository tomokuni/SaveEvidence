using System.Globalization;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.Presenter;

/// <summary>選択矩形コントロールの提示層が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 本層が単一所有する設定（外観 = <see cref="MarqueeAppearance"/> と表示スケーリング係数）の変更を記録する。<br/>
/// <b>「値を所有する主体が記録する」規則</b>により、コアが所有する設定（<c>MarqueeOptions</c> / 倍率 / ハイライト矩形）は<br/>
/// コアのログ定義（<c>Feature.Marquee.Core</c> の <c>MarqueeLogMessage</c>）が記録し、本層は重複して記録しない<br/>
/// （どの層まで値が伝搬したかをログの接頭辞から追える）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class MarqueePresenterLogMessage
{
    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Presenter] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Presenter] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>整数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, int oldValue, int newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>実数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, double oldValue, double newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>真偽設定値の変更ログ（同値の再設定では出力しない）。</summary>
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

    /// <summary>色設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の色。</param>
    /// <param name="newValue">変更後の色。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, DrawingColor oldValue, DrawingColor newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSettingChanged(name, oldValue.ToHexText(), newValue.ToHexText());
    }

    /// <summary>外観（<see cref="MarqueeAppearance"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Debug（書式は個別の変更ログと同一）<br/>
    /// 外観を 1 つの値へ統合しているため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （ホストは前後の値を渡さず、値を保持する提示層が変化を知るだけでよい）。<br/>
    /// 外観の再適用で項目数ぶんのログが溢れないよう、各項目のログが同値判定を行う（変化した項目だけが出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の外観。</param>
    /// <param name="newValue">変更後の外観。</param>
    internal static void LogAppearanceChanged(this ILogger logger, MarqueeAppearance oldValue, MarqueeAppearance newValue)
    {
        logger.LogSettingChanged(nameof(MarqueeAppearance.HandleColor), oldValue.HandleColor, newValue.HandleColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.HandleBorderColor), oldValue.HandleBorderColor, newValue.HandleBorderColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.MarkerColor), oldValue.MarkerColor, newValue.MarkerColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.SizeLabelColor), oldValue.SizeLabelColor, newValue.SizeLabelColor);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.SizeLabelBackgroundColor), oldValue.SizeLabelBackgroundColor, newValue.SizeLabelBackgroundColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.CoordLabelColor), oldValue.CoordLabelColor, newValue.CoordLabelColor);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.CoordLabelBackgroundColor), oldValue.CoordLabelBackgroundColor, newValue.CoordLabelBackgroundColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.OverlayColor), oldValue.OverlayColor, newValue.OverlayColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.OuterBorderColor), oldValue.OuterBorderColor, newValue.OuterBorderColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.InnerFillColor), oldValue.InnerFillColor, newValue.InnerFillColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.InnerBorderColor), oldValue.InnerBorderColor, newValue.InnerBorderColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.SizeLabelFontSize), oldValue.SizeLabelFontSize, newValue.SizeLabelFontSize);
        logger.LogSettingChanged(nameof(MarqueeAppearance.CoordLabelFontSize), oldValue.CoordLabelFontSize, newValue.CoordLabelFontSize);
        logger.LogSettingChanged(nameof(MarqueeAppearance.LabelPadding), oldValue.LabelPadding, newValue.LabelPadding);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowHandles), oldValue.ShowHandles, newValue.ShowHandles);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowSizeLabel), oldValue.ShowSizeLabel, newValue.ShowSizeLabel);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowCoordLabel), oldValue.ShowCoordLabel, newValue.ShowCoordLabel);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowOverlay), oldValue.ShowOverlay, newValue.ShowOverlay);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowOuterBorder), oldValue.ShowOuterBorder, newValue.ShowOuterBorder);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowFullOverlay), oldValue.ShowFullOverlay, newValue.ShowFullOverlay);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowInnerFill), oldValue.ShowInnerFill, newValue.ShowInnerFill);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowInnerBorder), oldValue.ShowInnerBorder, newValue.ShowInnerBorder);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.OuterBorderThickness), oldValue.OuterBorderThickness, newValue.OuterBorderThickness);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.InnerBorderThickness), oldValue.InnerBorderThickness, newValue.InnerBorderThickness);
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
}

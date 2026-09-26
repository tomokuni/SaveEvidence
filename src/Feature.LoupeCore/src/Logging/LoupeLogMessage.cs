using System.Globalization;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore.Logging;

/// <summary>ルーペのログ定義（ソース生成ログ・拡張メソッド）。</summary>
/// <remarks>
/// 設定値の変更・描画の例外・再描画の進行は、<b>両 UI で同一のレベル・書式にす る必要がある</b>ため<br/>
/// コアが単一所有する（UI 実装ごとに持つと書式がずれ、片方だけ修正される）。<br/>
/// 値の文字列化（<c>(X, Y)</c> / <c>WidthxHeight</c>）も <see cref="LoupeLogFormat"/> が行い、UI 側に整形処理を置かない。<br/>
/// 「変化したときだけ出力する」判断（同値の再設定・同一画像の再設定）も本クラスが単一所有する（UI 層に同値判定を置かない）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一 引数）として定義し、呼び出し側では logger 引数を省略する。<br/>
/// <b>可視性</b>: <b>UI が呼ぶ定義だけを public</b> とし、<b>コアが出力する定義は internal</b> とする<br/>
/// （設定・環境値・拡大中心の変更はコアが検出して出力するため、UI から呼ぶ必要がない）。<br/>
/// <b>UI 固有のログは各 UI プロジェクトが所有する</b>（中間ビットマップを生成する描画パイプライン固有のログ等）。
/// </remarks>
public static partial class LoupeLogMessage
{
    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>整数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同じ書式で出力する。<br/>
    /// 「変化したときだけ出力する」判断と数値の文字列化は <see cref="LogSettingChangedIfChanged"/> が単一所有する<br/>
    /// （外観設定の再適用でログが溢れるのを防ぐ規則）。
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

    /// <summary>拡大中心（ドメイン座標）の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[LoupeCtrl] Setting: CenterPoint {OldValue} → {NewValue}</c><br/>
    /// 中心はマウス移動のたびに設定されるため、「変化したときだけ出力する」判断を本メソッドが単一所有する<br/>
    /// （出力をコアが行うため、UI 層は同値判定を持たない）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の拡大中心（未設定は null）。</param>
    /// <param name="newValue">変更後の拡大中心（未設定は null）。</param>
    internal static void LogCenterPointChanged(this ILogger logger, EsPoint? oldValue, EsPoint? newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        LogSettingChanged(logger, "CenterPoint", LoupeLogFormat.FormatPoint(oldValue), LoupeLogFormat.FormatPoint(newValue));
    }

    /// <summary>拡大元画像の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[LoupeCtrl] Setting: Source {OldValue} → {NewValue}</c><br/>
    /// 「別のインスタンスに差し替わったか」の判断（参照の同一性）を本メソッドが単一所有し、<br/>
    /// UI 層は設定の前後を渡すだけでよい（寸法が同じ別画像への差し替えは変更として記録する）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の拡大元画像（未設定は null）。</param>
    /// <param name="newValue">変更後の拡大元画像（未設定は null）。</param>
    public static void LogSourceChanged(this ILogger logger, EsBitmap? oldValue, EsBitmap? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        logger.LogSourceChanged(
            LoupeLogFormat.DescribeImage(oldValue?.Width, oldValue?.Height),
            LoupeLogFormat.DescribeImage(newValue?.Width, newValue?.Height));
    }

    /// <summary>拡大元画像の変更ログ（画像の寸法は各 UI が整形して渡す）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[LoupeCtrl] Setting: Source {OldValue} → {NewValue}</c><br/>
    /// 画像の型は UI ごとに異なるため、<br/>
    /// 寸法の取り出しは各 UI が担い、書式（<see cref="LoupeLogFormat.DescribeImage"/>）と設定名はコアが単一所有する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の画像の寸法（未設定は null）。</param>
    /// <param name="newValue">変更後の画像の寸法（未設定は null）。</param>
    public static void LogSourceChanged(this ILogger logger, string oldValue, string newValue)
        => LogSettingChanged(logger, "Source", oldValue, newValue);

    /// <summary>ルーペの基本サイズ（DIP）の変更ログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[LoupeCtrl] Setting: RequestedSize {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の基本サイズ。</param>
    /// <param name="newValue">変更後の基本サイズ。</param>
    internal static void LogRequestedSizeChanged(this ILogger logger, int oldValue, int newValue)
        => LogSettingChanged(logger, "RequestedSize", oldValue, newValue);

    /// <summary>設定（<see cref="LoupeOptions"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Debug（項目ごとの書式は個別の変更ログと同一）<br/>
    /// 設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （UI 層は前後の値を渡さず、コアが保持する値の変化を知るだけでよい）。<br/>
    /// 寸法（<see cref="LoupeOptions.CheckerSize"/> / <see cref="LoupeOptions.FrameWidth"/>）とラベル表示の可否は変更ログの対象外とする（外観の再適用で設定数ぶんのログが溢れるため）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の設定。</param>
    /// <param name="newValue">変更後の設定。</param>
    internal static void LogOptionsChanged(this ILogger logger, LoupeOptions oldValue, LoupeOptions newValue)
    {
        // 各項目のログが同値判定を行うため、変化していない項目は出力されない
        logger.LogRequestedSizeChanged(oldValue.RequestedSize, newValue.RequestedSize);
        logger.LogZoomLevelChanged(oldValue.ZoomLevel, newValue.ZoomLevel);
    }

    /// <summary>表示スケーリング係数の変更ログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[LoupeCtrl] Setting: RenderScaling {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前のスケーリング係数。</param>
    /// <param name="newValue">変更後のスケーリング係数。</param>
    internal static void LogRenderScalingChanged(this ILogger logger, double oldValue, double newValue)
        => LogSettingChanged(logger, "RenderScaling", oldValue, newValue);

    /// <summary>拡大率の変更ログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[LoupeCtrl] Setting: ZoomLevel {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の拡大率。</param>
    /// <param name="newValue">変更後の拡大率。</param>
    internal static void LogZoomLevelChanged(this ILogger logger, int oldValue, int newValue)
        => LogSettingChanged(logger, "ZoomLevel", oldValue, newValue);

    /// <summary>ルーペ描画の例外エラーログ。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[LoupeCtrl] Update exception</c>（例外スタックトレース付き）</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    [LoggerMessage(LogLevel.Error, "[LoupeCtrl] Update exception")]
    public static partial void LogUpdateError(this ILogger logger, Exception exception);
}

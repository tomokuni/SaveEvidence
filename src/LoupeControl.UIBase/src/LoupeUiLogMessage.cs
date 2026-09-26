using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの UI が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 拡大元画像の変更と描画の例外は <b>両 UI が同じ書式・レベルで出力する必要がある</b>ため本層が単一所有する<br/>
/// （UI 実装ごとに持つと書式がずれ、片方だけ修正される）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。<br/>
/// <b>コアが出力する定義（設定値・拡大中心の変更）はコアが持つ</b>: 本クラスは UI が出力する定義だけを持つ。
/// </remarks>
public static partial class LoupeUiLogMessage
{
    /// <summary>拡大元画像の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の拡大元画像（未設定は null）。</param>
    /// <param name="newValue">変更後の拡大元画像（未設定は null）。</param>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Feature.LoupeCore] Setting: Source {OldValue} → {NewValue}</c><br/>
    /// 「別のインスタンスに差し替わったか」の判断（参照の同一性）を本メソッドが単一所有し、<br/>
    /// UI 層は設定の前後を渡すだけでよい（寸法が同じ別画像への差し替えは変更として記録する）。
    /// </remarks>
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
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の画像の寸法（未設定は null）。</param>
    /// <param name="newValue">変更後の画像の寸法（未設定は null）。</param>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Feature.LoupeCore] Setting: Source {OldValue} → {NewValue}</c><br/>
    /// 画像の型は UI ごとに異なるため、寸法の取り出しは各 UI が担う（書式は <see cref="LoupeLogFormat.DescribeImage"/> が単一所有する）。
    /// </remarks>
    public static void LogSourceChanged(this ILogger logger, string oldValue, string newValue)
        => logger.LogSettingChanged("Source", oldValue, newValue);

    /// <summary>設定変更のログ（書式はコアの設定変更ログと同一に保つ）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.LoupeCore] Setting: {Name} {OldValue} → {NewValue}")]
    private static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>ルーペ描画の例外エラーログ。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <remarks>
    /// LogLevel: Error<br/>出力書式: <c>[Feature.LoupeCore] Update exception</c>（例外スタックトレース付き）<br/>
    /// 例外の記録は UI の描画パイプラインが行う（コアは描画 API を持たず例外を検知しない）。
    /// </remarks>
    [LoggerMessage(LogLevel.Error, "[Feature.LoupeCore] Update exception")]
    public static partial void LogUpdateError(this ILogger logger, Exception exception);
}

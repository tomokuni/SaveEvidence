using Microsoft.Extensions.Logging;
using MarqueeFacade = EsUtil.Feature.Marquee.Core.Marquee;

namespace EsUtil.Feature.Marquee.Presenter;

/// <summary>ホイール要求（コントロール化）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// ズーム要求のログは、<b>両 UI で同一の書式・レベルにする必要がある</b>ため本層が単一所有する<br/>
/// （UI 実装ごとに持つと書式がずれ、片方だけ修正される）。<br/>
/// ロガーはコアのファサード（<see cref="MarqueeFacade.Log"/>）から取得し、ホストのログ基盤（ファイル出力・レベル制御）を共用する。<br/>
/// ログ基盤はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。<br/>
/// <b>外部公開しない</b>: ホストはロガーを <see cref="MarqueeFacade.Log"/> へ渡すだけでよく、ログ定義名を参照する必要はない。
/// </remarks>
internal static partial class MarqueeWheelLogMessage
{
    /// <summary>Ctrl+ホイールによるズーム要求のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Feature.Marquee.Presenter] ZoomRequested: zoomIn={ZoomIn}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    [LoggerMessage(LogLevel.Debug, "[Feature.Marquee.Presenter] ZoomRequested: zoomIn={ZoomIn}")]
    public static partial void LogZoomRequested(this ILogger logger, bool zoomIn);
}

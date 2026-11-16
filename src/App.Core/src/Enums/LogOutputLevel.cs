using System.ComponentModel;

namespace EsUtil.SaveEvidence.App.Core.Enums;

/// <summary>ログの出力レベル（このレベル以上の記録だけをファイルへ書く）。</summary>
/// <remarks>
/// <b>値は重大度の順</b>（<see cref="Trace"/> が最も詳しく、<see cref="Critical"/> が最も重大）であり、
/// <c>Microsoft.Extensions.Logging.LogLevel</c> と同じ並びにする（Serilog の <c>LogEventLevel</c> へ
/// 1 対 1 で写せるようにするため。 写像は <c>Logging.AppLogging</c> が単一所有する）。<br/>
/// 設定ページの選択肢の表示名は <see cref="DescriptionAttribute"/> が単一所有する
/// （読み出しは <c>Services.EnumDisplayNames</c>。 UI 版ごとに文言を書き分けない）。<br/>
/// 絞り込みの規則は <c>.github/ログ規約.md</c> §2 が定める。
/// </remarks>
public enum LogOutputLevel
{
    /// <summary>すべて出力する（トレースを含む。 既定）。</summary>
    [Description("すべて（トレース）")]
    Trace = 0,

    /// <summary>デバッグ以上を出力する。</summary>
    [Description("デバッグ")]
    Debug = 1,

    /// <summary>情報以上を出力する。</summary>
    [Description("情報")]
    Information = 2,

    /// <summary>警告以上を出力する。</summary>
    [Description("警告")]
    Warning = 3,

    /// <summary>エラー以上を出力する。</summary>
    [Description("エラー")]
    Error = 4,

    /// <summary>重大な障害だけを出力する。</summary>
    [Description("重大")]
    Critical = 5,
}

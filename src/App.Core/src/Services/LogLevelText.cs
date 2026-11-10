namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ログのレベルを 3 文字の表記（<c>[TRC]</c> 等）へ写す。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: ログの行は <c>[TRC]</c> / <c>[DBG]</c> / <c>[INF]</c> / <c>[WRN]</c> / <c>[ERR]</c> /
/// <c>[CRT]</c> の 3 文字でレベルを表す（規則の所有者は <c>.github/ログ規約.md</c> §4.1）。<br/>
/// Serilog の <c>{Level:u3}</c> は <c>Verbose</c> を <c>VRB</c> と表すため使えない（<c>TRC</c> にならない）。<br/>
/// 写す規則は<b>本クラスが単一所有する</b>（4 つの UI 版が同じ表記になる）。
/// <b>Serilog に依存しない</b>: レベル名の文字列だけを受け取る（各 UI 版の <c>AppLog</c> が
/// Serilog のレベルから名前を取り出して渡す）。
/// </remarks>
internal static class LogLevelText
{
    /// <summary>ログ イベントのプロパティ名（出力テンプレートの <c>{LogLevelText}</c> が読む）。</summary>
    public const string PropertyName = "LogLevelText";

    /// <summary>レベル名を 3 文字の表記へ写す。</summary>
    /// <param name="levelName">レベル名（<c>Trace</c> / <c>Debug</c> / <c>Information</c> / <c>Warning</c> / <c>Error</c> / <c>Critical</c>。
    /// Serilog の名前（<c>Verbose</c> / <c>Fatal</c>）も受け付ける）。</param>
    /// <returns>3 文字の表記（例: <c>TRC</c>）。 未知の名前は先頭 3 文字を大文字にしたもの。</returns>
    /// <remarks>
    /// <c>Trace</c> は Serilog では <c>Verbose</c>、<c>Critical</c> は <c>Fatal</c> に対応する<br/>
    /// （<c>Microsoft.Extensions.Logging</c> と Serilog で名前が違うため、両方を受け付ける）。
    /// </remarks>
    public static string Abbreviate(string levelName) => levelName switch
    {
        "Trace" or "Verbose" => "TRC",
        "Debug" => "DBG",
        "Information" => "INF",
        "Warning" => "WRN",
        "Error" => "ERR",
        "Critical" or "Fatal" => "CRT",
        _ => AbbreviateUnknown(levelName),
    };

    /// <summary>未知のレベル名を表記へ写す（先頭 3 文字・大文字）。</summary>
    /// <param name="levelName">レベル名。</param>
    /// <returns>3 文字以内の表記。 空の場合は <c>???</c>。</returns>
    /// <remarks>Serilog の 6 段階以外は現れないが、表記が空になるよりは名前の先頭を残す方が原因を追える。</remarks>
    private static string AbbreviateUnknown(string levelName)
        => string.IsNullOrEmpty(levelName)
            ? "???"
            : levelName[..Math.Min(3, levelName.Length)].ToUpperInvariant();
}

using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>ファイル名テンプレート文字列を解析し、実際の保存ファイル名を生成するユーティリティクラス。</summary>
/// <remarks>
/// プレースフォルダは <c>{ }</c> で囲われた文字列を対象とし、その内部のトークン（<c>date</c> / <c>time</c> / <c>yyyy</c> / <c>MM</c> / <c>dd</c> / <c>HH</c> / <c>mm</c> / <c>ss</c>）を日時値へ置換する。<br/>
/// 例: <c>{yyyyMMdd}</c> → <c>20260819</c>、<c>{HHmm}</c> → <c>2352</c>。<br/>
/// <c>{date}</c> は <c>yyyyMMdd</c>、<c>{time}</c> は <c>HHmmss</c> の省略形。<br/>
/// 中括弧自体を表す場合は <c>{{</c>（リテラル <c>{</c>）/ <c>}}</c>（リテラル <c>}</c>）としてエスケープする（例: <c>"{{date}}"</c> → <c>"{date}"</c>）。<br/>
/// テンプレートに含まれる右端の数値連続は、画像保存時に自動インクリメントされる通番として扱われる。<br/>
/// 例: <c>"screenshot_{date}_{time}_01.png"</c> → <c>"screenshot_20250101_120000_01.png"</c><br/>
/// ソースジェネレーターにより正規表現が最適化される（partial + GeneratedRegex）。<br/>
/// </remarks>
public sealed partial class FileNameTemplate
{
    private static readonly char[] s_invalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>エスケープ <c>{{</c> の一時トークン（置換処理中の衝突防止用・PUA）。</summary>
    private const string OpenBraceToken = "\uE000";

    /// <summary>エスケープ <c>}}</c> の一時トークン（置換処理中の衝突防止用・PUA）。</summary>
    private const string CloseBraceToken = "\uE001";

    /// <summary>日時トークンの定義（トークン名・日時書式・検証用サンプル値）。</summary>
    private static readonly (string Token, string Format, string Sample)[] s_tokens =
    [
        ("date", "yyyyMMdd", "20250101"),
        ("time", "HHmmss", "120000"),
        ("yyyy", "yyyy", "2025"),
        ("MM", "MM", "01"),
        ("dd", "dd", "01"),
        ("HH", "HH", "12"),
        ("mm", "mm", "00"),
        ("ss", "ss", "00"),
    ];

    /// <summary>テンプレートから実際のファイル名を生成する。 { } で囲われた領域内のトークン（date/time/yyyy/MM/dd/HH/mm/ss）を日時値へ置換する。<br/>{{ はリテラル {、}} はリテラル } として解決される。<br/>テンプレート内の数値はそのまま使用される（事前に IncrementRightmostNumber で更新済みであること）。</summary>
    /// <param name="template">テンプレート文字列</param>
    /// <param name="now">置換に使う日時。 null（既定）の場合は現在日時を使う。</param>
    /// <returns>生成されたファイル名</returns>
    /// <remarks>
    /// 日時を引数で受け取るのは、呼び出し側が時刻を固定して結果を再現できるようにするためである<br/>
    /// （本番の呼び出しは現在日時を使い、テストは固定値を渡して秒の境界に依存しない）。
    /// </remarks>
    public static string Generate(string template, DateTime? now = null)
    {
        var timestamp = now ?? DateTime.Now;
        return ReplacePlaceholders(template, t => timestamp.ToString(t.Format, CultureInfo.InvariantCulture));
    }

    /// <summary>テンプレート内の右端の数値連続を +1 し、桁数を維持する。 基本的には <see cref="decimal"/> で扱い、Decimal 型でも扱えない桁数の場合には <see cref="System.Numerics.BigInteger"/> として扱う。</summary>
    /// <param name="text">置換対象の文字列</param>
    /// <returns>置換後の文字列。 数値が見つからない場合は元の文字列をそのまま返す。</returns>
    /// <example>"screenshot_05.png" → "screenshot_06.png"</example>
    public static string IncrementRightmostNumber(string text)
    {
        var matches = NumberPattern().Matches(text);
        if (matches.Count == 0)
        {
            return text;
        }

        var lastMatch = matches[^1];
        var originalDigits = lastMatch.Value;
        var width = originalDigits.Length;

        string formattedNumber;

        // decimal でパースを試行
        if (decimal.TryParse(originalDigits, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var decValue))
        {
            decValue++;
            formattedNumber = decValue.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
        }
        // decimal でも扱えない桁数 → BigInteger
        else if (BigInteger.TryParse(originalDigits,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var bigValue))
        {
            bigValue++;
            formattedNumber = bigValue.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
        }
        else
        {
            return text;
        }

        var lastIndex = lastMatch.Index;
        return text[..lastIndex] + formattedNumber + text[(lastIndex + originalDigits.Length)..];
    }

    /// <summary>テンプレート内の数値の塊を検出する正規表現</summary>
    [GeneratedRegex("\\d+", RegexOptions.None, 100)]
    private static partial Regex NumberPattern();

    /// <summary>ファイル名テンプレートとして使用可能かどうかを検証する。 プレースホルダ（{ } 内のトークン）とエスケープ（{{ / }}）を解決し、右端数値を除去したとき、ファイル名に使用できない文字が含まれていないかを確認する。</summary>
    /// <param name="template">検証するテンプレート文字列</param>
    /// <param name="errorMessage">検証エラーメッセージ（無効な場合のみ設定）</param>
    /// <returns>テンプレートが有効な場合は true</returns>
    public static bool IsValidTemplate(string template, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(template))
        {
            errorMessage = "ファイル名テンプレートが空です。";
            return false;
        }

        // プレースホルダを値で仮置換して検証（サンプル値は定義配列から取得）
        var testName = ReplacePlaceholders(template, t => t.Sample);
        // 右端の数値連続も除去（桁数不定のため）
        testName = NumberPattern().Replace(testName, "0");

        var invalidChars = s_invalidFileNameChars;
        var foundChars = testName.Where(c => invalidChars.Contains(c)).Distinct().ToArray();
        if (foundChars.Length > 0)
        {
            var chars = string.Join(" ", foundChars.Select(static c => $"'{c}'"));
            errorMessage = $"ファイル名に使用できない文字 ({chars}) が含まれています。";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>エスケープ（<c>{{</c> → <c>{</c> / <c>}}</c> → <c>}</c>）を解決し、{ } 内の日時トークンを置換する。</summary>
    /// <param name="template">テンプレート文字列。</param>
    /// <param name="valueOf">トークン定義から置換値を生成する関数。</param>
    /// <returns>置換後の文字列。</returns>
    /// <remarks>
    /// <c>{{</c> / <c>}}</c> を一時トークンで保護してから <c>{ }</c> の領域を置換し、最後に復元する<br/>
    /// （エスケープ後の <c>{</c> / <c>}</c> がプレースホルダとして誤置換されるのを防ぐ）。<br/>
    /// トークンは <c>date</c> / <c>time</c> を先に、個別トークン（<c>yyyy</c> 等）を後に置換する<br/>
    /// （date の展開値に個別トークン名が含まれないため、二重置換は発生しない）。
    /// </remarks>
    private static string ReplacePlaceholders(string template, Func<(string Token, string Format, string Sample), string> valueOf)
    {
        var result = template
            .Replace("{{", OpenBraceToken, StringComparison.Ordinal)
            .Replace("}}", CloseBraceToken, StringComparison.Ordinal);

        // { } で囲われた領域を検出し、領域内のトークンを日時値へ置換する
        result = PlaceholderPattern().Replace(result, match =>
        {
            var content = match.Groups["content"].Value;
            foreach (var token in s_tokens)
            {
                content = content.Replace(token.Token, valueOf(token), StringComparison.Ordinal);
            }
            return content;
        });

        return result
            .Replace(OpenBraceToken, "{", StringComparison.Ordinal)
            .Replace(CloseBraceToken, "}", StringComparison.Ordinal);
    }

    /// <summary>{ } で囲われたプレースホルダ領域を検出する正規表現（入れ子なし・最長一致）。</summary>
    [GeneratedRegex("\\{(?<content>[^{}]*)\\}", RegexOptions.None, 100)]
    private static partial Regex PlaceholderPattern();
}

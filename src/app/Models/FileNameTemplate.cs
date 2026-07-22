using System.Text.RegularExpressions;

namespace app.Models;

/// <summary>
/// ファイル名テンプレート文字列を解析し、実際の保存ファイル名を生成するユーティリティクラス。
/// </summary>
/// <remarks>
/// テンプレート内の <c>{date}</c> は <c>yyyyMMdd</c>、<c>{time}</c> は <c>HHmmss</c> に置換される。<br/>
/// テンプレートに含まれる右端の数値連続は、画像保存時に自動インクリメントされる通番として扱われる。<br/>
/// 例: <c>"screenshot_{date}_{time}_01.png"</c> → <c>"screenshot_20250101_120000_01.png"</c><br/>
/// ソースジェネレーターにより正規表現が最適化される（partial + GeneratedRegex）。<br/>
/// </remarks>
public sealed partial class FileNameTemplate
{
    /// <summary>
    /// テンプレートから実際のファイル名を生成する。
    /// {date} → yyyyMMdd、{time} → HHmmss に置換される。
    /// テンプレート内の数値はそのまま使用される（事前に IncrementRightmostNumber で更新済みであること）。
    /// </summary>
    /// <param name="template">テンプレート文字列</param>
    /// <returns>生成されたファイル名</returns>
    public static string Generate(string template)
    {
        var now = DateTime.Now;
        return template
            .Replace("{date}", now.ToString("yyyyMMdd"))
            .Replace("{time}", now.ToString("HHmmss"));
    }

    /// <summary>
    /// テンプレート内の右端の数値連続を +1 し、桁数を維持する。
    /// 基本的には <see cref="decimal"/> で扱い、Decimal 型でも扱えない桁数の場合には
    /// <see cref="System.Numerics.BigInteger"/> として扱う。
    /// </summary>
    /// <param name="text">置換対象の文字列</param>
    /// <returns>置換後の文字列。数値が見つからない場合は元の文字列をそのまま返す。</returns>
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
        if (decimal.TryParse(originalDigits, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var decValue))
        {
            decValue++;
            formattedNumber = decValue.ToString().PadLeft(width, '0');
        }
        // decimal でも扱えない桁数 → BigInteger
        else if (System.Numerics.BigInteger.TryParse(originalDigits,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var bigValue))
        {
            bigValue++;
            formattedNumber = bigValue.ToString().PadLeft(width, '0');
        }
        else
        {
            return text;
        }

        var lastIndex = lastMatch.Index;
        return text[..lastIndex] + formattedNumber + text[(lastIndex + originalDigits.Length)..];
    }

    /// <summary>
    /// テンプレート内の数値の塊を検出する正規表現
    /// </summary>
    [GeneratedRegex("\\d+")]
    private static partial Regex NumberPattern();

    /// <summary>
    /// ファイル名テンプレートとして使用可能かどうかを検証する。
    /// {date}/{time} を置換し右端数値を除去したとき、ファイル名に使用できない文字が含まれていないかを確認する。
    /// </summary>
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

        // {date} / {time} をプレースホルダ値で仮置換して検証
        var testName = template
            .Replace("{date}", "20250101")
            .Replace("{time}", "120000");
        // 右端の数値連続も除去（桁数不定のため）
        testName = NumberPattern().Replace(testName, "0");

        var invalidChars = Path.GetInvalidFileNameChars();
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
}

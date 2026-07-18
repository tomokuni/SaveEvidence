using System.Text.RegularExpressions;

namespace app.Models;

/// <summary>
/// ファイル名テンプレートを解析し、実際のファイル名を生成するクラス
/// </summary>
public sealed partial class FileNameTemplate
{
    /// <summary>
    /// テンプレートから実際のファイル名を生成する。
    /// {date} → yyyyMMdd、{time} → HHmmss に置換される。
    /// 数値の塊はインクリメントされる。
    /// </summary>
    /// <param name="template">テンプレート文字列</param>
    /// <param name="currentNumber">現在の数値成分</param>
    /// <returns>生成されたファイル名</returns>
    public static string Generate(string template, int currentNumber)
    {
        var now = DateTime.Now;
        var result = template
            .Replace("{date}", now.ToString("yyyyMMdd"))
            .Replace("{time}", now.ToString("HHmmss"));

        // 右端の数値の塊を検出して置換
        result = IncrementRightmostNumber(result, currentNumber);

        return result;
    }

    /// <summary>
    /// テンプレート内の右端の数値の塊を検出して指定された数値で置換する。
    /// 数値の塊の桁数を維持する（例：テンプレートが "01" なら結果も "01"）。
    /// </summary>
    private static string IncrementRightmostNumber(string text, int number)
    {
        var matches = NumberPattern().Matches(text);
        if (matches.Count == 0)
        {
            return text;
        }

        var lastMatch = matches[^1];
        var originalDigits = lastMatch.Value;
        var width = originalDigits.Length;

        // 数値を指定された桁数でフォーマット
        var formattedNumber = number.ToString(width > 1 ? $"D{width}" : null);

        // 最後のマッチのみを置換（文字列の末尾から処理）
        var lastIndex = lastMatch.Index;
        return text[..lastIndex] + formattedNumber + text[(lastIndex + originalDigits.Length)..];
    }

    /// <summary>
    /// テンプレート文字列から現在の数値を抽出する。
    /// 最後の数値の塊の値を整数として返す。数値がない場合は 1 を返す。
    /// </summary>
    public static int ExtractCurrentNumber(string template)
    {
        var matches = NumberPattern().Matches(template);
        if (matches.Count == 0)
        {
            return 1;
        }

        var lastMatch = matches[^1];
        return int.Parse(lastMatch.Value);
    }

    /// <summary>
    /// テンプレート内の数値の塊を検出する正規表現
    /// </summary>
    [GeneratedRegex("\\d+")]
    private static partial Regex NumberPattern();
}

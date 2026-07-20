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
    /// 数値の塊はインクリメントされる。
    /// </summary>
    /// <param name="template">テンプレート文字列</param>
    /// <param name="currentNumber">現在の数値成分</param>
    /// <returns>生成されたファイル名</returns>
    public static string Generate(string template, int currentNumber)
    {
        // 数値プレースホルダを先に処理（{date}/{time} の数字が誤認識されるのを防止）
        var result = IncrementRightmostNumber(template, currentNumber);

        var now = DateTime.Now;
        result = result
            .Replace("{date}", now.ToString("yyyyMMdd"))
            .Replace("{time}", now.ToString("HHmmss"));

        return result;
    }

    /// <summary>
    /// テンプレート内の右端の数値連続を指定された数値で置換し、桁数を維持する。
    /// </summary>
    /// <param name="text">置換対象の文字列</param>
    /// <param name="number">埋め込む数値</param>
    /// <returns>置換後の文字列。数値が見つからない場合は元の文字列をそのまま返す。</returns>
    /// <example>"screenshot_01.png" に 5 を指定 → "screenshot_05.png"</example>
    public static string IncrementRightmostNumber(string text, int number)
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
    /// テンプレート文字列から右端の数値連続の値を整数として抽出する。
    /// </summary>
    /// <param name="template">テンプレート文字列</param>
    /// <returns>抽出された数値。数値が見つからない場合は 1。</returns>
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

using Avalonia.Media;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.ViewModels;

/// <summary>データ検証の結果 1 件分（画像 1 枚の検証結果）。</summary>
/// <param name="Name">データ名（画像ファイル名・拡張子なし）。</param>
/// <param name="IsCorrect">検出結果が正解データと一致したかどうか。</param>
/// <param name="DetailLines">4 辺（左X・上Y・右R・下B）の検証詳細行。 各行が 1 辺の情報を表す。</param>
/// <remarks>
/// <paramref name="DetailLines"/> は「左X: 検出=62 / 正解=62（差異なし）」のような 1 辺分の文字列を<br/>
/// 4 辺分（左X・上Y・右R・下B）格納する。<see cref="IsCorrect"/> が true の場合はすべて「差異なし」になる。
/// </remarks>
public sealed record VerifyResultItem(string Name, bool IsCorrect, IReadOnlyList<string> DetailLines)
{
    /// <summary>一致/不一致の表示テキスト。</summary>
    /// <value>「一致」または「不一致」。</value>
    public string StatusText => IsCorrect ? "一致" : "不一致";

    /// <summary>データ名のフォントウェイト（一致時は太字、不一致時は通常）。</summary>
    /// <value><see cref="FontWeight.Bold"/> または <see cref="FontWeight.Normal"/>。</value>
    public FontWeight NameFontWeight => IsCorrect ? FontWeight.Bold : FontWeight.Normal;
}

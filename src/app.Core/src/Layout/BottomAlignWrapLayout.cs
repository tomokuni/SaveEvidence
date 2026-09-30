using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Layout;

/// <summary>折り返しパネルの行高さ・下端揃えの配置計算（純粋関数・UI 非依存）。</summary>
/// <remarks>
/// <b>用途</b>: サムネイル一覧（フォルダビュー / Undo 一覧）のように行内のアイテム高さが異なる場合に、<br/>
/// 各アイテムの下端領域（ファイル名・ラベル）の高さ位置を揃えるための計算を提供する。<br/>
/// 標準の折り返しパネルは行内で上端揃えのため、高さが異なるアイテムでは下端のラベル位置が揃わない。<br/>
/// <br/>
/// <b>仕様</b>:<br/>
/// - 各行の高さは固定値とせず、その行の最大アイテム高さで決める。<br/>
/// - アイテムは各行内で下端揃えで配置する（ラベルの 1 文字目の高さ位置が行内で揃う）。<br/>
/// - 折り返しは利用可能幅に対して行い、先頭アイテムが幅を超える場合は単独行に置く。<br/>
/// <br/>
/// <b>所有</b>: Avalonia UI 版と MewUI 版の両方の折り返しパネルが本クラスへ委譲する（計算ロジックの単一所有）。
/// </remarks>
internal static class BottomAlignWrapLayout
{
    /// <summary>アイテムのサイズ群を幅で折り返し、推奨サイズを計算する。</summary>
    /// <param name="sizes">各アイテムの計測済みサイズ。</param>
    /// <param name="availableWidth">利用可能幅。</param>
    /// <param name="wrap">折り返すかどうか（幅が有限かつ正なら true）。</param>
    /// <param name="spacing">アイテム間の間隔。 0 以上の値。</param>
    /// <returns>推奨サイズ（幅 = 行の最大幅、高さ = 行高さの合計）。</returns>
    /// <remarks>アイテムが無い場合は空のサイズを返す。</remarks>
    public static EsSize Measure(IReadOnlyList<EsSize> sizes, double availableWidth, bool wrap, double spacing = 0)
    {
        double x = 0;
        double y = 0;
        double rowHeight = 0;
        double maxWidth = 0;
        var rowHasItems = false;

        foreach (var size in sizes)
        {
            // 行にアイテムがある場合は間隔を先に進める
            var advance = rowHasItems ? spacing + size.Width : size.Width;

            // 行が埋まったら次の行へ（先頭アイテムが幅超過の場合は単独行に置く）
            if (wrap && rowHasItems && x + advance > availableWidth)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
                advance = size.Width;
                rowHasItems = false;
            }

            x += advance;
            rowHeight = Math.Max(rowHeight, size.Height);
            maxWidth = Math.Max(maxWidth, x);
            rowHasItems = true;
        }

        return new EsSize(maxWidth, y + rowHeight);
    }

    /// <summary>アイテムのサイズ群を幅で折り返し、各行内で下端揃えした配置矩形を計算する。</summary>
    /// <param name="sizes">各アイテムの計測済みサイズ。</param>
    /// <param name="finalWidth">確定した配置幅。</param>
    /// <param name="spacing">アイテム間の間隔。 0 以上の値。</param>
    /// <returns>各アイテムの配置矩形（<paramref name="sizes"/> と同じ並び順）。</returns>
    /// <remarks>
    /// 行の高さは行内の最大アイテム高さで決め、各アイテムは「行の上端 + (行高さ − アイテム高さ)」へ配置する。<br/>
    /// そのため各行内でアイテムの下端が揃う。
    /// </remarks>
    public static IReadOnlyList<EsRect> Arrange(IReadOnlyList<EsSize> sizes, double finalWidth, double spacing = 0)
    {
        var result = new EsRect[sizes.Count];

        // 行単位にアイテムのインデックス群と行高さを収集する
        var rows = new List<(List<int> Indices, double RowHeight)>();

        var rowStart = 0;
        double x = 0;
        double rowHeight = 0;

        for (var i = 0; i < sizes.Count; i++)
        {
            var size = sizes[i];
            var advance = (x > 0 ? spacing : 0) + size.Width;

            // 行が埋まったら次の行へ（先頭アイテムが幅超過の場合は単独行に置く）
            if (x > 0 && x + advance > finalWidth)
            {
                rows.Add((RangeTo(rowStart, i), rowHeight));
                rowStart = i;
                x = 0;
                rowHeight = 0;
                advance = size.Width;
            }

            x += advance;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        if (rowStart < sizes.Count)
        {
            rows.Add((RangeTo(rowStart, sizes.Count), rowHeight));
        }

        // 各行内で下端揃えして配置する
        double y = 0;
        foreach (var (indices, height) in rows)
        {
            double rowX = 0;
            foreach (var index in indices)
            {
                var size = sizes[index];

                // 下端揃え: 行の上端から (行高さ − アイテム高さ) 下げた位置に配置する
                result[index] = new EsRect(rowX, y + height - size.Height, size.Width, size.Height);
                rowX += size.Width + spacing;
            }

            y += height;
        }

        return result;
    }

    /// <summary>[start, end) の連続インデックス群を生成する。</summary>
    /// <param name="start">開始インデックス（含む）。</param>
    /// <param name="end">終了インデックス（含まない）。</param>
    /// <returns>連続インデックスの一覧。</returns>
    private static List<int> RangeTo(int start, int end)
    {
        var list = new List<int>(end - start);
        for (var i = start; i < end; i++)
        {
            list.Add(i);
        }

        return list;
    }
}

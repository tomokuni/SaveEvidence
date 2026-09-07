using Avalonia;
using Avalonia.Controls;

namespace Estable.SaveEvidence.Controls;

/// <summary><see cref="BottomAlignWrapPanel"/> のレイアウト計算ロジック（純粋関数・UI 非依存）。</summary>
/// <remarks>
/// 幅で子を折り返し、各行の高さを「行内の最大アイテム高さ」で決定し、行内で下端揃えする配置計算を提供する。<br/>
/// <see cref="Control"/> の計測結果（<see cref="Size"/>）と配置結果（<see cref="Rect"/>）のみを扱い、<br/>
/// Avalonia のレイアウトエンジン（Measure / Arrange）に依存しない。<br/>
/// これにより UI 非依存の <c>[Fact]</c> で並列テストできる（UI ロジック分離・テスト並列性向上）。
/// </remarks>
internal static class BottomAlignWrapLayout
{
    /// <summary>子要素のサイズ群を幅で折り返し、推奨サイズ（幅 = 行の最大幅、高さ = 行高さの合計）を計算する。</summary>
    /// <param name="sizes">各子要素の計測済みサイズ。</param>
    /// <param name="availableWidth">利用可能幅。</param>
    /// <param name="wrap">折り返すかどうか（幅が有限かつ正なら true）。</param>
    /// <returns>推奨サイズ。</returns>
    /// <remarks>
    /// 各行の高さはその行の最大アイテム高さとし、合計を戻り値の高さとする。<br/>
    /// 折り返しは利用可能幅に対して行い、先頭アイテムが幅超過の場合は単独行に置く。
    /// </remarks>
    public static Size Measure(IReadOnlyList<Size> sizes, double availableWidth, bool wrap)
    {
        double x = 0;
        double y = 0;
        double rowHeight = 0;
        double maxWidth = 0;

        foreach (var size in sizes)
        {
            // 行が埋まったら次の行へ（先頭アイテムが幅超過の場合は単独行に置く）
            if (wrap && x > 0 && x + size.Width > availableWidth)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }

            x += size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
            maxWidth = Math.Max(maxWidth, x);
        }

        return new Size(maxWidth, y + rowHeight);
    }

    /// <summary>子要素のサイズ群を幅で折り返し、各行内で下端揃えした配置矩形を計算する。</summary>
    /// <param name="sizes">各子要素の計測済みサイズ。</param>
    /// <param name="finalWidth">確定した配置幅。</param>
    /// <returns>各子要素の配置矩形（子要素の並び順と同じインデックス）。</returns>
    /// <remarks>
    /// 行の高さは行内の最大アイテム高さで決め、各アイテムは「行の上端 + (行高さ − アイテム高さ)」へ配置<br/>
    /// されるため、行内で下端が揃う。
    /// </remarks>
    public static IReadOnlyList<Rect> Arrange(IReadOnlyList<Size> sizes, double finalWidth)
    {
        var result = new Rect[sizes.Count];

        // 行単位に子要素のインデックス群と行高さを収集する
        var rows = new List<(List<int> Indices, double RowHeight)>();

        int rowStart = 0;
        double x = 0;
        double rowHeight = 0;

        for (int i = 0; i < sizes.Count; i++)
        {
            var size = sizes[i];

            // 行が埋まったら次の行へ（先頭アイテムが幅超過の場合は単独行に置く）
            if (x > 0 && x + size.Width > finalWidth)
            {
                rows.Add((RangeTo(rowStart, i), rowHeight));
                rowStart = i;
                x = 0;
                rowHeight = 0;
            }

            x += size.Width;
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
                result[index] = new Rect(rowX, y + height - size.Height, size.Width, size.Height);
                rowX += size.Width;
            }
            y += height;
        }

        return result;
    }

    /// <summary>[start, end) の連続インデックス群を生成する。</summary>
    private static List<int> RangeTo(int start, int end)
    {
        var list = new List<int>(end - start);
        for (int i = start; i < end; i++)
        {
            list.Add(i);
        }
        return list;
    }
}

using Avalonia;
using Avalonia.Controls;

namespace Estable.SaveEvidence.Controls;

/// <summary>横方向に折り返し、各行内で子要素を下端揃えするパネル。</summary>
/// <remarks>
/// 用途: サムネイル一覧（FolderViewPage）のように行内のアイテム高さが異なる場合に、<br/>
/// 各アイテムの下端領域（ファイル名等）の高さ位置を揃えて表示する。<br/>
/// 標準の <see cref="WrapPanel"/> は行内で上端揃えのため、高さが異なるアイテムでは<br/>
/// 下端のラベル位置が行内で揃わない。<br/>
/// <br/>
/// 本パネルの仕様:<br/>
/// - 各行の高さは固定値とせず、その行の最大アイテム高さに応じて決定する。<br/>
/// - アイテムは各行内で下端揃えで配置する（ファイル名の 1 文字目の高さ位置が行内で揃う）。<br/>
/// - 行の折り返しは利用可能幅に対して行う（1 アイテムが幅を超える場合は単独行に置く）。<br/>
/// <br/>
/// 実装の詳細:<br/>
/// - 子要素は Measure 時に幅のみ制約し、高さはコンテンツ任せ（各行の高さの入力）。<br/>
/// - Arrange 時は行単位に子要素と行高さ（最大アイテム高さ）を先に確定し、<br/>
///   「行の上端 + (行高さ − アイテム高さ)」の位置へ配置して下端揃えを実現する。<br/>
/// - 利用可能幅が無限の場合は折り返さず 1 行に並べる。
/// </remarks>
public sealed class BottomAlignWrapPanel : Panel
{
    /// <summary>子要素を幅で折り返しながら各行の高さを計算する。</summary>
    /// <param name="availableSize">利用可能サイズ。</param>
    /// <returns>必要なサイズ（幅 = 行の最大幅、高さ = 行高さの合計）。</returns>
    /// <remarks>
    /// 子要素は幅のみ制約（高さは無限）で計測する。<br/>
    /// 各行の高さはその行の最大アイテム高さとし、合計を戻り値の高さとする。<br/>
    /// 利用可能幅が無限の場合は折り返さない。
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var wrap = !double.IsInfinity(availableSize.Width) && availableSize.Width > 0;

        // 子要素の計測（幅のみ制約する。高さはコンテンツ任せ）→ 計測結果を純粋計算へ委譲
        var sizes = new List<Size>(Children.Count);
        foreach (var child in Children)
        {
            var constraint = wrap
                ? new Size(availableSize.Width, double.PositiveInfinity)
                : new Size(double.PositiveInfinity, double.PositiveInfinity);
            child.Measure(constraint);
            sizes.Add(child.DesiredSize);
        }

        // 折り返し・行高さ計算は UI 非依存の純粋計算へ委譲（テスト並列性向上）
        return BottomAlignWrapLayout.Measure(sizes, availableSize.Width, wrap);
    }

    /// <summary>子要素を幅で折り返し、各行内で下端揃えして配置する。</summary>
    /// <param name="finalSize">確定した配置サイズ。</param>
    /// <returns>配置後のサイズ（finalSize）。</returns>
    /// <remarks>
    /// 行単位に子要素と行高さ（最大アイテム高さ）を先に確定してから配置する（行高さは<br/>
    /// 行の全アイテムが揃わないと決定できないため）。<br/>
    /// 各アイテムは「行の上端 + (行高さ − アイテム高さ)」へ配置され、下端が揃う。
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 各行の配置計算は UI 非依存の純粋計算へ委譲（テスト並列性向上）
        var sizes = new List<Size>(Children.Count);
        foreach (var child in Children)
        {
            sizes.Add(child.DesiredSize);
        }

        var rects = BottomAlignWrapLayout.Arrange(sizes, finalSize.Width);

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(rects[i]);
        }

        return finalSize;
    }
}

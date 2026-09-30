using Avalonia;
using Avalonia.Controls;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Layout;

namespace EsUtil.SaveEvidence.Controls;

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
/// <b>計算の所有</b>: 行高さと下端揃えの計算は UI 非依存の <see cref="BottomAlignWrapLayout"/>（<c>app.Core</c>）が単一所有し、<br/>
/// 本パネルは寸法の変換と委譲のみを担う（MewUI 版の同名パネルと計算を共有する）。
/// </remarks>
public sealed class BottomAlignWrapPanel : Panel
{
    /// <summary>子要素を幅で折り返しながら各行の高さを計算する。</summary>
    /// <param name="availableSize">利用可能サイズ。</param>
    /// <returns>必要なサイズ（幅 = 行の最大幅、高さ = 行高さの合計）。</returns>
    /// <remarks>
    /// 子要素は幅のみ制約（高さは無限）で計測する。<br/>
    /// 利用可能幅が無限の場合は折り返さない。
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var wrap = !double.IsInfinity(availableSize.Width) && availableSize.Width > 0;

        // 子要素の計測（幅のみ制約する。 高さはコンテンツ任せ）→ 計測結果を純粋計算へ委譲
        var sizes = new List<EsSize>(Children.Count);
        foreach (var child in Children)
        {
            var constraint = wrap
                ? new Size(availableSize.Width, double.PositiveInfinity)
                : new Size(double.PositiveInfinity, double.PositiveInfinity);
            child.Measure(constraint);
            sizes.Add(new EsSize(child.DesiredSize.Width, child.DesiredSize.Height));
        }

        // 折り返し・行高さ計算は UI 非依存の純粋計算へ委譲（テスト並列性向上）
        var result = BottomAlignWrapLayout.Measure(sizes, availableSize.Width, wrap);
        return new Size(result.Width, result.Height);
    }

    /// <summary>子要素を幅で折り返し、各行内で下端揃えして配置する。</summary>
    /// <param name="finalSize">確定した配置サイズ。</param>
    /// <returns>配置後のサイズ（finalSize）。</returns>
    /// <remarks>行単位に子要素と行高さ（最大アイテム高さ）を先に確定してから配置する。</remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 各行の配置計算は UI 非依存の純粋計算へ委譲（テスト並列性向上）
        var sizes = new List<EsSize>(Children.Count);
        foreach (var child in Children)
        {
            sizes.Add(new EsSize(child.DesiredSize.Width, child.DesiredSize.Height));
        }

        var rects = BottomAlignWrapLayout.Arrange(sizes, finalSize.Width);

        for (int i = 0; i < Children.Count; i++)
        {
            var rect = rects[i];
            Children[i].Arrange(new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        return finalSize;
    }
}

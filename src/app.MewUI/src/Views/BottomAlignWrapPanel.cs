using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;
using Estable.SaveEvidence.Layout;

namespace SaveEvidence.MewUI.Views;

/// <summary>横方向に折り返し、各行内で子要素を下端揃えするパネル（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>BottomAlignWrapPanel</c> に相当する。<br/>
/// <br/>
/// <b>用途</b>: サムネイル一覧（フォルダビュー / Undo 一覧）のように行内のアイテム高さが異なる場合に、<br/>
/// 各アイテムの下端領域（ファイル名・ラベル）の高さ位置を揃えて表示する。<br/>
/// MewUI の <c>WrapPanel</c> は行内で上端揃えのため、高さが異なるアイテムでは下端のラベル位置が揃わない。<br/>
/// <br/>
/// <b>計算の委譲</b>: 行高さと下端揃えの計算は UI 非依存の <see cref="BottomAlignWrapLayout"/>（<c>app.Core</c>）が単一所有し、<br/>
/// 本パネルは寸法の変換と委譲のみを担う（Avalonia 版の同名パネルと計算を共有する）。<br/>
/// <br/>
/// <b>適用パターン</b>: カスタム Panel（子の配置を自前で決める）。<br/>
/// <b>再測定</b>: <see cref="Spacing"/> は生成時に確定させ、変更時は呼び出し側が再配置を要求する。
/// </remarks>
internal sealed class BottomAlignWrapPanel : Panel
{
    /// <summary>パネルを初期化する。</summary>
    /// <param name="spacing">アイテム間の間隔（DIP）。0 以上の値。</param>
    public BottomAlignWrapPanel(double spacing = 0) => Spacing = spacing;

    /// <summary>アイテム間の間隔（DIP）を取得する。</summary>
    /// <value>0 以上の値。</value>
    public double Spacing { get; }

    /// <summary>子要素を幅で折り返しながら各行の高さを計算する。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>必要なサイズ（幅 = 行の最大幅、高さ = 行高さの合計）。</returns>
    /// <remarks>子要素は幅のみ制約（高さはコンテンツ任せ）で計測する。利用可能幅が無限の場合は折り返さない。</remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        var wrap = !double.IsInfinity(availableSize.Width) && availableSize.Width > 0;

        var sizes = new List<EsSize>(Count);
        foreach (var child in Children)
        {
            var constraint = wrap
                ? new Size(availableSize.Width, double.PositiveInfinity)
                : new Size(double.PositiveInfinity, double.PositiveInfinity);

            child.Measure(constraint);
            sizes.Add(new EsSize(child.DesiredSize.Width, child.DesiredSize.Height));
        }

        var result = BottomAlignWrapLayout.Measure(sizes, availableSize.Width, wrap, Spacing);
        return new Size(result.Width, result.Height);
    }

    /// <summary>子要素を幅で折り返し、各行内で下端揃えして配置する。</summary>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// 行単位に子要素と行高さ（最大アイテム高さ）を先に確定してから配置する。<br/>
    /// 位置はコアの計算が親領域のローカル座標で返すため、割り当て矩形の左上を加算する。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        var sizes = new List<EsSize>(Count);
        foreach (var child in Children)
        {
            sizes.Add(new EsSize(child.DesiredSize.Width, child.DesiredSize.Height));
        }

        var rects = BottomAlignWrapLayout.Arrange(sizes, bounds.Width, Spacing);

        for (var i = 0; i < Count; i++)
        {
            var rect = rects[i];
            Children[i].Arrange(new Rect(
                bounds.X + rect.X, bounds.Y + rect.Y, rect.Width, rect.Height));
        }
    }
}

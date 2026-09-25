using EsUtil.Feature.MarqueeCore;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using MarqueeFacade = EsUtil.Feature.MarqueeCore.Marquee;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>コアの描画内容から、ラベル要素の配置指示を解決する（実寸 → 配置矩形の写像）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「文字を UI 要素として配置する」UI のための変換である。コア（<see cref="MarqueeFacade.ResolveLabels"/>）は<br/>
/// 文言・外観・層の順序を決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI から受け取って要素の矩形へ変換する。<br/>
/// 体裁は <see cref="EsLabelLayout"/> と <see cref="EsLabelElement"/> が単一所有する。<br/>
/// <br/>
/// <b>順序はコアが所有する</b>: 本クラスは <see cref="MarqueeDrawContent.Layers"/> の順序に従って並べるだけで、<br/>
/// 順序を判断しない（ラベルは必ず最後に来るため、要素も常に最上位に置かれる）。<br/>
/// UI フレームワークに依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public static class MarqueeLabelPlan
{
    /// <summary>ラベル要素の配置指示を解決する（描画順に並ぶ）。</summary>
    /// <param name="content">コアが解決した描画内容（文言・寸法・層の順序を持つ）。</param>
    /// <param name="paint">描画に使う色（描画にだけ必要な設定。本層が単一所有する）。null 不可。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素の一覧（サイズラベル → カーソル座標ラベルの順）。どちらも無い場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>寸法（フォントサイズ・余白）と文言はコアが、色は描画の設定が単一所有する（本クラスは実寸の反映のみ）。</remarks>
    public static IReadOnlyList<EsLabelElement> Resolve(
        in MarqueeDrawContent content, MarqueePaint paint, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(paint);
        ArgumentNullException.ThrowIfNull(measureText);

        if (content.SizeLabel is null && content.CoordLabel is null)
        {
            // 描画のたびに呼ばれるため、要素が無い場合は一覧を生成しない
            return [];
        }

        var elements = new List<EsLabelElement>(2);

        if (content.SizeLabel is { } sizeLabel)
        {
            elements.Add(sizeLabel.ToElement(
                measureText(sizeLabel.Text, sizeLabel.FontSize),
                paint.SizeLabelColor, paint.SizeLabelBackgroundColor));
        }

        if (content.CoordLabel is { } coordLabel)
        {
            elements.Add(coordLabel.ToElement(
                measureText(coordLabel.Text, coordLabel.FontSize),
                paint.CoordLabelColor, paint.CoordLabelBackgroundColor));
        }

        return elements;
    }
}

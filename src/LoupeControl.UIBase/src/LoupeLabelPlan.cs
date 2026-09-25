using EsUtil.Feature.LoupeCore;
using EsUtil.Platform.Core.Geometry;
using LoupeFacade = EsUtil.Feature.LoupeCore.Loupe;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの中心座標ラベルの配置指示を解決する（実寸 → 配置矩形の写像）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「文字を UI 要素として配置する」UI のための変換である。コア（<see cref="LoupeFacade.ResolveLabels"/>）は<br/>
/// 文言・外観・表示可否を決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI から受け取って要素の矩形へ変換する。<br/>
/// 体裁は <see cref="EsLabelLayout"/> と <see cref="EsLabelElement"/> が、配置の計算はコアの <c>LoupeCoordLabel.ComputeFrame</c> が単一所有する。<br/>
/// <br/>
/// <b>順序はコアが所有する</b>: ラベルは <see cref="LoupeDrawContent.Layers"/> の最後の層であり、常に最上位に置かれる<br/>
/// （UI の要素は親より後に描かれるため、構造で成立する）。<br/>
/// UI フレームワークに依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public static class LoupeLabelPlan
{
    /// <summary>中心座標ラベルの配置指示を解決する。</summary>
    /// <param name="coordLabel">コアが解決したラベル契約。描かない場合は null。</param>
    /// <param name="paint">描画に使う色（描画にだけ必要な設定。本層が単一所有する）。null 不可。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素（1 要素）。描かない場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>寸法（フォントサイズ・角丸・余白）と文言はコアが、色は描画の設定が単一所有する（本クラスは実寸の反映のみ）。</remarks>
    public static IReadOnlyList<EsLabelElement> Resolve(
        LoupeCoordLabel? coordLabel, LoupePaint paint, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(paint);
        ArgumentNullException.ThrowIfNull(measureText);

        return coordLabel is { } label
            ? [label.ToElement(
                measureText(label.Text, label.FontSize), paint.CoordLabelColor, paint.CoordLabelBackgroundColor)]
            : [];
    }
}

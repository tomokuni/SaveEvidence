using EsUtil.Feature.LoupeCore;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Drawing;


namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの中心座標ラベルの配置指示を解決する（実寸 → 配置矩形の写像）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「文字を UI 要素として配置する」UI のための変換である。コア（<see cref="Loupe.ResolveContent"/>）は<br/>
/// 文言・基準点・アラインを決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI 層から受け取って要素の矩形へ変換する。<br/>
/// 体裁（内側余白）は <see cref="EsLabelLayout"/> が、要素の配置指示は <see cref="EsLabelElement"/> が単一所有する。<br/>
/// フォントサイズと角丸は描画の設定（<see cref="LoupePaint"/>）が単一所有する（コアは値を持たない）。<br/>
/// <br/>
/// <b>中心座標ラベルは常に最上位</b>である（UI の要素は親より後に描かれるため構造で成立する）。<br/>
/// UI フレームワークに依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public static class LoupeLabelPlan
{
    /// <summary>中心座標ラベル背景の上下の内側余白（DIP）。</summary>
    /// <remarks>左右（<see cref="EsLabelLayout.Padding"/>）より狭くする（ルーペ上端の帯へ収めるため）。</remarks>
    private const double PaddingY = 1.0;

    /// <summary>中心座標ラベルの配置指示を解決する。</summary>
    /// <param name="coordLabel">コアが解決したテキスト配置。表示しない場合は null。</param>
    /// <param name="paint">描画に使う設定（色・フォントサイズ・角丸。本層が単一所有する）。null 不可。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素（1 要素）。表示しない場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>文言・位置はコアが、色・フォントサイズ・角丸は描画の設定が単一所有する（本クラスは実寸の反映のみ）。</remarks>
    public static IReadOnlyList<EsLabelElement> Resolve(
        LoupeTextPlacement? coordLabel, LoupePaint paint, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(paint);
        ArgumentNullException.ThrowIfNull(measureText);

        return coordLabel is { } label ? [ResolveElement(label, paint, measureText)] : [];
    }

    /// <summary>テキスト配置と体裁から、要素の配置指示を組み立てる。</summary>
    /// <param name="placement">コアが決めた文言と基準点（アラインを含む）。</param>
    /// <param name="paint">描画に使う設定（色・フォントサイズ・角丸）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。</param>
    /// <returns>要素の配置指示。</returns>
    /// <remarks>
    /// 背景矩形の算出は <see cref="EsLabelLayout.ComputeAlignedBox"/> が単一所有する<br/>
    /// （基準点がラベルのどこを指すかをアラインで受け取り、領域が確定している軸だけクランプする）。<br/>
    /// 上下の余白はルーペ固有の値であり本クラスが単一所有する（左右は共通の <see cref="EsLabelLayout.Padding"/>）。
    /// </remarks>
    private static EsLabelElement ResolveElement(
        in LoupeTextPlacement placement, LoupePaint paint, Func<string, double, EsSize> measureText)
    {
        var fontSize = paint.CoordLabelFontSize;
        var textSize = measureText(placement.Text, fontSize);
        var box = EsLabelLayout.ComputeAlignedBox(
            placement.Point, placement.ClampArea, textSize,
            placement.Horizontal, placement.Vertical, EsLabelLayout.Padding, PaddingY);
        var frame = new EsLabelFrame(
            box, EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, PaddingY));

        return EsLabelElement.Create(
            frame, placement.Text, fontSize,
            paint.CoordLabelColor, paint.CoordLabelBackgroundColor, paint.CoordLabelCornerRadius);
    }
}

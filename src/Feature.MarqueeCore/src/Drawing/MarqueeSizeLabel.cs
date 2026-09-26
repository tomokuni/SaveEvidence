using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.Feature.MarqueeCore.Drawing;

/// <summary>サイズラベル（穴あけ矩形の位置と寸法）の描画契約。</summary>
/// <param name="Text">表示する文言（コアが矩形から生成する。ホストがプロバイダで与えた場合はその文字列）。</param>
/// <param name="Anchor">ラベルを積む基準点（穴あけ矩形の左上・DIP）。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <remarks>
/// <b>文言はコアが単一所有する</b>: 生成は描画内容の解決（<see cref="Marquee.ResolveFrame"/>）が行い、
/// 書式は本アセンブリの内部に閉じる（ホスト・描画層は文言を作らない）。
/// ホスト固有の文言（元画像座標の接頭辞等）が必要な場合は <see cref="Marquee.SizeLabelOverrideProvider"/> で与える。<br/>
/// <b>座標ラベル（<see cref="MarqueeCoordLabel"/>）と同じ形の契約</b>である（値 + 配置の計算だけを持ち、整形の API を持たない）。<br/>
/// <b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c>）が単一所有する。<br/>
/// <br/>
/// <b>文字の描画は UI 層が担う</b>: テキストの実寸は描画 API でしか測れないため、<br/>
/// UI 層が実寸を <see cref="ComputeFrame"/> へ渡し、返された配置へテキスト要素を置く<br/>
/// （要素への写像は <see cref="EsLabelExtensions.ToElement"/> が単一所有する）。<br/>
/// <b>単位は DIP</b>である（描画領域・選択矩形の座標系と同じ）。
/// </remarks>
public readonly record struct MarqueeSizeLabel(
    string Text,
    EsPoint Anchor,
    double FontSize) : IEsLabel
{
    /// <summary>テキストの実寸からラベルの配置（背景矩形・文字の描画原点）を求める。</summary>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>ラベルの配置（基準点の左上に置き、内側余白を四方へ空ける）。</returns>
    /// <remarks>
    /// 背景の大きさ・文字の原点は <see cref="EsLabelLayout"/> が単一所有する。<br/>
    /// 内側余白は付加情報ラベルと共通の <see cref="EsLabelLayout.Padding"/> を使う（同じ体裁で縦に積める）。
    /// </remarks>
    public EsLabelFrame ComputeFrame(EsSize textSize)
    {
        var box = EsLabelLayout.ComputeBoxAtAnchor(
            Anchor, textSize, EsLabelLayout.Padding, EsLabelLayout.Padding);
        return new EsLabelFrame(
            box, EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, EsLabelLayout.Padding));
    }
}

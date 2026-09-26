using EsUtil.Feature.MarqueeCore;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Drawing;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>コアのテキスト配置から、ラベル要素の配置指示を解決する（実寸 → 配置矩形の写像）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「文字を UI 要素として配置する」UI のための変換である。コア（<see cref="Marquee.ResolveContent"/>）は<br/>
/// 文言・基準点・アライン・領域を決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI 層から受け取って要素の矩形へ変換する。<br/>
/// 体裁（内側余白・背景・行送り）は <see cref="EsLabelLayout"/> が、要素の配置指示は <see cref="EsLabelElement"/> が単一所有する。<br/>
/// フォントサイズは描画の設定（<see cref="MarqueePaint"/>）が単一所有する（コアは値を持たない）。<br/>
/// <br/>
/// <b>順序は本クラスが所有する</b>: サイズラベル → カーソル座標ラベルの順に並べる<br/>
/// （文字を要素として置く場合、要素は親より後に描かれるため常に最上位になる）。<br/>
/// UI フレームワークに依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public static class MarqueeLabelPlan
{
    /// <summary>ラベル要素の配置指示を解決する（描画順に並ぶ）。</summary>
    /// <param name="content">コアが解決した描画内容（文言・基準点・層の順序を持つ）。</param>
    /// <param name="paint">描画に使う設定（色・フォントサイズ。本層が単一所有する）。null 不可。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素の一覧（サイズラベル → カーソル座標ラベルの順）。どちらも無い場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>文言・位置はコアが、色・フォントサイズは描画の設定が単一所有する（本クラスは実寸の反映のみ）。</remarks>
    public static IReadOnlyList<EsLabelElement> Resolve(
        in MarqueeDrawContent content, MarqueePaint paint, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(paint);
        ArgumentNullException.ThrowIfNull(measureText);

        if (content.SizeLabelText is null && content.CoordLabelText is null)
        {
            // 描画のたびに呼ばれるため、要素が無い場合は一覧を生成しない
            return [];
        }

        var elements = new List<EsLabelElement>(2);

        if (content.SizeLabelText is { } sizeLabel)
        {
            elements.Add(ResolveElement(
                sizeLabel, paint.SizeLabelFontSize, paint.SizeLabelColor, paint.SizeLabelBackgroundColor, measureText));
        }

        if (content.CoordLabelText is { } coordLabel)
        {
            elements.Add(ResolveElement(
                coordLabel, paint.CoordLabelFontSize, paint.CoordLabelColor, paint.CoordLabelBackgroundColor, measureText));
        }

        return elements;
    }

    /// <summary>テキスト配置と体裁から、要素の配置指示を組み立てる。</summary>
    /// <param name="placement">コアが決めた文言と基準点（アライン・クランプ領域を含む）。</param>
    /// <param name="fontSize">フォントサイズ（DIP。描画の設定が所有する）。</param>
    /// <param name="textColor">文字色（描画の設定が所有する）。</param>
    /// <param name="backgroundColor">背景色（描画の設定が所有する）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。</param>
    /// <returns>要素の配置指示（背景の角丸は四角形）。</returns>
    /// <remarks>
    /// 背景矩形の算出は <see cref="EsLabelLayout.ComputeAlignedBox"/> が単一所有する<br/>
    /// （基準点がラベルのどこを指すかをアラインで受け取り、領域が確定している軸だけクランプする）。<br/>
    /// 内側余白はサイズラベルと付加情報ラベルで共通の <see cref="EsLabelLayout.Padding"/> を使う。
    /// </remarks>
    private static EsLabelElement ResolveElement(
        in MarqueeTextPlacement placement,
        double fontSize,
        DrawingColor textColor,
        DrawingColor backgroundColor,
        Func<string, double, EsSize> measureText)
    {
        var textSize = measureText(placement.Text, fontSize);
        var box = EsLabelLayout.ComputeAlignedBox(
            placement.Point, placement.ClampArea, textSize,
            placement.Horizontal, placement.Vertical, EsLabelLayout.Padding, EsLabelLayout.Padding);
        var frame = new EsLabelFrame(
            box, EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, EsLabelLayout.Padding));

        return EsLabelElement.Create(frame, placement.Text, fontSize, textColor, backgroundColor);
    }
}

using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Decoration;


namespace EsUtil.Feature.MarqueeCore.Drawing;

/// <summary>選択矩形コントロールの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="Decoration">選択矩形の外側に描く装飾（暗転マスク・穴あけ枠・枠線）。描くものが無い場合は各項目が空。</param>
/// <param name="InnerDecoration">穴あけ矩形の内側に描く装飾（塗り・内枠）。描くものが無い場合は各項目が空。</param>
/// /// <param name="HoleRect">穴あけ矩形（選択またはハイライトの領域・DIP）。選択もハイライトも無い場合は null。</param>
/// <param name="Handles">ドラッグハンドル。null の場合は描かない（非表示の設定または選択なし）。</param>
/// <param name="Marker">始点マーカー（十字線）。null の場合は描かない（nudge 中のみ表示する）。</param>
/// <param name="SizeLabelText">サイズラベルの文言と基準点。null の場合は表示しない（非表示の設定または穴あけ矩形なし）。</param>
/// <param name="CoordLabelText">カーソル座標ラベルの文言と基準点。null の場合は表示しない（非表示の設定）。</param>
/// <param name="Layers">描く図形の層とその順序（<b>描画順はコアが単一所有する</b>）。先頭から順に処理する。</param>
/// <remarks>
/// UI 実装は <see cref="Layers"/> の順に各層を処理するだけにする（描くかどうかの判断・外観値の解決はすべて済んでいる）。<br/>
/// <b>層にある ⇔ 描く値がある</b>: 描く値の無い層は含めない（<see cref="HasDecoration"/> / <see cref="HasText"/> が層の有無と一致する）。<br/>
/// <b>文字の層は含まない</b>: テキストは <see cref="SizeLabelText"/> / <see cref="CoordLabelText"/> として値を渡し、<br/>
/// UI 層が要素として置くか画像として描くかを決める（要素は親より後に描かれるため常に最上位になる）。<br/>
/// <br/>
/// <see cref="HoleRect"/> はホストが付加情報（操作ラベル等）を対象領域へ重ねるための基準である（DIP）。<br/>
/// テキストの配置は実寸に依存するため、基準点は各テキストの <c>Point</c> が持つ（実寸の反映は UI 層が行う）。
/// </remarks>
public readonly record struct MarqueeDrawContent(
    MarqueeOuterDecoFrame Decoration,
    MarqueeInnerDecoFrame InnerDecoration,
    EsRect? HoleRect,
    MarqueeHandles? Handles,
    MarqueeMarker? Marker,
    MarqueeTextPlacement? SizeLabelText,
    MarqueeTextPlacement? CoordLabelText,
    IReadOnlyList<MarqueeDrawLayer> Layers)
{
    /// <summary>テキスト（ラベル）を描く必要があるかどうかを取得する。</summary>
    /// <value>サイズラベルまたはカーソル座標ラベルを持つ場合は true。</value>
    public bool HasText => SizeLabelText is not null || CoordLabelText is not null;

    /// <summary>装飾（外側・内側）を描く必要があるかどうかを取得する。</summary>
    /// <value>外側または内側の装飾に描く値がある場合は true。</value>
    /// <remarks>層（<see cref="MarqueeDrawLayer.Decoration"/>）に含めるかどうかを本値が表す（層にある ⇔ 描く値がある）。</remarks>
    public bool HasDecoration => Decoration.HasContent || InnerDecoration.HasContent;
}

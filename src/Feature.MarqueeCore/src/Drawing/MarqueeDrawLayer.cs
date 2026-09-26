namespace EsUtil.Feature.MarqueeCore.Drawing;

/// <summary>選択矩形の UI 層が描く図形の層（描画順の契約）。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: UI 層は <c>Marquee.ResolveContent</c> が返す
/// <see cref="MarqueeDrawContent.Layers"/> を先頭から順に処理するだけで、順序を判断しない。<br/>
/// <b>文字（ラベル）の層は含まない</b>: テキストは <see cref="MarqueeDrawContent.SizeLabelText"/> /
/// <see cref="MarqueeDrawContent.CoordLabelText"/> として渡され、UI 層が要素として置くか画像として描くかを決める<br/>
/// （UI の要素は親より後に描かれるため、順序を指定しなくても常に最上位になる）。<br/>
/// <br/>
/// <b>UI 層の実装</b>: 本層の順序に従って自 UI の描画 API へ写す。
/// </remarks>
public enum MarqueeDrawLayer
{
    /// <summary>選択矩形の外側・内側に描く装飾（暗転マスク・穴あけ枠・枠線・内側の塗り）。</summary>
    Decoration,

    /// <summary>ドラッグハンドル（選択がある場合のみ）。</summary>
    Handles,

    /// <summary>始点マーカー（nudge 中のみ）。</summary>
    Marker,
}

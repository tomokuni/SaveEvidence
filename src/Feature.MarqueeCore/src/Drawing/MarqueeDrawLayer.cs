namespace EsUtil.Feature.MarqueeCore.Drawing;

/// <summary>選択矩形の UI 層が処理する層（描画順の契約）。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: UI 層は <c>Marquee.ResolveContent</c> が返す
/// <see cref="MarqueeDrawContent.Layers"/> を先頭から順に処理するだけで、順序を判断しない。<br/>
/// ラベル（<see cref="SizeLabel"/> / <see cref="CoordLabel"/>）は<b>必ず最後</b>に置く（常に最上位に表示する）。<br/>
/// <br/>
/// <b>UI 層の実装</b>: 図形の層は自 UI の描画 API へ写し、文字の層は UI 要素（テキスト要素）を配置する。<br/>
/// どちらの方式で描くかは UI の裁量であるが、<b>層の順序と有無は本契約に従う</b>。
/// </remarks>
public enum MarqueeDrawLayer
{
    /// <summary>選択矩形の外側・内側に描く装飾（暗転マスク・穴あけ枠・枠線・内側の塗り）。</summary>
    Decoration,

    /// <summary>ドラッグハンドル（選択がある場合のみ）。</summary>
    Handles,

    /// <summary>始点マーカー（nudge 中のみ）。</summary>
    Marker,

    /// <summary>サイズラベル（穴あけ矩形の左上。常に最上位）。</summary>
    SizeLabel,

    /// <summary>カーソル座標ラベル（常に最上位。サイズラベルより手前）。</summary>
    CoordLabel,
}

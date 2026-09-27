using EsUtil.Platform.Core.Geometry;


namespace EsUtil.Feature.Marquee.Core.Drawing;

/// <summary>コアが UI 層へ渡すテキスト（文言と基準点）を表す値。</summary>
/// <param name="Text">表示する文言（コアが生成する。ホストがプロバイダで与えた場合はその文字列）。</param>
/// <param name="Point">基準点（DIP。<paramref name="Horizontal"/> / <paramref name="Vertical"/> に従って解釈する）。</param>
/// <param name="Horizontal">基準点を合わせるラベルの水平位置（左端 / 中央 / 右端）。</param>
/// <param name="Vertical">基準点を合わせるラベルの垂直位置（上端 / 中央 / 下端）。</param>
/// <param name="ClampArea">描画領域のサイズ（DIP。0 以下の軸はクランプしない。既定（0）はクランプなし）。</param>
/// <remarks>
/// <b>コアはラベルを特殊扱いしない</b>: コアが決めるのは「文言・位置・アライン・領域」までであり、<br/>
/// ラベルを<b>テキスト要素（<c>TextBlock</c> 等）として置くか、画像として描くかは UI 層が決める</b>。<br/>
/// そのためフォントサイズ・色・背景・内側余白は本値に含まない（描画の設定が単一所有する）。<br/>
/// 背景矩形と文字の原点はテキストの実寸に依存するため、UI 層が実寸を測って解決する<br/>
/// （体裁の計算は描画層（<c>Feature.Marquee.Presenter</c> の <c>MarqueePaint</c> / <c>MarqueeLabelPlan</c>）が単一所有する）。<br/>
/// <br/>
/// <b>サイズラベルとカーソル座標ラベルは同じ型を使う</b>: 差は「基準点・アライン・領域」だけであり、<br/>
/// 位置の算出（穴あけ矩形の左上 / カーソル + オフセット）はコアが単一所有する。<br/>
/// <b>単位は DIP</b>である（描画領域・選択矩形の座標系と同じ）。
/// </remarks>
public readonly record struct MarqueeTextPlacement(
    string Text,
    EsPoint Point,
    EsHorizontalAlign Horizontal,
    EsVerticalAlign Vertical,
    EsSize ClampArea);

using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>ラベル 1 件の配置（背景矩形と文字の描画原点）を表す値オブジェクト。</summary>
/// <param name="Background">背景の矩形（DIP。呼び出し側の座標系に従う）。</param>
/// <param name="TextOrigin">文字の描画原点（左上・DIP）。</param>
/// <remarks>
/// <b>位置付け</b>: テキストの実寸は描画 API（フォント）に依存するためコアでは測れない。<br/>
/// そこで「実寸を受け取って配置を返す」契約を本型が単一所有し、実寸の測定と返された配置の反映だけを UI 層が担う。<br/>
/// 配置の算出は <see cref="EsLabelLayout"/> が単一所有する（本型は値のみを持つ）。<br/>
/// <br/>
/// <b>単位</b>: DIP（描画領域と同じ座標系）。物理ピクセルへの換算は描画時に行う。
/// </remarks>
public readonly record struct EsLabelFrame(EsRect Background, EsPoint TextOrigin);

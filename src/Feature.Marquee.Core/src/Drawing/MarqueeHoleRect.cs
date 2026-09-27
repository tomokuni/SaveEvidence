using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Core.Drawing;

/// <summary>穴あけ矩形と、その矩形を扱うときの閉区間の指定。</summary>
/// <param name="Rect">穴あけ矩形（座標系は呼び出し側の契約に従う）。</param>
/// <param name="ClosedInterval">閉区間（終点 +1）として扱うか。選択矩形は true、ハイライト矩形は false。</param>
/// <remarks>
/// 「どちらの矩形を穴として優先するか」と「その矩形の終点を +1 するか」は同じ判断であるため 1 つの値として返す<br/>
/// （呼び出し側が優先順位を再判定すると、規則が 2 箇所へ分かれる）。
/// </remarks>
internal readonly record struct MarqueeHoleRect(EsRect Rect, bool ClosedInterval);

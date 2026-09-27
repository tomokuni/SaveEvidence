using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Core.Drawing;

/// <summary>始点マーカー（十字線）の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="Center">十字線の中心（DIP）。</param>
/// <param name="Thickness">十字線の太さ（DIP）。</param>
/// <param name="Size">十字線の半幅（DIP）。</param>
/// <remarks><b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c>）が単一所有する。</remarks>
public readonly record struct MarqueeMarker(
    EsPoint Center,
    double Thickness,
    double Size);

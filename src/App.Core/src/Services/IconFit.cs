using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>アイコンの図形（SVG のパス）を指定寸法の枠いっぱいへ広げるための変換を求める。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: <b>図形の外接矩形を枠いっぱいへ広げる</b>という規則は UI に依存しない
/// 1 つの式であり、UI 版ごとに書き分けると<b>アイコンの大きさが見た目で食い違う</b>
/// （実測 2026-11-13: 同じ式が 3 か所に重複していた）。<br/>
/// <b>規則</b>: 外接矩形の縦横比を保って枠へ内接させ（拡大率は縦横の小さい方）、余白を均等に割り付ける。<br/>
/// <b>なぜ viewBox（24x24）基準ではないか</b>: SVG の図形は viewBox の中に余白を持つため、viewBox 基準で描くと
/// アイコンだけが小さく見える（実測）。 Avalonia 版の <c>PathIcon</c> は外接矩形を枠いっぱいへ引き伸ばすため、
/// 他の UI 版も同じ規則に合わせる。
/// </remarks>
internal static class IconFit
{
    /// <summary>外接矩形を枠いっぱいへ広げる変換（拡大率と平行移動）を求める。</summary>
    /// <param name="bounds">図形の外接矩形。</param>
    /// <param name="size">枠の一辺（ピクセル）。</param>
    /// <returns>拡大率と平行移動。 退化した矩形（幅または高さが 0 以下）の場合は null。</returns>
    /// <remarks>
    /// 変換の適用順は「拡大してから平行移動」である（<c>x' = x * Scale + OffsetX</c>）。<br/>
    /// 退化した矩形で null を返すのは、0 除算で無限大の拡大率を作らないためである
    /// （呼び出し側は空の画像で代替する）。
    /// </remarks>
    public static IconFitTransform? Resolve(EsRect bounds, int size)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        var scale = Math.Min(size / bounds.Width, size / bounds.Height);
        var offsetX = ((size - (bounds.Width * scale)) / 2) - (bounds.X * scale);
        var offsetY = ((size - (bounds.Height * scale)) / 2) - (bounds.Y * scale);
        return new IconFitTransform(scale, offsetX, offsetY);
    }
}

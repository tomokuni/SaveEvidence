using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;

/// <summary>矩形の 1 辺を破線の区間（画像ピクセル座標）へ分割する。</summary>
/// <remarks>
/// 「どちらの軸の辺か」「ダッシュの刻み」「区間端のクランプ」「画素座標の丸め」を単一所有する。<br/>
/// 破線を描く 2 つの経路（外部提供矩形のラスタライズと選択矩形の破線オーバーレイ）が同じ幾何を使う<br/>
/// （経路ごとに刻みや端の扱いがずれると、同じスタイルでも破線の見た目が変わる）。<br/>
/// 画素座標の丸めは <see cref="CropBoundsMath.RoundToPixel"/> が単一所有する（0.5 は切り上げ）。<br/>
/// 呼び出しは 1 矩形あたり 4 辺であり毎フレームではないため、区間の通知にコールバックを使う。
/// </remarks>
internal static class OverlayDashWalk
{
    /// <summary>1 ダッシュ分の区間（画像ピクセル座標）。</summary>
    /// <param name="Horizontal">水平辺の場合は true（垂直辺は false）。</param>
    /// <param name="From">区間の先頭（水平辺は X、垂直辺は Y。 終点を含む）。</param>
    /// <param name="To">区間の末尾（水平辺は X、垂直辺は Y。 終点を含む）。</param>
    /// <param name="FixedCoord">固定座標（水平辺は Y、垂直辺は X。 小数を保持する）。</param>
    /// <param name="Index">ダッシュの通し番号（辺ごとに 0 から。 色の交互切替に使う）。</param>
    internal readonly record struct Span(bool Horizontal, int From, int To, double FixedCoord, int Index);

    /// <summary>1 辺を破線の区間へ分割して通知する。</summary>
    /// <param name="from">始点（画像ピクセル座標）。</param>
    /// <param name="to">終点（画像ピクセル座標）。</param>
    /// <param name="dashed">破線の場合は true。 実線（1 画素刻み）の場合は false。</param>
    /// <param name="dashLength">ダッシュの長さ（画素。 実線では参照しない）。</param>
    /// <param name="gapLength">隙間の長さ（画素。 実線では参照しない）。</param>
    /// <param name="sink">区間を受け取るコールバック。 描画するダッシュだけが通知される。</param>
    /// <remarks>
    /// 負の方向へ向かう辺（終点が始点より小さい）は区間を通知しない<br/>
    /// （矩形の幅・高さは常に 0 以上のため通常は発生しない）。<br/>
    /// 実線は 1 画素刻みで、区間の端を距離でクランプしない（現行の描画と同じ画素を打つ）。
    /// </remarks>
    internal static void ForEach(
        EsPoint from, EsPoint to, bool dashed, int dashLength, int gapLength, Action<Span> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var horizontal = Math.Abs(to.Y - from.Y) < Math.Abs(to.X - from.X);
        var length = horizontal ? to.X - from.X : to.Y - from.Y;
        var start = horizontal ? from.X : from.Y;
        var fixedCoord = horizontal ? from.Y : from.X;
        var sign = length >= 0 ? 1 : -1;
        var distance = Math.Abs(length);
        var step = dashed ? dashLength + gapLength : 1;

        var index = 0;
        for (double pos = 0; pos < distance; pos += step)
        {
            var segmentStart = start + sign * pos;
            var segmentEnd = start + sign * (dashed ? Math.Min(pos + dashLength, distance) : pos + 1);
            var first = CropBoundsMath.RoundToPixel(segmentStart);
            var last = CropBoundsMath.RoundToPixel(segmentEnd);

            // 負の方向へ向かう辺（退化した辺）はダッシュを生成しない（色の通し番号も消費しない）
            if (last < first)
            {
                continue;
            }

            sink(new Span(horizontal, first, last, fixedCoord, index));
            index++;
        }
    }
}

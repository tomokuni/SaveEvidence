using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;

/// <summary>オーバーレイ矩形の 4 辺をルーペ画像（<see cref="EsBitmap"/>）へラスタライズする。</summary>
/// <remarks>
/// ルーペに表示される切り出し済み画像へ、外部提供矩形の枠線を直接打ち込む（ドラッグハンドルを持つ<br/>
/// UI の図形要素ではルーペ画像へ届かないため、ピクセル単位で描画する）。<br/>
/// 破線はダッシュごとに前景色と背景色を交互に塗る（市松風の視認性）。<br/>
/// ダッシュ長は <see cref="OverlayLineDash"/>、太さの下限は <see cref="OverlayRectLayout.EffectiveThickness"/> が単一所有する<br/>
/// （UI のベクター描画と共有し、経路ごとに見た目がずれないようにする）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class OverlayRectRasterizer
{
    /// <summary>矩形の 4 辺（上・下・左・右）を描画する。</summary>
    /// <param name="bitmap">描画対象の画像（<b>書き換えるため <see cref="EsBitmap"/> を受ける</b>）。 書き込み先のサイズ（正方形）は <see cref="EsReadOnlyBitmap.Width"/>。</param>
    /// <param name="rect">描画する矩形（対象座標）とスタイル。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <remarks>
    /// 矩形が描画範囲外の場合は何もせずに戻る（早期リターン）。<br/>
    /// 対象座標 → ルーペピクセル座標の変換は半開区間の終点を含むよう幅・高さへ +1 する。
    /// </remarks>
    internal static void Draw(EsBitmap bitmap, OverlayRect rect, EsPoint captureTopLeft, double scale)
    {
        // 対象座標 → ルーペピクセル座標（半開区間の終点を含むよう +1 する）
        double x = (rect.Bounds.X - captureTopLeft.X) * scale;
        double y = (rect.Bounds.Y - captureTopLeft.Y) * scale;
        double w = (rect.Bounds.Width + 1) * scale;
        double h = (rect.Bounds.Height + 1) * scale;

        var pixelSize = bitmap.Width;
        if (x + w < 0 || y + h < 0 || x > pixelSize || y > pixelSize)
        {
            return;
        }

        // 4 辺（上・下・左・右）を描画する
        DrawEdge(bitmap, new EsPoint(x, y), new EsPoint(x + w, y), rect);
        DrawEdge(bitmap, new EsPoint(x, y + h), new EsPoint(x + w, y + h), rect);
        DrawEdge(bitmap, new EsPoint(x, y), new EsPoint(x, y + h), rect);
        DrawEdge(bitmap, new EsPoint(x + w, y), new EsPoint(x + w, y + h), rect);
    }

    /// <summary>画像上に矩形の 1 辺を描画する（実線または破線）。</summary>
    /// <param name="bitmap">描画対象の画像。</param>
    /// <param name="from">始点（ルーペピクセル座標）。</param>
    /// <param name="to">終点（ルーペピクセル座標）。</param>
    /// <param name="rect">描画スタイル（破線種類・前景色・背景色・幅）。</param>
    /// <remarks>
    /// 辺の軸の判定・ダッシュの刻み・端のクランプ・丸めは <see cref="OverlayDashWalk"/> が単一所有する。<br/>
    /// 破線はダッシュごとに前景色と背景色を交互に切り替える（インデックスの偶奇）。
    /// </remarks>
    private static void DrawEdge(EsBitmap bitmap, EsPoint from, EsPoint to, OverlayRect rect)
    {
        // 線の太さ（1 ピクセル以上。 偶数幅は中心から片側へ寄る）
        // 下限の規則は OverlayRectLayout.EffectiveThickness が単一所有する（両 UI のベクター描画と共有）
        var thickness = CropBoundsMath.RoundToPixel(OverlayRectLayout.EffectiveThickness(rect.Thickness));
        var half = Math.Max(0, thickness / 2);

        var isDashed = OverlayLineDash.IsDashed(rect.Style);
        var (dashLength, gapLength) = OverlayLineDash.Get(rect.Style);

        OverlayDashWalk.ForEach(
            from, to, isDashed, CropBoundsMath.RoundToPixel(dashLength), CropBoundsMath.RoundToPixel(gapLength),
            span =>
            {
                // 破線はダッシュごとに色を交互（前景色 / 背景色・市松風）
                var color = isDashed && span.Index % 2 == 1 ? rect.Background : rect.Foreground;
                var fixedPixel = CropBoundsMath.RoundToPixel(span.FixedCoord);
                for (var pixel = span.From; pixel <= span.To; pixel++)
                {
                    if (span.Horizontal)
                    {
                        StampPixel(bitmap, pixel, fixedPixel, color, half, horizontal: true);
                    }
                    else
                    {
                        StampPixel(bitmap, fixedPixel, pixel, color, half, horizontal: false);
                    }
                }
            });
    }

    /// <summary>線の太さを考慮して画像へ色を打ち込む。</summary>
    /// <param name="bitmap">描画対象の画像。</param>
    /// <param name="x">中心 X 座標。</param>
    /// <param name="y">中心 Y 座標。</param>
    /// <param name="color">描画色（不透明）。</param>
    /// <param name="half">線の太さの半径（半幅）。</param>
    /// <param name="horizontal">水平辺かどうか。</param>
    /// <remarks>
    /// 水平辺は Y 固定で太さ方向が X、垂直辺は X 固定で太さ方向が Y になる。<br/>
    /// 太さ分だけ固定座標の両側へ広げて描画する（範囲外は <see cref="EsBitmap.TrySetPixel(int, int, DrawingColor)"/> が無視する）。
    /// </remarks>
    private static void StampPixel(EsBitmap bitmap, int x, int y, DrawingColor color, int half, bool horizontal)
    {
        var fixedCoord = horizontal ? y : x;
        for (var d = -half; d <= half; d++)
        {
            var px = horizontal ? x : fixedCoord + d;
            var py = horizontal ? fixedCoord + d : y;
            bitmap.TrySetPixel(px, py, color);
        }
    }
}

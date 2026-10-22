using Avalonia;
using Avalonia.Media;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;

/// <summary>オーバーレイ矩形（<see cref="OverlayRect"/>）を Avalonia の描画コンテキストへ描画するレンダラー。</summary>
/// <remarks>
/// ルーペの拡大表示に重ねる矩形（自動切り出しの検出矩形等）を、ルーペの描画面へ直接描く。<br/>
/// <b>破線の幾何と太さはコアが単一所有する</b>（<see cref="OverlayDashWalk"/> の区間列挙と<br/>
/// <see cref="OverlayRectLayout.EffectiveThickness"/>）ため、本クラスは列挙された区間を矩形として塗るだけである<br/>
/// （MewUI 版の枠線描画とダッシュの刻み・太さの下限が一致する）。
/// </remarks>
internal static class OverlayRectContextRenderer
{
    /// <summary>矩形の 4 辺（上・下・左・右）を描画する。</summary>
    /// <param name="context">描画先の描画コンテキスト（描画中のみ有効）。 null 不可。</param>
    /// <param name="rect">描画する矩形（対象座標）とスタイル。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="pixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <remarks>
    /// 矩形が描画範囲外の場合は何もせずに戻る（早期リターン）。<br/>
    /// 対象座標 → ルーペピクセル座標の変換は半開区間の終点を含むよう幅・高さへ +1 する。
    /// </remarks>
    public static void Draw(
        DrawingContext context, OverlayRect rect, EsPoint captureTopLeft, double scale, int pixelSize)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 対象座標 → ルーペピクセル座標（半開区間の終点を含むよう +1 する）
        double x = (rect.Bounds.X - captureTopLeft.X) * scale;
        double y = (rect.Bounds.Y - captureTopLeft.Y) * scale;
        double w = (rect.Bounds.Width + 1) * scale;
        double h = (rect.Bounds.Height + 1) * scale;

        if (x + w < 0 || y + h < 0 || x > pixelSize || y > pixelSize)
        {
            return;
        }

        // 4 辺（上・下・左・右）を描画する
        DrawEdge(context, new EsPoint(x, y), new EsPoint(x + w, y), rect);
        DrawEdge(context, new EsPoint(x, y + h), new EsPoint(x + w, y + h), rect);
        DrawEdge(context, new EsPoint(x, y), new EsPoint(x, y + h), rect);
        DrawEdge(context, new EsPoint(x + w, y), new EsPoint(x + w, y + h), rect);
    }

    /// <summary>矩形の 1 辺を描画する（実線または破線）。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="from">始点（ルーペピクセル座標）。</param>
    /// <param name="to">終点（ルーペピクセル座標）。</param>
    /// <param name="rect">描画スタイル（破線種類・前景色・背景色・幅）。</param>
    /// <remarks>
    /// 辺の軸の判定・ダッシュの刻み・端のクランプ・丸めは <see cref="OverlayDashWalk"/> が単一所有する。<br/>
    /// 破線はダッシュごとに前景色と背景色を交互に切り替える（インデックスの偶奇）。<br/>
    /// 区間は 1 つの矩形として塗る（連続する画素と線の太さをまとめる。 画素単位の打ち込みと同じ範囲になる）。
    /// </remarks>
    private static void DrawEdge(DrawingContext context, EsPoint from, EsPoint to, OverlayRect rect)
    {
        // 線の太さ（1 ピクセル以上）と半径（半幅）はコアが単一所有する
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
                var brush = ResolveBrush(color);
                var band = span.Horizontal
                    ? new Rect(span.From, fixedPixel - half, (span.To - span.From) + 1, (half * 2) + 1)
                    : new Rect(fixedPixel - half, span.From, (half * 2) + 1, (span.To - span.From) + 1);
                context.FillRectangle(brush, band);
            });
    }

    /// <summary>色に対応するブラシを取得する（半透明は不透明度を保持したブラシにする）。</summary>
    /// <param name="color">描画色（透明度を含む）。</param>
    /// <returns>描画に使うブラシ。</returns>
    /// <remarks>
    /// 半透明は共有キャッシュの対象にしない（不透明度ごとのブラシを無限に作らないため、その都度生成する）。<br/>
    /// 不透明は <see cref="SolidColorBrushCache"/> が共有する。
    /// </remarks>
    private static IBrush ResolveBrush(DrawingColor color)
        => color.A == 255
            ? SolidColorBrushCache.GetOrCreate(color)
            : new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
}

using Avalonia;
using Avalonia.Media;
using Estable.AvaloniaUI.Loupe.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの十字線・外枠・選択矩形破線を描画します。</summary>
internal static class LoupeDecorationRenderer
{
    /// <summary>十字線を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="color">十字線の色（不透明度は色のアルファに統合）。null の場合は描画しない。</param>
    public static void DrawCrosshair(DrawingContext context, int pixelSize, int zoomLevel, IBrush? color)
    {
        if (color is null) return;

        // 切り出し領域の一辺（ブロック数）は切り出しサイズの計算を単一所有する LoupeImageExtractor に委譲する
        var blocks = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);
        var start = blocks / 2 * zoomLevel;
        // 不透明度は color（ブラシ）のアルファに統合済み。そのまま描画する。
        context.DrawRectangle(color, null, new Rect(start, 0, zoomLevel, pixelSize));
        context.DrawRectangle(color, null, new Rect(0, start, pixelSize, zoomLevel));
    }

    /// <summary>外枠を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="color">外枠の色（不透明度は色のアルファに統合）。null の場合は描画しない。</param>
    /// <param name="width">外枠の太さ（ピクセル）。1 〜 ルーペサイズに正規化される。</param>
    public static void DrawFrame(DrawingContext context, int pixelSize, IBrush? color, int width)
    {
        if (color is null) return;

        var frame = Math.Clamp(width, 1, pixelSize);
        var inner = Math.Max(0, pixelSize - frame * 2);
        context.DrawRectangle(color, null, new Rect(0, 0, pixelSize, frame));
        context.DrawRectangle(color, null, new Rect(0, pixelSize - frame, pixelSize, frame));
        context.DrawRectangle(color, null, new Rect(0, frame, frame, inner));
        context.DrawRectangle(color, null, new Rect(pixelSize - frame, frame, frame, inner));
    }

    /// <summary>選択矩形の枠線をルーペ画像上に 2 色破線で描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="selectionRect">選択矩形（ドメイン座標系 = 元画像ピクセル座標）。半開区間（幅 = 終点 − 始点）で指定する。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（ドメイン座標系）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="colorA">破線の色A（明色・不透明度はアルファに統合）。null の場合は描画しない。</param>
    /// <param name="colorB">破線の色B（暗色・不透明度はアルファに統合）。null の場合は描画しない。</param>
    /// <remarks>
    /// 選択矩形（ドメイン座標）をルーペ座標に変換（<c>(rect − captureTopLeft) × zoomLevel</c>）し、<br/>
    /// 4 辺を 1px 破線（1px 描画 → 1px 空白）で描画する。<br/>
    /// 色は <b>1 ピクセル（= RenderScaling 1.0 のとき 1 DIP）ごとに色A → 色B → 色A → …</b> と交互に切り替わり、<br/>
    /// 市松風の視認性を確保する。<br/>
    /// 選択矩形は半開区間のため、<b>終点のピクセルを含む閉区間（<c>幅 + 1</c>）</b>で描画する（終点の枠線が欠けない）。<br/>
    /// ルーペの切り出し領域と交差しない場合は何も描画しない。<br/>
    /// ズーム 1 倍では破線が潰れるため描画しない（2 倍以上で有効）。
    /// </remarks>
    public static void DrawSelectionRect(
        DrawingContext context, Rect selectionRect, Point captureTopLeft,
        int zoomLevel, int pixelSize, IBrush? colorA, IBrush? colorB)
    {
        if (colorA is null || colorB is null || zoomLevel < 2) return;

        // ドメイン座標 → ルーペ座標（拡大後）の変換と交差判定は純粋関数に委譲
        var transformed = ComputeSelectionRectTransform(selectionRect, captureTopLeft, zoomLevel, pixelSize);
        if (transformed is not { } rect) return;

        // 不透明度は colorA / colorB（ブラシ）のアルファに統合済み。そのまま描画する。
        var penA = new Pen(colorA, 1);
        var penB = new Pen(colorB, 1);
        double x = rect.X;
        double y = rect.Y;
        double w = rect.Width;
        double h = rect.Height;

        // 破線パターン: 1px 描画 → 1px 空白。色は 1 ピクセルごとに色A → 色B → 色A → … と交互に切り替える（市松風）
        const int dash = 1;
        const int gap = 1;
        const int step = dash + gap;

        // 上辺（y 固定。x 方向に破線）
        DrawDashedLine(context, new Point(x, y), new Point(x + w, y), dash, gap, step, penA, penB);

        // 下辺（y + h 固定。x 方向に破線）
        DrawDashedLine(context, new Point(x, y + h), new Point(x + w, y + h), dash, gap, step, penA, penB);

        // 左辺（x 固定。y 方向に破線）
        DrawDashedLine(context, new Point(x, y), new Point(x, y + h), dash, gap, step, penA, penB);

        // 右辺（x + w 固定。y 方向に破線）
        DrawDashedLine(context, new Point(x + w, y), new Point(x + w, y + h), dash, gap, step, penA, penB);
    }

    /// <summary>選択矩形（ドメイン座標）をルーペ座標（拡大後）へ変換し、切り出し領域との交差を判定する（純粋関数）。</summary>
    /// <param name="selectionRect">選択矩形（ドメイン座標系 = 元画像ピクセル座標）。半開区間（幅 = 終点 − 始点）で指定する。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（ドメイン座標系）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <returns>
    /// ルーペ座標へ変換した矩形（<c>X, Y, Width, Height</c>。閉区間のため幅・高さは <c>+1</c> 済み）。<br/>
    /// 切り出し領域と交差しない場合、またはズーム 1 倍以下（破線が潰れる）の場合は null。
    /// </returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 選択矩形は半開区間のため、終点のピクセルを含むよう +1（= 1 ドメインピクセル）する（閉区間化）。<br/>
    /// ズーム 1 倍では破線が潰れるため null を返す（2 倍以上で有効）。
    /// </remarks>
    public static Rect? ComputeSelectionRectTransform(
        Rect selectionRect, Point captureTopLeft, int zoomLevel, int pixelSize)
    {
        if (zoomLevel < 2) return null;

        // ドメイン座標 → ルーペ座標（拡大後）。
        // 選択矩形は半開区間（幅 = 終点 − 始点）のため、終点のピクセルを含むよう +1（= 1 ドメインピクセル）する（閉区間化）。
        double x = (selectionRect.X - captureTopLeft.X) * zoomLevel;
        double y = (selectionRect.Y - captureTopLeft.Y) * zoomLevel;
        double w = (selectionRect.Width + 1) * zoomLevel;
        double h = (selectionRect.Height + 1) * zoomLevel;

        // ルーペの切り出し領域と交差しない場合は描画しない
        if (x + w < 0 || y + h < 0 || x > pixelSize || y > pixelSize) return null;

        return new Rect(x, y, w, h);
    }

    /// <summary>2 点間を 2 色のダッシュ破線（2px 描画 → 2px 空白）で描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="from">始点。</param>
    /// <param name="to">終点。</param>
    /// <param name="dash">ダッシュ長。</param>
    /// <param name="gap">ギャップ長。</param>
    /// <param name="step">ダッシュ + ギャップの周期。</param>
    /// <param name="penA">色A のペン。</param>
    /// <param name="penB">色B のペン。</param>
    /// <remarks>水平（X 変化）または垂直（Y 変化）の直線を想定する（斜線には非対応）。</remarks>
    internal static void DrawDashedLine(
        DrawingContext context, Point from, Point to, double dash, double gap, double step,
        Pen penA, Pen penB)
    {
        bool horizontal = Math.Abs(to.Y - from.Y) < Math.Abs(to.X - from.X);
        double length = horizontal ? to.X - from.X : to.Y - from.Y;
        double start = horizontal ? from.X : from.Y;
        double fixedCoord = horizontal ? from.Y : from.X;
        double sign = length >= 0 ? 1 : -1;
        double distance = Math.Abs(length);

        int dashIndex = 0;
        for (double pos = 0; pos < distance; pos += step)
        {
            double segStart = start + sign * pos;
            double segEnd = start + sign * Math.Min(pos + dash, distance);
            // ダッシュごとに色を交互（0 始まり = 色A）
            var pen = dashIndex % 2 == 0 ? penA : penB;
            dashIndex++;

            if (horizontal)
            {
                context.DrawLine(pen, new Point(segStart, fixedCoord), new Point(segEnd, fixedCoord));
            }
            else
            {
                context.DrawLine(pen, new Point(fixedCoord, segStart), new Point(fixedCoord, segEnd));
            }
        }
    }
}

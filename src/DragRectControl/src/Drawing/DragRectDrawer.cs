using Estable.AvaloniaUI.DragRect.Selection;
using Avalonia;
using Avalonia.Media;
using System.Globalization;

namespace Estable.AvaloniaUI.DragRect.Drawing;

/// <summary>リサイズ可能なドラッグ矩形の描画（選択枠線・ドラッグハンドル・始点マーカー・カーソル座標ラベル）を提供する静的クラス。</summary>
/// <remarks>
/// 矩形ドメイン（DragRect）の視覚表現を担当する。<br/>
/// ハンドル位置は <see cref="Selection.SelectionGeometry.GetHandlePosition"/>（DRY）から取得し、中心に描画する。<br/>
/// <br/>
/// 暗転マスク・サイズラベル等の選択オーバーレイ描画は Selection 側（FadeMaskDrawer）が担う。<br/>
/// </remarks>
internal static class DragRectDrawer
{
    /// <summary>選択枠線を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="rect">選択矩形。</param>
    /// <param name="borderColor">枠線の色（不透明度は色のアルファに統合）。</param>
    /// <param name="borderThickness">枠線の太さ。</param>
    public static void DrawBorder(DrawingContext context, Rect rect, Color borderColor, double borderThickness)
    {
        var borderBrush = new SolidColorBrush(borderColor);
        var borderPen = new Pen(borderBrush, borderThickness);
        context.DrawRectangle(borderPen, rect);
    }

    /// <summary>ドラッグハンドル（白色矩形＋枠線）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="rect">選択矩形。</param>
    /// <param name="handleSize">ハンドルの一辺サイズ。</param>
    /// <param name="handleColor">ハンドルの色。デフォルト（透明）は白。</param>
    /// <param name="borderBrush">枠線ブラシ（ハンドル枠に使用）。</param>
    /// <remarks>
    /// 8方向のハンドル（None / Move を除く）を列挙し、<see cref="Selection.SelectionGeometry.GetHandlePosition"/> から<br/>
    /// 中心位置を取得して描画する。新ハンドル追加時は DragHandle の追加のみで描画が追従する（OCP）。<br/>
    /// </remarks>
    public static void DrawHandles(DrawingContext context, Rect rect, double handleSize, Color handleColor, IBrush borderBrush)
    {
        var handleFill = new SolidColorBrush(handleColor == default ? Colors.White : handleColor);
        var handlePen = new Pen(borderBrush, 1.5);
        double hs = handleSize;
        double hh = hs / 2;

        foreach (var handle in Enum.GetValues<DragHandle>())
        {
            if (handle == DragHandle.None || handle == DragHandle.Move) continue;
            var center = SelectionGeometry.GetHandlePosition(rect, handle);
            DrawHandle(context, center.X - hh, center.Y - hh, hs, hs, handleFill, handlePen);
        }
    }

    /// <summary>始点マーカー（十字線）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="center">始点の中心位置。</param>
    /// <param name="color">十字線の色。</param>
    /// <param name="thickness">十字線の太さ。</param>
    /// <param name="size">十字線の半幅。</param>
    public static void DrawStartMarker(DrawingContext context, Point center, Color color, double thickness, double size)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        context.DrawLine(pen, new Point(center.X - size, center.Y), new Point(center.X + size, center.Y));
        context.DrawLine(pen, new Point(center.X, center.Y - size), new Point(center.X, center.Y + size));
    }

    /// <summary>ドラッグハンドル（矩形＋枠線）を描画する。</summary>
    private static void DrawHandle(DrawingContext context, double x, double y, double w, double h, IBrush fill, Pen pen)
    {
        var r = new Rect(x, y, w, h);
        context.FillRectangle(fill, r);
        context.DrawRectangle(pen, r);
    }

    /// <summary>カーソルの右側にカーソルのドメイン座標ラベル（"(x, y)"）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="cursorDip">カーソルのローカル DIP 座標。</param>
    /// <param name="domainPos">カーソルのドメイン座標（表示する値）。</param>
    /// <param name="clientSize">描画領域のサイズ（はみ出しクランプ用）。</param>
    /// <param name="textColor">ラベルの文字色。</param>
    /// <param name="bgColor">ラベルの背景色（不透明度は色のアルファに統合）。</param>
    /// <param name="fontSize">ラベルのフォントサイズ。</param>
    /// <remarks>
    /// カーソルの右側（+8px オフセット）に座標テキストを背景付きで描画する。<br/>
    /// ラベルが描画領域の右端・下端をはみ出す場合は、領域内へクランプして表示する（ラベル全体が見える）。<br/>
    /// 座標は整数に丸めて "(x, y)" 形式で表示する（ピクセル座標として可読性を優先）。
    /// </remarks>
    public static void DrawCoordLabel(
        DrawingContext context,
        Point cursorDip,
        Point domainPos,
        Size clientSize,
        Color textColor,
        Color bgColor,
        double fontSize)
    {
        var text = $"({domainPos.X:F0}, {domainPos.Y:F0})";
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            new SolidColorBrush(textColor));

        var pad = 4.0;
        var w = ft.Width + pad * 2;
        var h = ft.Height + pad * 2;
        // ラベル配置（カーソル右側 + クランプ）は純粋関数に委譲
        var labelPos = ComputeCoordLabelPosition(cursorDip, clientSize, w, h);

        // 不透明度は bgColor のアルファに統合済み（SolidColorBrush がアルファを反映する）
        var bgBrush = new SolidColorBrush(bgColor);
        context.FillRectangle(bgBrush, new Rect(labelPos.X, labelPos.Y, w, h));
        context.DrawText(ft, new Point(labelPos.X + pad, labelPos.Y + pad));
    }

    /// <summary>カーソル座標ラベルの左上位置を計算する（純粋関数）。</summary>
    /// <param name="cursorDip">カーソルのローカル DIP 座標。</param>
    /// <param name="clientSize">描画領域のサイズ（はみ出しクランプ用）。</param>
    /// <param name="labelWidth">ラベルの幅（DIP）。</param>
    /// <param name="labelHeight">ラベルの高さ（DIP）。</param>
    /// <returns>ラベルの左上位置。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// カーソルの右側（+8px オフセット）に配置し、描画領域の右端・下端をはみ出す場合は<br/>
    /// 領域内へクランプする（ラベル全体が見えるよう左寄せ・上寄せ）。
    /// </remarks>
    public static Point ComputeCoordLabelPosition(Point cursorDip, Size clientSize, double labelWidth, double labelHeight)
    {
        double x = cursorDip.X + 8;
        double y = cursorDip.Y + 8;
        if (clientSize.Width > 0) x = Math.Min(x, Math.Max(0, clientSize.Width - labelWidth));
        if (clientSize.Height > 0) y = Math.Min(y, Math.Max(0, clientSize.Height - labelHeight));
        return new Point(x, y);
    }
}




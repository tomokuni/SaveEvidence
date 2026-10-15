using System.Drawing.Drawing2D;

namespace EsUtil.SaveEvidence.App.WinForms.Icons;

/// <summary>アイコンを GDI+ の図形で描く単一の描画層。</summary>
/// <remarks>
/// <b>SVG を使わない理由</b>: 本 UI は GDI+ のみで描き、SVG のラスタライズを行う依存を持たない。<br/>
/// アイコンはサイズと色を引数で受け取り、<b>用途ごとに 1 枚のビットマップとして描く</b>（テーマの変更時に描き直す）。<br/>
/// <b>純粋な描画</b>: 描画は <see cref="Bitmap"/> と <c>Graphics.FromImage</c> だけで完結するため、<br/>
/// ウィンドウ（ハンドル）を生成せずに画素を検証できる。
/// </remarks>
internal static class IconLoader
{
    /// <summary>描画に使う余白の比率（一辺に対する比率）。</summary>
    /// <remarks>線の端がアイコンの外接矩形へ接しないようにする。</remarks>
    private const float Inset = 0.08f;

    /// <summary>線の太さの比率（一辺に対する比率）。</summary>
    private const float StrokeRatio = 0.085f;

    /// <summary>アイコンをビットマップとして描く。</summary>
    /// <param name="icon">描くアイコン。</param>
    /// <param name="size">一辺のピクセル数（1 未満は 1 として扱う）。</param>
    /// <param name="color">線と塗りの色。</param>
    /// <returns>描いたビットマップ（<c>Format32bppPArgb</c>）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// 背景は透明にする（ボタンの背景・テーマの面の上へ重ねて使う）。<br/>
    /// 線はアンチエイリアスを有効にして描く（16 ピクセルでも字形が潰れないようにする）。
    /// </remarks>
    public static Bitmap Create(AppIcon icon, int size, Color color)
    {
        var pixels = Math.Max(1, size);
        var bitmap = new Bitmap(pixels, pixels, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var pen = new Pen(color, Math.Max(1f, pixels * StrokeRatio))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var brush = new SolidBrush(color);

        Draw(graphics, icon, new RectangleF(0, 0, pixels, pixels), pen, brush);
        return bitmap;
    }

    /// <summary>アイコンを指定の領域へ描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="icon">描くアイコン。</param>
    /// <param name="bounds">描画先の領域（正方形）。</param>
    /// <param name="pen">線に使うペン。</param>
    /// <param name="brush">塗りに使うブラシ。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> / <paramref name="pen"/> / <paramref name="brush"/> が null の場合。</exception>
    private static void Draw(Graphics graphics, AppIcon icon, RectangleF bounds, Pen pen, Brush brush)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(brush);

        var area = Deflate(bounds);
        switch (icon)
        {
            case AppIcon.Menu:
                DrawMenu(graphics, area, pen);
                break;
            case AppIcon.Save:
                DrawSave(graphics, area, pen, brush);
                break;
            case AppIcon.ScreenSelect:
                DrawScreen(graphics, area, pen);
                break;
            case AppIcon.WindowSelect:
                DrawWindow(graphics, area, pen);
                break;
            case AppIcon.AreaSelect:
                DrawAreaSelect(graphics, area, pen);
                break;
            case AppIcon.Crop:
                DrawCrop(graphics, area, pen);
                break;
            case AppIcon.AutoCrop:
                DrawAutoCrop(graphics, area, pen);
                break;
            case AppIcon.Copy:
                DrawCopy(graphics, area, pen);
                break;
            case AppIcon.Paste:
                DrawPaste(graphics, area, pen);
                break;
            case AppIcon.Undo:
                DrawUndo(graphics, area, pen, true);
                break;
            case AppIcon.Redo:
                DrawUndo(graphics, area, pen, false);
                break;
            case AppIcon.Refresh:
                DrawRefresh(graphics, area, pen);
                break;
            case AppIcon.SortAscending:
                DrawSort(graphics, area, pen, ascending: true);
                break;
            case AppIcon.SortDescending:
                DrawSort(graphics, area, pen, ascending: false);
                break;
            case AppIcon.Settings:
                DrawSettings(graphics, area, pen, brush);
                break;
            case AppIcon.Folder:
                DrawFolder(graphics, area, pen);
                break;
            case AppIcon.ZoomIn:
                DrawZoom(graphics, area, pen, zoomIn: true);
                break;
            case AppIcon.ZoomOut:
                DrawZoom(graphics, area, pen, zoomIn: false);
                break;
            case AppIcon.ZoomFit:
                DrawZoomFit(graphics, area, pen);
                break;
            case AppIcon.NavToggle:
                DrawNavToggle(graphics, area, pen);
                break;
            default:
                DrawWindow(graphics, area, pen);
                break;
        }
    }

    /// <summary>領域を内側へ縮めた矩形を返す。</summary>
    /// <param name="bounds">元の領域。</param>
    /// <returns>内側へ縮めた領域。</returns>
    private static RectangleF Deflate(RectangleF bounds)
    {
        var inset = bounds.Width * Inset;
        return RectangleF.Inflate(bounds, -inset, -inset);
    }

    /// <summary>横 3 本線（メニュー）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawMenu(Graphics graphics, RectangleF area, Pen pen)
    {
        for (var i = 0; i < 3; i++)
        {
            var y = area.Top + (area.Height * (0.22f + (i * 0.28f)));
            graphics.DrawLine(pen, area.Left, y, area.Right, y);
        }
    }

    /// <summary>フロッピー ディスク（保存）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    /// <param name="brush">塗りのブラシ。</param>
    private static void DrawSave(Graphics graphics, RectangleF area, Pen pen, Brush brush)
    {
        graphics.DrawRectangle(pen, area.Left, area.Top, area.Width, area.Height);

        // 上部のシャッター（書き込み窓）と下部のラベルを塗りで示す
        var shutter = new RectangleF(area.Left + (area.Width * 0.28f), area.Top, area.Width * 0.44f, area.Height * 0.36f);
        graphics.FillRectangle(brush, shutter);

        var label = new RectangleF(
            area.Left + (area.Width * 0.2f), area.Top + (area.Height * 0.62f), area.Width * 0.6f, area.Height * 0.38f);
        graphics.DrawRectangle(pen, label.Left, label.Top, label.Width, label.Height);
    }

    /// <summary>モニターと選択マーク（スクリーン選択）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawScreen(Graphics graphics, RectangleF area, Pen pen)
    {
        graphics.DrawRectangle(pen, area.Left, area.Top, area.Width, area.Height * 0.78f);

        var mark = area.Width * 0.28f;
        var offset = area.Width * 0.06f;
        var top = area.Top + (area.Height * 0.02f);
        var left = area.Left + offset;
        var right = area.Right - offset;
        var bottom = area.Top + (area.Height * 0.76f);

        graphics.DrawLine(pen, left, top, left + mark, top);
        graphics.DrawLine(pen, left, top, left, top + mark);
        graphics.DrawLine(pen, right, top, right - mark, top);
        graphics.DrawLine(pen, right, top, right, top + mark);
        graphics.DrawLine(pen, left, bottom, left + mark, bottom);
        graphics.DrawLine(pen, left, bottom, left, bottom - mark);
        graphics.DrawLine(pen, right, bottom, right - mark, bottom);
        graphics.DrawLine(pen, right, bottom, right, bottom - mark);
    }

    /// <summary>重なる 2 枚の矩形（ウィンドウ選択）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawWindow(Graphics graphics, RectangleF area, Pen pen)
    {
        var back = new RectangleF(area.Left, area.Top, area.Width * 0.68f, area.Height * 0.68f);
        var front = new RectangleF(
            area.Left + (area.Width * 0.32f), area.Top + (area.Height * 0.32f), area.Width * 0.68f, area.Height * 0.68f);
        graphics.DrawRectangle(pen, back.Left, back.Top, back.Width, back.Height);
        graphics.DrawRectangle(pen, front.Left, front.Top, front.Width, front.Height);
    }

    /// <summary>破線の矩形（範囲選択）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawAreaSelect(Graphics graphics, RectangleF area, Pen pen)
    {
        using var dashed = (Pen)pen.Clone();
        dashed.DashStyle = DashStyle.Dash;
        dashed.DashPattern = [3f, 2f];
        graphics.DrawRectangle(dashed, area.Left, area.Top, area.Width, area.Height);

        var mark = area.Width * 0.22f;
        graphics.DrawLine(pen, area.Left, area.Top, area.Left + mark, area.Top);
        graphics.DrawLine(pen, area.Left, area.Top, area.Left, area.Top + mark);
        graphics.DrawLine(pen, area.Right, area.Bottom, area.Right - mark, area.Bottom);
        graphics.DrawLine(pen, area.Right, area.Bottom, area.Right, area.Bottom - mark);
    }

    /// <summary>切り出しマークを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawCrop(Graphics graphics, RectangleF area, Pen pen)
    {
        // 左上（縦線 → 下辺）と右下（横線 → 右辺）の 2 本の切り出しマーク
        var x = area.Left + (area.Width * 0.3f);
        var y = area.Bottom - (area.Height * 0.3f);
        graphics.DrawLine(pen, x, area.Top, x, y);
        graphics.DrawLine(pen, x, y, area.Right, y);

        var x2 = area.Right - (area.Width * 0.3f);
        var y2 = area.Top + (area.Height * 0.3f);
        graphics.DrawLine(pen, area.Left, y2, x2, y2);
        graphics.DrawLine(pen, x2, y2, x2, area.Bottom);
    }

    /// <summary>矩形と拡大鏡（自動切り出し）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawAutoCrop(Graphics graphics, RectangleF area, Pen pen)
    {
        graphics.DrawRectangle(pen, area.Left, area.Top, area.Width * 0.78f, area.Height * 0.7f);

        var radius = area.Width * 0.24f;
        var center = new PointF(area.Right - radius, area.Bottom - radius);
        graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.DrawLine(
            pen,
            center.X + (radius * 0.7f),
            center.Y + (radius * 0.7f),
            center.X + (radius * 1.5f),
            center.Y + (radius * 1.5f));
    }

    /// <summary>重なる 2 枚の矩形（コピー）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawCopy(Graphics graphics, RectangleF area, Pen pen)
    {
        var back = new RectangleF(area.Left, area.Top, area.Width * 0.6f, area.Height * 0.6f);
        var front = new RectangleF(
            area.Left + (area.Width * 0.4f), area.Top + (area.Height * 0.4f), area.Width * 0.6f, area.Height * 0.6f);
        graphics.DrawRectangle(pen, back.Left, back.Top, back.Width, back.Height);
        graphics.DrawRectangle(pen, front.Left, front.Top, front.Width, front.Height);
    }

    /// <summary>クリップボード（貼り付け）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawPaste(Graphics graphics, RectangleF area, Pen pen)
    {
        var body = new RectangleF(area.Left, area.Top + (area.Height * 0.14f), area.Width, area.Height * 0.86f);
        graphics.DrawRectangle(pen, body.Left, body.Top, body.Width, body.Height);

        var tab = new RectangleF(
            area.Left + (area.Width * 0.3f), area.Top, area.Width * 0.4f, area.Height * 0.22f);
        graphics.FillRectangle(pen.Brush, tab);
    }

    /// <summary>曲がった矢印（元に戻す / 取り消し）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    /// <param name="backward">元に戻す方向の場合は true。 取り消しの場合は false。</param>
    private static void DrawUndo(Graphics graphics, RectangleF area, Pen pen, bool backward)
    {
        var diameter = area.Width * 0.86f;
        var arc = new RectangleF(
            area.Left + ((area.Width - diameter) / 2), area.Top + (area.Height * 0.16f), diameter, diameter);

        graphics.DrawArc(pen, arc, backward ? 300f : 180f, 300f);

        // 矢印の頭を弧の端へ付ける
        var head = area.Width * 0.26f;
        if (backward)
        {
            graphics.DrawLine(pen, arc.Left, arc.Top + (arc.Height * 0.5f), arc.Left + head, arc.Top + (arc.Height * 0.5f) - head);
            graphics.DrawLine(pen, arc.Left, arc.Top + (arc.Height * 0.5f), arc.Left + head, arc.Top + (arc.Height * 0.5f) + head);
        }
        else
        {
            graphics.DrawLine(pen, arc.Right, arc.Top + (arc.Height * 0.5f), arc.Right - head, arc.Top + (arc.Height * 0.5f) - head);
            graphics.DrawLine(pen, arc.Right, arc.Top + (arc.Height * 0.5f), arc.Right - head, arc.Top + (arc.Height * 0.5f) + head);
        }
    }

    /// <summary>円弧の矢印（更新）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawRefresh(Graphics graphics, RectangleF area, Pen pen)
    {
        graphics.DrawArc(pen, area, 60f, 280f);

        var head = area.Width * 0.24f;
        var tip = new PointF(area.Left + (area.Width * 0.95f), area.Top + (area.Height * 0.28f));
        graphics.DrawLine(pen, tip.X, tip.Y, tip.X - head, tip.Y);
        graphics.DrawLine(pen, tip.X, tip.Y, tip.X, tip.Y - head);
    }

    /// <summary>並べ替え（三角と 3 本線）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    /// <param name="ascending">昇順の場合は true。</param>
    private static void DrawSort(Graphics graphics, RectangleF area, Pen pen, bool ascending)
    {
        var triangleWidth = area.Width * 0.3f;
        var top = ascending ? area.Top + (area.Height * 0.1f) : area.Top + (area.Height * 0.9f);
        var tipY = ascending ? top + (area.Height * 0.3f) : top - (area.Height * 0.3f);
        var centerX = area.Left + (area.Width * 0.2f);
        graphics.DrawLine(pen, centerX, top, centerX, tipY);
        graphics.DrawLine(pen, centerX, top, centerX - (triangleWidth / 2), top);
        graphics.DrawLine(pen, centerX, top, centerX + (triangleWidth / 2), top);

        for (var i = 0; i < 3; i++)
        {
            var y = area.Top + (area.Height * (0.2f + (i * 0.3f)));
            graphics.DrawLine(pen, area.Left + (area.Width * 0.45f), y, area.Right, y);
        }
    }

    /// <summary>歯車（設定）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    /// <param name="brush">塗りのブラシ。</param>
    private static void DrawSettings(Graphics graphics, RectangleF area, Pen pen, Brush brush)
    {
        var radius = area.Width * 0.34f;
        var center = new PointF(area.Left + (area.Width / 2), area.Top + (area.Height / 2));
        graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);

        var hole = radius * 0.4f;
        graphics.FillEllipse(brush, center.X - hole, center.Y - hole, hole * 2, hole * 2);

        for (var i = 0; i < 8; i++)
        {
            var angle = Math.PI * i / 4;
            var inner = radius * 1.05f;
            var outer = radius * 1.5f;
            graphics.DrawLine(
                pen,
                center.X + (float)(Math.Cos(angle) * inner),
                center.Y + (float)(Math.Sin(angle) * inner),
                center.X + (float)(Math.Cos(angle) * outer),
                center.Y + (float)(Math.Sin(angle) * outer));
        }
    }

    /// <summary>フォルダを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawFolder(Graphics graphics, RectangleF area, Pen pen)
    {
        var tab = new RectangleF(area.Left, area.Top + (area.Height * 0.12f), area.Width * 0.45f, area.Height * 0.2f);
        var body = new RectangleF(area.Left, area.Top + (area.Height * 0.24f), area.Width, area.Height * 0.66f);
        graphics.DrawRectangle(pen, tab.Left, tab.Top, tab.Width, tab.Height);
        graphics.DrawRectangle(pen, body.Left, body.Top, body.Width, body.Height);
    }

    /// <summary>拡大鏡（拡大 / 縮小）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    /// <param name="zoomIn">拡大の場合は true（＋印）。</param>
    private static void DrawZoom(Graphics graphics, RectangleF area, Pen pen, bool zoomIn)
    {
        var radius = area.Width * 0.32f;
        var center = new PointF(area.Left + (area.Width * 0.42f), area.Top + (area.Height * 0.42f));
        graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.DrawLine(pen, center.X + (radius * 0.7f), center.Y + (radius * 0.7f), area.Right, area.Bottom);
        graphics.DrawLine(pen, center.X - (radius * 0.5f), center.Y, center.X + (radius * 0.5f), center.Y);

        if (zoomIn)
        {
            graphics.DrawLine(pen, center.X, center.Y - (radius * 0.5f), center.X, center.Y + (radius * 0.5f));
        }
    }

    /// <summary>ウィンドウに合わせる（内向きの 4 つの角）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawZoomFit(Graphics graphics, RectangleF area, Pen pen)
    {
        graphics.DrawRectangle(pen, area.Left, area.Top, area.Width, area.Height);

        var mark = area.Width * 0.22f;
        graphics.DrawLine(pen, area.Left, area.Top, area.Left + mark, area.Top + mark);
        graphics.DrawLine(pen, area.Right, area.Top, area.Right - mark, area.Top + mark);
        graphics.DrawLine(pen, area.Left, area.Bottom, area.Left + mark, area.Bottom - mark);
        graphics.DrawLine(pen, area.Right, area.Bottom, area.Right - mark, area.Bottom - mark);
    }

    /// <summary>ナビゲーション パネルの開閉（枠と縦線）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">描画領域。</param>
    /// <param name="pen">線のペン。</param>
    private static void DrawNavToggle(Graphics graphics, RectangleF area, Pen pen)
    {
        graphics.DrawRectangle(pen, area.Left, area.Top, area.Width, area.Height);
        var x = area.Left + (area.Width * 0.38f);
        graphics.DrawLine(pen, x, area.Top, x, area.Bottom);
    }
}

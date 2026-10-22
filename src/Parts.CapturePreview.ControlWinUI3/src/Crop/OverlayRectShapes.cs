using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop;

/// <summary>オーバーレイ矩形の描画入力（<see cref="OverlayRectFrame"/>）を描画リストへ写す。</summary>
/// <remarks>
/// コアが確定した描画入力（配置・太さ・破線の判定と長さ・色）を矩形の列へ写すだけの層であり、<br/>
/// 式・判断・クランプを持たない（描画手順として「枠を内側へ寄せる」「背景→前景の順に置く」だけを持つ）。<br/>
/// 実線は前景色の枠のみ、破線は「背景色の実線 → 前景色の破線」の 2 回塗りで市松風の視認性を出す<br/>
/// （他の UI 版が背景色の下層矩形と前景色の破線矩形を重ねる方式と等価）。<br/>
/// <br/>
/// <b>XAML の要素を作らない</b>: 本クラスは <see cref="DrawList"/> を埋めるだけであるため、ウィンドウ無しで検証できる。<br/>
/// 要素への反映は <see cref="RectShapeLayer"/> が担う。
/// </remarks>
internal static class OverlayRectShapes
{
    /// <summary>描画入力を描画リストへ追加する。</summary>
    /// <param name="list">追加先の描画リスト。 null 不可。</param>
    /// <param name="frame">描画入力（コアが確定した配置・太さ・破線・色）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> が null の場合。</exception>
    /// <remarks>描画できる大きさでない場合（幅または高さが 0 以下）は何も追加しない。</remarks>
    public static void Add(DrawList list, OverlayRectFrame frame)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (!frame.HasDrawableSize)
        {
            return;
        }

        var bounds = frame.Bounds;
        var thickness = frame.Thickness;

        // 枠は矩形の内側へ置く（外へはみ出して画像に掛からないようにする）
        var x = bounds.X;
        var y = bounds.Y;
        var width = bounds.Width;
        var height = bounds.Height;

        if (frame.DrawsBackground)
        {
            // 背景色の実線を下層に置く（破線の隙間から見える市松風の下地）
            AddOutline(list, x, y, width, height, thickness, frame.Background);
        }

        if (frame.IsDashed)
        {
            AddDashes(list, x, y, width, height, frame);
            return;
        }

        AddOutline(list, x, y, width, height, thickness, frame.Foreground);
    }

    /// <summary>矩形の輪郭を 4 辺の矩形として追加する。</summary>
    /// <param name="list">追加先の描画リスト。</param>
    /// <param name="x">左端。</param>
    /// <param name="y">上端。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="thickness">線の太さ。</param>
    /// <param name="color">線の色。</param>
    private static void AddOutline(
        DrawList list, double x, double y, double width, double height, double thickness, DrawingColor color)
    {
        list.AddRect(new EsRect(x, y, width, thickness), color);
        list.AddRect(new EsRect(x, y + height - thickness, width, thickness), color);
        list.AddRect(new EsRect(x, y, thickness, height), color);
        list.AddRect(new EsRect(x + width - thickness, y, thickness, height), color);
    }

    /// <summary>矩形の輪郭を破線で追加する。</summary>
    /// <param name="list">追加先の描画リスト。</param>
    /// <param name="x">左端。</param>
    /// <param name="y">上端。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="frame">描画入力（破線の長さ・間隔・太さ・色）。</param>
    /// <remarks>
    /// 破線の区間は 4 辺を 1 本の周回として数える（角で破線が途切れない）。<br/>
    /// <b>破線の列挙を自前で持たない</b>: 他の UI 版と同じ破線にするため、長さと間隔はコアの値だけを使う。
    /// </remarks>
    private static void AddDashes(DrawList list, double x, double y, double width, double height, OverlayRectFrame frame)
    {
        var period = frame.DashLength + frame.GapLength;
        if (period <= 0)
        {
            AddOutline(list, x, y, width, height, frame.Thickness, frame.Foreground);
            return;
        }

        // 4 辺を 1 本の周回として、破線の開始位置からの距離で区間を求める
        var perimeter = (width + height) * 2;
        for (var start = 0.0; start < perimeter; start += period)
        {
            var end = Math.Min(start + frame.DashLength, perimeter);
            AddPerimeterSegment(list, x, y, width, height, start, end, frame.Thickness, frame.Foreground);
        }
    }

    /// <summary>周回上の区間を矩形として追加する。</summary>
    /// <param name="list">追加先の描画リスト。</param>
    /// <param name="x">左端。</param>
    /// <param name="y">上端。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="start">区間の開始（周回上の距離）。</param>
    /// <param name="end">区間の終了（周回上の距離）。</param>
    /// <param name="thickness">線の太さ。</param>
    /// <param name="color">線の色。</param>
    /// <remarks>区間が辺をまたぐ場合は辺ごとに分けて追加する（矩形は 1 辺に沿う形でしか表せないため）。</remarks>
    private static void AddPerimeterSegment(
        DrawList list, double x, double y, double width, double height,
        double start, double end, double thickness, DrawingColor color)
    {
        var top = width;
        var right = top + height;
        var bottom = right + width;
        var total = bottom + height;

        AddHorizontalSpan(list, x, y, width, start, Math.Min(end, top), thickness, color);
        AddVerticalSpan(list, x + width - thickness, y, height, start - top, Math.Min(end, right) - top, thickness, color);
        AddHorizontalSpan(list, x, y + height - thickness, width, start - right, Math.Min(end, bottom) - right, thickness, color);
        AddVerticalSpan(list, x, y, height, start - bottom, Math.Min(end, total) - bottom, thickness, color);
    }

    /// <summary>上辺または下辺の区間を追加する。</summary>
    /// <param name="list">追加先の描画リスト。</param>
    /// <param name="x">左端。</param>
    /// <param name="y">辺の上端。</param>
    /// <param name="width">辺の長さ。</param>
    /// <param name="start">区間の開始（辺の先頭からの距離）。</param>
    /// <param name="end">区間の終了（辺の先頭からの距離）。</param>
    /// <param name="thickness">線の太さ。</param>
    /// <param name="color">線の色。</param>
    private static void AddHorizontalSpan(
        DrawList list, double x, double y, double width, double start, double end, double thickness, DrawingColor color)
    {
        var from = Math.Max(0, start);
        var to = Math.Min(width, end);
        if (to > from)
        {
            list.AddRect(new EsRect(x + from, y, to - from, thickness), color);
        }
    }

    /// <summary>左辺または右辺の区間を追加する。</summary>
    /// <param name="list">追加先の描画リスト。</param>
    /// <param name="x">辺の左端。</param>
    /// <param name="y">上端。</param>
    /// <param name="height">辺の長さ。</param>
    /// <param name="start">区間の開始（辺の先頭からの距離）。</param>
    /// <param name="end">区間の終了（辺の先頭からの距離）。</param>
    /// <param name="thickness">線の太さ。</param>
    /// <param name="color">線の色。</param>
    private static void AddVerticalSpan(
        DrawList list, double x, double y, double height, double start, double end, double thickness, DrawingColor color)
    {
        var from = Math.Max(0, start);
        var to = Math.Min(height, end);
        if (to > from)
        {
            list.AddRect(new EsRect(x, y + from, thickness, to - from), color);
        }
    }
}

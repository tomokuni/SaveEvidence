using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView;

/// <summary>スクロールバーの描画内容（描画リスト）を組み立てる。</summary>
/// <remarks>
/// <b>計算を持たない</b>: トラックとサムの矩形はコア（<see cref="ZoomViewSession.CurrentScrollBars"/>）が確定する。<br/>
/// 本クラスは「どの順にどの色で塗るか」だけを持ち、XAML の要素を作らないため、ウィンドウ無しで検証できる。<br/>
/// <br/>
/// <b>描画順</b>: トラック → 枠 → サム（枠はトラックの縁、サムはトラックの上に載る）。<br/>
/// トラックの表示可否はコア（<see cref="ZoomViewAppearance.HasScrollBarTrack"/>）が単一所有する。
/// </remarks>
internal static class ZoomScrollBarShapes
{
    /// <summary>スクロールバーを描画リストへ追加する。</summary>
    /// <param name="list">追加先の描画リスト。 null 不可。</param>
    /// <param name="bars">水平・垂直のスクロールバー（スクロール不要な軸は null）。</param>
    /// <param name="appearance">外観（色・枠の太さ）。 null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> または <paramref name="appearance"/> が null の場合。</exception>
    public static void AddScrollBars(
        DrawList list,
        (ZoomScrollBar? Horizontal, ZoomScrollBar? Vertical) bars,
        ZoomViewAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(appearance);

        AddScrollBar(list, bars.Horizontal, appearance);
        AddScrollBar(list, bars.Vertical, appearance);
    }

    /// <summary>スクロールバー 1 本を描画リストへ追加する。</summary>
    /// <param name="list">追加先の描画リスト。</param>
    /// <param name="bar">追加するスクロールバー。 null の場合は何もしない。</param>
    /// <param name="appearance">外観。</param>
    private static void AddScrollBar(DrawList list, ZoomScrollBar? bar, ZoomViewAppearance appearance)
    {
        if (bar is not { } value)
        {
            return;
        }

        if (appearance.HasScrollBarTrack)
        {
            list.AddRect(value.Track, appearance.ScrollBarTrackColor);
        }

        if (appearance.HasScrollBarBorder)
        {
            AddBorder(list, value.Track, appearance);
        }

        list.AddRect(value.Thumb, appearance.ScrollBarColor);
    }

    /// <summary>トラックの枠を 4 辺の矩形として追加する。</summary>
    /// <param name="list">追加先の描画リスト。</param>
    /// <param name="track">トラックの矩形。</param>
    /// <param name="appearance">外観（枠の太さと色）。</param>
    /// <remarks>枠はトラックの内側へ置く（外へはみ出して画像に掛からないようにする）。</remarks>
    private static void AddBorder(DrawList list, EsRect track, ZoomViewAppearance appearance)
    {
        var thickness = appearance.ScrollBarBorderThickness;
        if (thickness <= 0)
        {
            return;
        }

        var x = track.X + thickness;
        var y = track.Y + thickness;
        var width = Math.Max(0, track.Width - (thickness * 2));
        var height = Math.Max(0, track.Height - (thickness * 2));
        if (width <= 0 || height <= 0)
        {
            return;
        }

        list.AddRect(new EsRect(x, y, width, thickness), appearance.ScrollBarBorderColor);
        list.AddRect(new EsRect(x, y + height - thickness, width, thickness), appearance.ScrollBarBorderColor);
        list.AddRect(new EsRect(x, y, thickness, height), appearance.ScrollBarBorderColor);
        list.AddRect(new EsRect(x + width - thickness, y, thickness, height), appearance.ScrollBarBorderColor);
    }
}

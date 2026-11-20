using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネルの位置を決める（純粋計算。 単一所有）。</summary>
/// <remarks>
/// <b>座標の単位は物理ピクセル</b>: OS のポインター位置もウィンドウの位置も物理ピクセルで扱う
/// （DPI の倍率を掛けたり割ったりするのは各 UI の 1 行変換に閉じ込める）。<br/>
/// <b>アプリのウィンドウの内側へ収めない</b>: 収めると狭いウィンドウでパネルが潰れる、または縁で切れる。<br/>
/// <b>編集対象に被せない</b>: 書式バーを本文の上へ重ねると、編集中の文字が隠れて読めない（実測 2026-11-20）。
/// 本文の外（上・下・右・左の順）で画面に収まる場所へ置く。<br/>
/// <b>画面上の位置へ収める</b>: 画面の外へ出すと掴めなくなるため、画面の作業領域へ収める。
/// </remarks>
public static class AnnotationPanelPlacement
{
    /// <summary>アンカー（右クリックした位置）をパネルの左上にする。</summary>
    /// <param name="anchorScreen">アンカーの位置（物理ピクセル）。</param>
    /// <returns>パネルの左上（物理ピクセル）。</returns>
    public static EsPointInt ResolveInitial(EsPointInt anchorScreen) => anchorScreen;

    /// <summary>対象（編集中の注釈）に被さらない位置を求める。</summary>
    /// <param name="target">被せたくない矩形（物理ピクセル）。</param>
    /// <param name="panel">パネルの大きさ（物理ピクセル）。</param>
    /// <param name="workArea">画面の作業領域（物理ピクセル）。</param>
    /// <param name="gap">対象とパネルの間の余白（物理ピクセル）。</param>
    /// <returns>パネルの左上（物理ピクセル）。</returns>
    /// <remarks>
    /// <b>上・下・右・左の順に探す</b>: 本文の上に置くのが最も自然で、入りきらない場合は下・右・左へ回る。
    /// どれも入りきらないときは上へ置き、画面の作業領域へ収める（見えないより重なるほうがよい）。
    /// </remarks>
    public static EsPointInt ResolveOutside(
        EsRect target, EsSizeInt panel, EsRect workArea, int gap)
    {
        var width = Math.Max(1, panel.Width);
        var height = Math.Max(1, panel.Height);
        var x = (int)Math.Round(target.X);
        var y = (int)Math.Round(target.Y);
        var right = (int)Math.Round(target.X + target.Width);
        var bottom = (int)Math.Round(target.Y + target.Height);

        // 上（対象の左上へ揃える）
        var top = y - gap - height;
        if (top >= workArea.Y)
        {
            return Clamp(new EsPointInt(x, top), width, height, workArea);
        }

        // 下
        var below = bottom + gap;
        if (below + height <= workArea.Y + workArea.Height)
        {
            return Clamp(new EsPointInt(x, below), width, height, workArea);
        }

        // 右
        var toRight = right + gap;
        if (toRight + width <= workArea.X + workArea.Width)
        {
            return Clamp(new EsPointInt(toRight, y), width, height, workArea);
        }

        // 左
        return Clamp(new EsPointInt(x - gap - width, y), width, height, workArea);
    }

    /// <summary>ドラッグ中のパネルの左上を求める。</summary>
    /// <param name="windowOrigin">ドラッグ開始時のパネルの左上（物理ピクセル）。</param>
    /// <param name="cursorOrigin">ドラッグ開始時のポインターの位置（物理ピクセル）。</param>
    /// <param name="cursorCurrent">現在のポインターの位置（物理ピクセル）。</param>
    /// <returns>移動先のパネルの左上（物理ピクセル）。</returns>
    public static EsPointInt ResolveDrag(
        EsPointInt windowOrigin,
        EsPointInt cursorOrigin,
        EsPointInt cursorCurrent)
        => new(
            windowOrigin.X + (cursorCurrent.X - cursorOrigin.X),
            windowOrigin.Y + (cursorCurrent.Y - cursorOrigin.Y));

    /// <summary>パネルが画面の作業領域からはみ出さない位置へ補正する。</summary>
    /// <param name="position">補正する位置。</param>
    /// <param name="width">パネルの幅。</param>
    /// <param name="height">パネルの高さ。</param>
    /// <param name="workArea">画面の作業領域。</param>
    /// <returns>補正した位置。</returns>
    private static EsPointInt Clamp(EsPointInt position, int width, int height, EsRect workArea)
    {
        var minX = (int)Math.Round(workArea.X);
        var minY = (int)Math.Round(workArea.Y);
        var maxX = Math.Max(minX, (int)Math.Round(workArea.X + workArea.Width) - width);
        var maxY = Math.Max(minY, (int)Math.Round(workArea.Y + workArea.Height) - height);
        return new EsPointInt(Math.Clamp(position.X, minX, maxX), Math.Clamp(position.Y, minY, maxY));
    }
}

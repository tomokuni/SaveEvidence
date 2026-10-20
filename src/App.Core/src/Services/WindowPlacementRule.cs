using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>保存されたウィンドウ配置を復元してよいかの判定（UI 非依存の共有規則）。</summary>
/// <remarks>
/// 保存値はウィンドウ枠を含む画面物理ピクセルの矩形であり、次の 2 つを満たさないと復元できない。<br/>
/// 1. 幅・高さが 1 以上（0 サイズのウィンドウを作らない）。<br/>
/// 2. 画面の表示領域と十分に重なっている（画面外の座標を復元すると、ウィンドウが見えないまま起動する）。<br/>
/// <br/>
/// <b>画面外の座標が保存される経路</b>: 撮影中はオーナー ウィンドウを画面外（<c>-32000</c>）へ退避するため、<br/>
/// その間に終了すると退避先の座標が保存される。 また、モニター構成を変えると以前の座標が表示領域の外になる。<br/>
/// どちらの場合も「画面外のまま起動して操作できない」ため、復元をあきらめて既定の配置で表示する。<br/>
/// <br/>
/// <b>最小化は復元しない</b>: 終了時に最小化していた場合、次回起動も最小化で始まると<br/>
/// ウィンドウが表示されず「起動していない」ように見える（タスクバーからの復帰だけが手掛かりになる）。
/// </remarks>
public static class WindowPlacementRule
{
    /// <summary>復元を許すために表示領域と重なっていなければならない最小の幅・高さ（画面物理ピクセル）。</summary>
    /// <remarks>
    /// 1 ピクセルでも見えていれば復元可とすると、ほぼ画面外の状態を許してしまうため、<br/>
    /// タイトルバーを掴める程度（64 ピクセル）を下限にする。
    /// </remarks>
    public const int MinimumVisibleSize = 64;

    /// <summary>保存された配置を復元してよいかどうかを判定する。</summary>
    /// <param name="bounds">保存された配置（画面物理ピクセル）。 null 可。</param>
    /// <param name="visibleArea">画面の表示領域（全モニターを包含する矩形。 画面物理ピクセル）。</param>
    /// <returns>幅・高さが 1 以上で、表示領域と <see cref="MinimumVisibleSize"/> 以上重なる場合は true。</returns>
    public static bool CanRestore(EsRectInt? bounds, EsRectInt visibleArea)
    {
        if (bounds is not { Width: > 0, Height: > 0 } value)
        {
            return false;
        }

        var overlapWidth = Math.Min(value.X + value.Width, visibleArea.X + visibleArea.Width)
            - Math.Max(value.X, visibleArea.X);
        var overlapHeight = Math.Min(value.Y + value.Height, visibleArea.Y + visibleArea.Height)
            - Math.Max(value.Y, visibleArea.Y);

        return overlapWidth >= MinimumVisibleSize && overlapHeight >= MinimumVisibleSize;
    }

    /// <summary>復元する表示状態を返す（最小化は通常表示へ置き換える）。</summary>
    /// <param name="state">保存された表示状態。</param>
    /// <returns>復元に使う表示状態。 最小化の場合は <see cref="EsWindowState.Normal"/>。</returns>
    public static EsWindowState ToRestorableState(EsWindowState state)
        => state == EsWindowState.Minimized ? EsWindowState.Normal : state;

    /// <summary>保存された配置を表示領域の内側へ収めた配置を返す。</summary>
    /// <param name="bounds">保存された配置（幅・高さは 1 以上であること）。</param>
    /// <param name="visibleArea">画面の表示領域（全モニターを包含する矩形）。</param>
    /// <returns>位置だけを表示領域の内側へ収めた配置（大きさは変えない）。</returns>
    /// <remarks>
    /// 大きさは変えない（利用者が決めた大きさを尊重する）。 画面より大きい場合は左上を表示領域へ合わせる。
    /// </remarks>
    public static EsRectInt ClampToVisibleArea(EsRectInt bounds, EsRectInt visibleArea)
    {
        var x = Math.Clamp(bounds.X, visibleArea.X, Math.Max(visibleArea.X, visibleArea.X + visibleArea.Width - bounds.Width));
        var y = Math.Clamp(bounds.Y, visibleArea.Y, Math.Max(visibleArea.Y, visibleArea.Y + visibleArea.Height - bounds.Height));
        return new EsRectInt(x, y, bounds.Width, bounds.Height);
    }

    /// <summary>DIP の長さを画面物理ピクセルへ換算する（最小サイズの指定に使う）。</summary>
    /// <param name="dip">DIP の長さ。</param>
    /// <param name="scaling">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>画面物理ピクセルの長さ（四捨五入・最小 1）。</returns>
    /// <remarks>
    /// WinUI 3 の <c>OverlappedPresenter.PreferredMinimumWidth</c> と WinForms の <c>Form.MinimumSize</c> は<br/>
    /// 画面物理ピクセルで指定するため、他 UI 版（DIP 指定）と同じ見た目の最小サイズにするには換算が要る。
    /// </remarks>
    public static int ToPhysicalLength(double dip, double scaling)
        => Math.Max(1, (int)Math.Round(dip * DpiScaler.NormalizeScaling(scaling)));

    /// <summary>物理ピクセルのウィンドウ枠を差し引き、外寸 DIP からクライアント DIP を求める。</summary>
    /// <param name="outerDip">枠を含む外寸（DIP）。</param>
    /// <param name="framePixels">枠の合計幅または高さ（画面物理ピクセル）。</param>
    /// <param name="scaling">DPI スケール係数。</param>
    /// <returns>クライアント領域の長さ（DIP）。</returns>
    public static double ToClientLength(double outerDip, int framePixels, double scaling)
        => Math.Max(0, outerDip - (Math.Max(0, framePixels) / DpiScaler.NormalizeScaling(scaling)));

    /// <summary>クライアント DIP と物理ピクセルのウィンドウ枠から外寸ピクセルを求める。</summary>
    /// <param name="clientDip">クライアント領域の長さ（DIP）。</param>
    /// <param name="framePixels">枠の合計幅または高さ（画面物理ピクセル）。</param>
    /// <param name="scaling">DPI スケール係数。</param>
    /// <returns>ウィンドウ外寸の長さ（画面物理ピクセル）。</returns>
    public static int ToOuterPhysicalLength(double clientDip, int framePixels, double scaling)
        => ToPhysicalLength(clientDip, scaling) + Math.Max(0, framePixels);

    /// <summary>物理ピクセルのウィンドウ枠を差し引き、外寸ピクセルからクライアント DIP を求める。</summary>
    /// <param name="outerPixels">枠を含む外寸（画面物理ピクセル）。</param>
    /// <param name="framePixels">枠の合計幅または高さ（画面物理ピクセル）。</param>
    /// <param name="scaling">DPI スケール係数。</param>
    /// <returns>クライアント領域の長さ（DIP）。</returns>
    public static double ToClientLength(int outerPixels, int framePixels, double scaling)
        => Math.Max(0, (outerPixels - Math.Max(0, framePixels)) / DpiScaler.NormalizeScaling(scaling));
}

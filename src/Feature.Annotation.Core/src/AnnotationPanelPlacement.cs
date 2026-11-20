using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネルの位置を決める（純粋計算。 単一所有）。</summary>
/// <remarks>
/// <b>座標の単位は物理ピクセル</b>: OS のポインター位置もウィンドウの位置も物理ピクセルで扱う
/// （DPI の倍率を掛けたり割ったりするのは各 UI の 1 行変換に閉じ込める）。<br/>
/// <b>アプリのウィンドウの内側へ収めない</b>: 収めると狭いウィンドウでパネルが潰れる、または縁で切れる。
/// 画面の外へは出さない（OS が画面内へ寄せるため、ここでは何もしない）。<br/>
/// <b>つまみが無くても動かせる</b>: パネルの余白（見出しの帯）を押したまま動かす。 位置の計算を
/// ここへ集約しないと、UI ごとに「1 回で止まる」（パネル内座標はウィンドウを動かしても変わらない）等の
/// 差が出る。
/// </remarks>
public static class AnnotationPanelPlacement
{
    /// <summary>アンカー（右クリックした位置）をパネルの左上にする。</summary>
    /// <param name="anchorScreen">アンカーの位置（物理ピクセル）。</param>
    /// <returns>パネルの左上（物理ピクセル）。</returns>
    public static EsPointInt ResolveInitial(EsPointInt anchorScreen) => anchorScreen;

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
}

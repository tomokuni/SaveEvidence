using Estable.FadeMask;

namespace Estable.FadeMaskDragRect;

/// <summary>動作モード（<see cref="FadeMaskDragRectMode"/>）から導出される状態を提供する静的クラス。</summary>
/// <remarks>
/// モードと状態（内包コントロールの表示可否・暗転マスクの描画モード）の対応を単一所有する（両 UI 実装が同じ判断を使う）。<br/>
/// 各 UI 実装は <see cref="Resolve"/> の結果を内包コントロールへ適用するだけで、モード判定の重複を持たない。
/// </remarks>
internal static class FadeMaskDragRectModes
{
    /// <summary>モードに対応する内包コントロールの状態を求める。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>表示可否（選択矩形・ルーペ・破線・全面暗転）と暗転マスクの描画モード。</returns>
    /// <remarks>
    /// 選択矩形を使うモード（Crop / AreaSelect）は、選択矩形・ルーペ・選択矩形の破線と全面暗転を有効にする。<br/>
    /// ScreenSelect / WindowSelect はハイライト表示のみのため、いずれも無効にする。
    /// </remarks>
    public static FadeMaskDragRectState Resolve(FadeMaskDragRectMode mode)
        => new(mode is FadeMaskDragRectMode.Crop or FadeMaskDragRectMode.AreaSelect, ToRenderMode(mode));

    /// <summary>モードに対応する暗転マスクの描画モードを取得する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>暗転マスクの描画モード。</returns>
    private static FadeMaskRenderMode ToRenderMode(FadeMaskDragRectMode mode) => mode switch
    {
        FadeMaskDragRectMode.Crop => FadeMaskRenderMode.Crop,
        FadeMaskDragRectMode.AreaSelect => FadeMaskRenderMode.AreaSelect,
        FadeMaskDragRectMode.ScreenSelect => FadeMaskRenderMode.ScreenSelect,
        _ => FadeMaskRenderMode.WindowSelect,
    };
}

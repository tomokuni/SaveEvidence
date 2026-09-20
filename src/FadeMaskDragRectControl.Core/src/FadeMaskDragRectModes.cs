using Estable.FadeMask;

namespace Estable.FadeMaskDragRect;

/// <summary>動作モード（<see cref="FadeMaskDragRectMode"/>）から導出される描画設定を提供する静的クラス。</summary>
/// <remarks>
/// モードと描画設定の対応を単一所有する（両 UI 実装が同じ判断を使う）。<br/>
/// 各 UI 実装は本クラスの結果を内包コントロールへ適用するだけで、モード判定の重複を持たない。
/// </remarks>
internal static class FadeMaskDragRectModes
{
    /// <summary>モードに対応する暗転マスクの描画モードを取得する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>暗転マスクの描画モード。</returns>
    public static FadeMaskRenderMode ToRenderMode(FadeMaskDragRectMode mode) => mode switch
    {
        FadeMaskDragRectMode.Crop => FadeMaskRenderMode.Crop,
        FadeMaskDragRectMode.AreaSelect => FadeMaskRenderMode.AreaSelect,
        FadeMaskDragRectMode.ScreenSelect => FadeMaskRenderMode.ScreenSelect,
        _ => FadeMaskRenderMode.WindowSelect,
    };

    /// <summary>モードが選択矩形とルーペを使用するかどうかを判定する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>選択矩形とルーペを使用する場合は true（Crop / AreaSelect）。</returns>
    /// <remarks>
    /// 全面暗転の要否も本判定と一致する（選択矩形を使うモードでのみ全面を暗転する）。<br/>
    /// ScreenSelect / WindowSelect はハイライト表示のみのため、全面暗転しない。
    /// </remarks>
    public static bool UsesDragRect(FadeMaskDragRectMode mode)
        => mode is FadeMaskDragRectMode.Crop or FadeMaskDragRectMode.AreaSelect;
}

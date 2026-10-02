namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>動作モード（<see cref="FadeMaskDragRectMode"/>）から導出される状態を提供する静的クラス。</summary>
/// <remarks>
/// モードと状態（内包コントロールの表示可否）の対応を単一所有する（両 UI 実装が同じ判断を使う）。<br/>
/// 各 UI 実装は <see cref="Resolve"/> の結果を内包コントロールへ適用するだけで、モード判定の重複を持たない。<br/>
/// ラベル文言（操作ラベル・情報ラベル）は本クラスに含めない（ホストが与える）。
/// </remarks>
internal static class FadeMaskDragRectModes
{
    /// <summary>モードに対応する内包コントロールの状態を求める。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>表示可否（選択矩形・ルーペ・破線・全面暗転）。</returns>
    /// <remarks>
    /// 選択矩形を使うモード（Crop / AreaSelect）は、選択矩形・ルーペ・選択矩形の破線と全面暗転を有効にする。<br/>
    /// ScreenSelect / WindowSelect はハイライト表示のみのため、いずれも無効にする。
    /// </remarks>
    public static FadeMaskDragRectState Resolve(FadeMaskDragRectMode mode)
        => new(mode is FadeMaskDragRectMode.Crop or FadeMaskDragRectMode.AreaSelect);
}

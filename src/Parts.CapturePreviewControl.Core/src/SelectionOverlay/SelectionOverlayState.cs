namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;

/// <summary>動作モード（<see cref="SelectionOverlayMode"/>）から導出される内包コントロールの状態。</summary>
/// <param name="UsesSelection">選択矩形・ルーペ・選択矩形の破線を使用するモードかどうか（Crop / AreaSelect）。</param>
/// <remarks>
/// <b>モードと状態の対応を単一所有する</b>。 UI 層は本値の各メンバーを内包コントロールへ代入するだけで、<br/>
/// モードごとの可否を自前で判定しない（両 UI の挙動が構造的に一致する）。 生成は <see cref="Resolve"/> から行う<br/>
/// （既定値 <c>default</c> は「選択矩形を使わない」になるため、既定値のまま使わない）。<br/>
/// 描画モードや文言は本値に含めない（描画の内容は装飾コアが、文言はホストが与える）。
/// </remarks>
internal readonly record struct SelectionOverlayState(bool UsesSelection)
{
    /// <summary>モードに対応する内包コントロールの状態を求める。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>表示可否（選択矩形・ルーペ・破線・全面暗転）。</returns>
    /// <remarks>
    /// 選択矩形を使うモード（Crop / AreaSelect）は、選択矩形・ルーペ・選択矩形の破線と全面暗転を有効にする。<br/>
    /// ScreenSelect / WindowSelect はハイライト表示のみのため、いずれも無効にする。
    /// </remarks>
    public static SelectionOverlayState Resolve(SelectionOverlayMode mode)
        => new(mode is SelectionOverlayMode.Crop or SelectionOverlayMode.AreaSelect);

    /// <summary>全面暗転（注目なし）を表示するかどうかを取得する。</summary>
    /// <value>選択矩形を使うモードでは true（切り出し・領域選択は全面を暗転して穴を開ける）。</value>
    /// <remarks>選択矩形を使わないモード（ScreenSelect / WindowSelect）はハイライトのみを表示するため全面暗転しない。</remarks>
    public bool ShowFullOverlay => UsesSelection;

    /// <summary>ルーペ画像上へ選択矩形の破線オーバーレイを描画するかどうかを取得する。</summary>
    /// <value>選択矩形を使うモードでは true。</value>
    public bool ShowSelectionOverlay => UsesSelection;

    /// <summary>ルーペを表示するかどうかを判定する。</summary>
    /// <param name="hasSource">拡大元画像が設定されているかどうか。</param>
    /// <param name="hasCenter">ルーペ中心（拡大中心）が設定されているかどうか。</param>
    /// <returns>表示する場合は true。</returns>
    /// <remarks>
    /// 拡大元画像が未設定の間は表示しない（未設定の市松模様を見せない）。<br/>
    /// 拡大中心が未設定の間も表示しない（配置先が決まる前に要素を出さない。 表示位置は中心から計算する）。<br/>
    /// 選択矩形を使わないモードでは、画像と中心が設定されていても表示しない。
    /// </remarks>
    public bool IsLoupeVisible(bool hasSource, bool hasCenter) => UsesSelection && hasSource && hasCenter;
}

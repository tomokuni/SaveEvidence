using Estable.DragRect.Decoration;

namespace Estable.FadeMaskDragRect;

/// <summary>動作モードから導出される内包コントロールの状態（表示の可否と暗転マスクの描画モード）。</summary>
/// <param name="UsesSelection">選択矩形・ルーペ・選択矩形の破線を使用するモードかどうか（Crop / AreaSelect）。</param>
/// <param name="RenderMode">暗転マスクの描画モード。</param>
/// <remarks>
/// 「モードから何が有効になるか」の判断を本値が単一所有する。UI 層は本値の各メンバーを内包コントロールへ<br/>
/// 代入するだけで、モードごとの可否を自前で判定しない（両 UI の挙動が構造的に一致する）。<br/>
/// 既定値（<c>default</c>）は「選択矩形を使わない・暗転マスクは WindowSelect」になるため、<br/>
/// 既定値のまま使うことを避ける目的で生成は <see cref="FadeMaskDragRectModes.Resolve"/> から行う。
/// </remarks>
internal readonly record struct FadeMaskDragRectState(bool UsesSelection, OuterDecoRenderMode RenderMode)
{
    /// <summary>全面暗転（穴あけなし）を表示するかどうかを取得する。</summary>
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
    /// 拡大中心が未設定の間も表示しない（配置先が決まる前に要素を出さない。表示位置は中心から計算する）。<br/>
    /// 選択矩形を使わないモードでは、画像と中心が設定されていても表示しない。
    /// </remarks>
    public bool IsLoupeVisible(bool hasSource, bool hasCenter) => UsesSelection && hasSource && hasCenter;
}

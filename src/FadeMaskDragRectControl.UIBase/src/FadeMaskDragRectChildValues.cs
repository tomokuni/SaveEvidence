using EsUtil.Feature.Marquee.Core.Setting;
using EsUtil.Feature.Loupe.Core.Setting;
using EsUtil.Feature.Marquee.Presenter.Setting;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.UIBase;

/// <summary>複合コントロールが内包コントロールへ適用する値（設定とモードから解決した結果）。</summary>
/// <param name="Marquee">選択矩形コントロールの設定（モードから導出するハンドルの表示可否を反映済み）。</param>
/// <param name="MarqueeAppearance">選択矩形の外観（モードから導出するハンドルの表示可否と全面暗転の可否を反映済み）。</param>
/// <param name="Loupe">ルーペの設定。</param>
/// <param name="LoupeOffset">ルーペのカーソル中心からの右下オフセット（DIP）。</param>
/// <param name="IsHitTestVisible">選択矩形コントロールが入力を受け取るかどうか（選択矩形を使うモードのみ true）。</param>
/// <remarks>
/// <b>なぜ本層が持つか</b>: 「設定とモードから子コントロールの値を決める」手順は UI フレームワークに依存しないが、<br/>
/// 結果を代入するには子コントロール（UI 固有型）が必要なため、UI 層には代入だけを残す。<br/>
/// 本型が値の決定を単一所有することで、片方の UI だけ分配規則が変わる余地をなくす。<br/>
/// <br/>
/// <b>ハイライト矩形は実行時状態</b>（設定値に含まれない）のため本値には含めず、子コントロールが保持する。<br/>
/// <br/>
/// <b>値の型は UI 非依存</b>（コアの設定型のみ）であり、UI 層はそのまま子コントロールへ代入する。
/// </remarks>
public readonly record struct FadeMaskDragRectChildValues(
    MarqueeOptions Marquee,
    MarqueeAppearance MarqueeAppearance,
    LoupeOptions Loupe,
    double LoupeOffset,
    bool IsHitTestVisible)
{
    /// <summary>設定とモードから内包コントロールへ適用する値を解決する。</summary>
    /// <param name="options">設定の単一値。</param>
    /// <param name="mode">動作モード。</param>
    /// <param name="marqueeAppearance">選択矩形の外観（ホストが与えた現在値。 モードから導出する項目だけを上書きする）。 null 不可。</param>
    /// <returns>内包コントロールへ適用する値。</returns>
    /// <remarks>
    /// 1. モードから表示可否を解決する（判断はコアの <c>FadeMaskDragRectModes</c> が単一所有する）。<br/>
    /// 2. 設定の値を写し、モードから導出する項目（ハンドルの表示可否・全面暗転の可否・入力の可否）だけを上書きする。<br/>
    /// 3. ハイライト矩形は実行時状態のため本値に含めない（子コントロールが現在値を持ち続ける）。<br/>
    /// 4. 選択矩形を使わないモード（ハイライトのみ）では入力を受け取らない（描画は止めない）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="marqueeAppearance"/> が null の場合。</exception>
    public static FadeMaskDragRectChildValues Resolve(
        FadeMaskDragRectOptions options, FadeMaskDragRectMode mode, MarqueeAppearance marqueeAppearance)
    {
        ArgumentNullException.ThrowIfNull(marqueeAppearance);

        var state = FadeMaskDragRectModes.Resolve(mode);

        return new FadeMaskDragRectChildValues(
            options.Marquee,
            marqueeAppearance with
            {
                ShowHandles = state.UsesSelection,
                ShowFullOverlay = state.ShowFullOverlay,
            },
            options.Loupe,
            options.LoupeOffset,
            state.UsesSelection);
    }
}

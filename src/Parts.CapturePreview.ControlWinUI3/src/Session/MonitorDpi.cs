using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;

/// <summary>モニターごとの DPI（表示倍率）を解決する。</summary>
/// <remarks>
/// <b>位置付け</b>: WinUI 3 の公開 API はウィンドウの表示倍率（<c>XamlRoot.RasterizationScale</c>）までしか与えず、
/// 仮想スクリーン全体を覆う 1 枚のウィンドウからはモニターごとの実倍率を特定できない。
/// サイズラベルの DisplayScale（モニターの実表示スケール）を求めるために本クラスを使う。<br/>
/// <b>照会そのものはコアが単一所有する</b>（<see cref="WinMonitorDpi.GetEffectiveDpi"/>。 4 つの UI 版が同じ値を使う）。
/// 本クラスは WinUI 3 側の契約（<c>uint</c> の DPI・失敗時は 96）へ合わせるだけを持つ。<br/>
/// <b>失敗時</b>: 照会できない場合は基準の DPI（96 = 100%）を返す（例外にしない。 表示倍率は診断用の補正値である）。
/// </remarks>
internal static class MonitorDpi
{
    /// <summary>基準の DPI（96 = 表示倍率 100%）。</summary>
    private const uint StandardDpi = WinMonitorDpi.StandardDpi;

    /// <summary>指定位置を含むモニターの有効 DPI を返す。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>モニターの有効 DPI。 照会できない場合は 96。</returns>
    /// <remarks>最も近いモニターを対象にする（ウィンドウが跨っていても座標から一意に決まる）。</remarks>
    public static uint ForPoint(EsPointInt screenPoint)
        => WinMonitorDpi.GetEffectiveDpi(screenPoint) ?? StandardDpi;
}

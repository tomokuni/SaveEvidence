namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>メイン ウィンドウの寸法（4 つの UI 版で共通）。</summary>
/// <remarks>
/// 最小サイズと既定サイズを 1 か所で決める（UI 版ごとに値がずれないようにする）。<br/>
/// 値は <b>DIP</b> で持つ。 WinUI 3 の <c>OverlappedPresenter.PreferredMinimumWidth</c> と<br/>
/// WinForms の <c>Form.MinimumSize</c> は画面物理ピクセルで解釈されるため、各 UI 版は<br/>
/// <see cref="WindowPlacementRule.ToPhysicalLength"/> で換算して指定する。<br/>
/// <br/>
/// <b>ナビゲーション パネルをオーバーレイ表示にする前提の幅</b>である: ページ領域はナビゲーション パネルの<br/>
/// 収納時の幅（<see cref="NavPanelMetrics.CompactWidth"/>）だけ左へ寄せたまま動かないため、<br/>
/// 展開時の幅（<see cref="NavPanelMetrics.ExpandedWidth"/>）を足した幅を最小にすると過剰になる。
/// </remarks>
public static class WindowMetrics
{
    /// <summary>ウィンドウの最小幅（DIP）。</summary>
    public const double MinimumWidth = 620;

    /// <summary>ウィンドウの最小の高さ（DIP）。</summary>
    public const double MinimumHeight = 400;

    /// <summary>復元する配置が無いときの既定の幅（DIP）。</summary>
    public const double DefaultWidth = 620;

    /// <summary>復元する配置が無いときの既定の高さ（DIP）。</summary>
    public const double DefaultHeight = 400;
}
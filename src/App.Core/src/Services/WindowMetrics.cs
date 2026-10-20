namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>メイン ウィンドウの寸法（4 つの UI 版で共通）。</summary>
/// <remarks>
/// 最小サイズと既定サイズを 1 か所で決める（UI 版ごとに値がずれないようにする）。<br/>
/// 最小・既定サイズはウィンドウ枠を含む外寸で、値は <b>DIP</b> で持つ。 WinUI 3 の<br/>
/// <c>OverlappedPresenter.PreferredMinimumWidth</c> と WinForms の <c>Form.MinimumSize</c> は画面物理ピクセルで<br/>
/// 解釈されるため、各 UI 版は <see cref="WindowPlacementRule.ToPhysicalLength"/> で換算して指定する。<br/>
/// クライアント領域を制約する UI 版は、実際のウィンドウ枠を測定して最小クライアント寸法へ換算する。<br/>
/// <br/>
/// <b>ナビゲーション パネルをオーバーレイ表示にする前提の幅</b>である: ページ領域はナビゲーション パネルの<br/>
/// 収納時の幅（<see cref="NavPanelMetrics.CompactWidth"/>）だけ左へ寄せたまま動かないため、<br/>
/// 展開時の幅（<see cref="NavPanelMetrics.ExpandedWidth"/>）を足した幅を最小にすると過剰になる。
/// </remarks>
public static class WindowMetrics
{
    /// <summary>ウィンドウの外寸の最小幅（DIP）。</summary>
    public const double MinimumWidth = 640;

    /// <summary>ウィンドウの外寸の最小高さ（DIP）。</summary>
    public const double MinimumHeight = 400;

    /// <summary>復元する配置が無いときの既定の外寸幅（DIP）。</summary>
    public const double DefaultWidth = 1200;

    /// <summary>復元する配置が無いときの既定の外寸高さ（DIP）。</summary>
    public const double DefaultHeight = 800;
}

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ウィンドウの標準メニュー（左上のアイコンの押下で出す）の項目。</summary>
/// <remarks>
/// 4 つの UI 版が同じ項目・同じ並びでメニューを出すための識別子である。<br/>
/// 実行（移動・サイズ変更・最小化など）は UI フレームワークの操作のため、各 UI 版が担う。
/// </remarks>
public enum WindowMenuAction
{
    /// <summary>元のサイズに戻す（最大化中のみ有効）。</summary>
    Restore,

    /// <summary>移動（OS の移動モードへ入る）。</summary>
    Move,

    /// <summary>サイズ変更（OS のサイズ変更モードへ入る）。</summary>
    Size,

    /// <summary>最小化。</summary>
    Minimize,

    /// <summary>最大化（最大化中は無効）。</summary>
    Maximize,

    /// <summary>閉じる。</summary>
    Close,
}
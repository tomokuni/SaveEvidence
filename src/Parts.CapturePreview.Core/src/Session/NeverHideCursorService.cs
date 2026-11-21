using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;

/// <summary>ポインタを決して隠さないカーソル サービス（OS の表示状態を変えない）。</summary>
/// <remarks>
/// <b>ポインタを隠してはいけない</b>: 隠すと入力位置を見失い、ウィンドウを切り替えるまで戻らないことがある。
/// Win32 の <c>ShowCursor</c> は表示カウンタを増減するだけで、対応が崩れると復元できない
/// （実測 2026-11-21: 矢印キーで nudge しただけでカーソルが消えたままになった）。<br/>
/// <b>位置の操作は委ねる</b>: nudge のカーソル移動（<see cref="TrySetCursorScreenPoint"/>）と位置の取得は
/// 元の実装へそのまま渡し、<b>表示の切替だけを無効化する</b>。 これにより、マーキー（EsUtil）を
/// 変更せずに「隠さない」規則を 4 つの UI 版すべてへ適用できる。<br/>
/// 本型は <c>Parts.CapturePreview.Core</c> が単一所有する（UI ごとに実装すると片方だけ漏れる）。
/// </remarks>
/// <param name="inner">位置の取得と移動を委ねる実装（省略すると Windows の既定実装）。</param>
public sealed class NeverHideCursorService(ICursorService? inner = null) : ICursorService
{
    /// <summary>位置の取得と移動を委ねる実装。</summary>
    private readonly ICursorService _inner = inner ?? new WinCursorService();

    /// <inheritdoc/>
    public bool TryGetCursorScreenPoint(out EsPointInt point)
        => _inner.TryGetCursorScreenPoint(out point);

    /// <inheritdoc/>
    public bool TrySetCursorScreenPoint(EsPointInt point)
        => _inner.TrySetCursorScreenPoint(point);

    /// <summary>表示の切替を行わない（ポインタは常に表示したままにする）。</summary>
    /// <param name="show">要求された表示状態（無視する）。</param>
    public void ShowCursor(bool show) => _ = show;
}

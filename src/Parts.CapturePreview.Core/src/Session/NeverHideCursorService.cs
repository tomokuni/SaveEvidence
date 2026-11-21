using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;

/// <summary>ポインタを動かさず隠さないカーソル サービス（OS のカーソル状態を変えない）。</summary>
/// <remarks>
/// <b>ポインタを隠してはいけない</b>: 隠すと入力位置を見失い、ウィンドウを切り替えるまで戻らないことがある。
/// Win32 の <c>ShowCursor</c> は表示カウンタを増減するだけで、対応が崩れると復元できない
/// （実測 2026-11-21: 矢印キーで nudge しただけでカーソルが消えたままになった）。<br/>
/// <b>ポインタを動かしてもいけない</b>: nudge のたびに実カーソルを動かすと、注釈の編集中に
/// キャレットを移動しただけでポインタが少しずつ動く（実測 2026-11-21）。<br/>
/// 位置の取得だけを元の実装へ委ね、<b>移動と表示の切替は行わない</b>。 これにより、マーキー（EsUtil）を
/// 変更せずに「カーソル状態を変えない」規則を 4 つの UI 版すべてへ適用できる。<br/>
/// 本型は <c>Parts.CapturePreview.Core</c> が単一所有する（UI ごとに実装すると片方だけ漏れる）。
/// </remarks>
/// <param name="inner">位置の取得を委ねる実装（省略すると Windows の既定実装）。</param>
public sealed class NeverHideCursorService(ICursorService? inner = null) : ICursorService
{
    /// <summary>位置の取得を委ねる実装。</summary>
    private readonly ICursorService _inner = inner ?? new WinCursorService();

    /// <inheritdoc/>
    public bool TryGetCursorScreenPoint(out EsPointInt point)
        => _inner.TryGetCursorScreenPoint(out point);

    /// <summary>位置の取得は委ね、移動と表示の切替は行わない。</summary>
    /// <param name="point">移動先（無視する）。</param>
    /// <returns>常に <see langword="false"/>（移動していない）。</returns>
    /// <remarks>
    /// <b>ポインタを動かさない</b>: nudge のたびに実カーソルを動かすと、注釈の編集中にキャレットを
    /// 移動しただけでポインタが少しずつ動く（実測 2026-11-21）。 カーソルは利用者の操作にのみ従う。
    /// </remarks>
    public bool TrySetCursorScreenPoint(EsPointInt point)
    {
        _ = point;
        return false;
    }

    /// <summary>表示の切替を行わない（ポインタは常に表示したままにする）。</summary>
    /// <param name="show">要求された表示状態（無視する）。</param>
    public void ShowCursor(bool show) => _ = show;
}

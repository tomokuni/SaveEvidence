namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズームセッションから UI 層へ返す効果（UI が適用する操作）。</summary>
/// <remarks>
/// 「コアが判断し、UI が適用する」を構造化するための値。<br/>
/// 既定はすべて false（何もしない）であり、UI は <see cref="ApplyVisual"/> / <see cref="ApplyPointer"/> を呼ぶだけでよい<br/>
/// （呼び出し側で「どの順に何を適用するか」を判断しない）。<br/>
/// ホットパス（ポインタ移動）で確保を避けるため <c>readonly record struct</c> とし、<c>with</c> 式で組み立てる。<br/>
/// <b>通知（イベント）は本値に含めない</b>: 通知はセッションの <see cref="ZoomViewSession.Subscribe"/>（<see cref="ZoomNotice"/>）が担い、<br/>
/// 本値は UI 側の操作（再描画・ポインタキャプチャ）に限定する（MarqueeControl と同じ分担）。
/// </remarks>
public readonly record struct ZoomFeedback
{
    /// <summary>表示の再描画が必要かどうかを取得する。</summary>
    /// <value>状態が変化して描き直しが必要な場合は true。</value>
    public bool ShouldRedraw { get; init; }

    /// <summary>ポインタをキャプチャ（ドラッグ追跡）するかどうかを取得する。</summary>
    /// <value>パン開始などでポインタを捕捉する場合は true。</value>
    /// <remarks>ポインタを暗黙にキャプチャする UI では無視してよい（使用しない場合は常に false として扱える）。</remarks>
    public bool ShouldCapturePointer { get; init; }

    /// <summary>ポインタのキャプチャを解放するかどうかを取得する。</summary>
    /// <value>パン終了などでポインタを解放する場合は true。</value>
    public bool ShouldReleasePointer { get; init; }

    /// <summary>表示に関わる効果（再描画）を適用する。</summary>
    /// <param name="redraw">再描画するコールバック。</param>
    /// <remarks>再描画の要否を UI 層で判定させないための入口（本値が唯一の判断元）。</remarks>
    public void ApplyVisual(Action redraw)
    {
        if (ShouldRedraw)
        {
            redraw();
        }
    }

    /// <summary>ポインタのキャプチャ／解放を適用する。</summary>
    /// <param name="capture">ポインタをキャプチャするコールバック。</param>
    /// <param name="release">ポインタを解放するコールバック。</param>
    /// <remarks>キャプチャと解放が同時に要求されることはない（順序の判断が不要になるよう本メソッドが一方だけを呼ぶ）。</remarks>
    public void ApplyPointer(Action capture, Action release)
    {
        if (ShouldCapturePointer)
        {
            capture();
        }

        if (ShouldReleasePointer)
        {
            release();
        }
    }
}

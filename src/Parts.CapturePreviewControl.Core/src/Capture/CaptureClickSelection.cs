using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

/// <summary>クリック確定（押下で固定・解放で確定）の状態と判断を単一所有する。</summary>
/// <remarks>
/// スクリーン選択・ウィンドウ選択は「押下時にカーソル下の要素を固定し、解放時にその要素で確定する」操作になる。<br/>
/// この状態（固定した矩形）と判断（退化した検出は固定しない・固定が無ければ確定しない・固定は 1 回限り）は<br/>
/// UI フレームワークに依存しないため、本クラスが単一所有する。<br/>
/// <br/>
/// <b>規則</b>:<br/>
/// 1. <see cref="Fix"/> は押下時に矩形を固定し、押下のたびに置き換える（前回の固定を残さない）。<br/>
///    退化した矩形（幅・高さが 0 以下）は固定せず、前回の固定も破棄する（古い矩形で確定しない）。<br/>
/// 2. <see cref="Take"/> は解放時に固定した矩形を取り出す。 固定が無い場合は null（確定しない）。<br/>
/// 3. 固定は <see cref="Take"/> で消費される（同じ押下で 2 回確定しない）。<br/>
/// <br/>
/// UI 層は「押下で <see cref="Fix"/>・解放で <see cref="Take"/>」を呼ぶだけにし、退化の判定と消費の管理を持たない。
/// </remarks>
public sealed class CaptureClickSelection
{
    /// <summary>押下時に固定した矩形（画面物理ピクセル。 未固定の場合は null）。</summary>
    private EsRectInt? _fixed;

    /// <summary>押下時に確定対象を固定する。</summary>
    /// <param name="target">固定する矩形（画面物理ピクセル）。</param>
    /// <returns>固定した場合は true。 退化した矩形（幅・高さが 0 以下）の場合は false（固定しない）。</returns>
    /// <remarks>退化した矩形を固定すると、解放時に空の切り出しを確定することになるため受け付けない。</remarks>
    public bool Fix(EsRectInt target)
    {
        _fixed = target.IsEmpty ? null : target;
        return _fixed is not null;
    }

    /// <summary>解放時に固定した矩形を取り出す（取り出し後は未固定に戻る）。</summary>
    /// <returns>固定した矩形（画面物理ピクセル）。 固定が無い場合は null。</returns>
    /// <remarks>取り出しは 1 回限りとする（同じ押下で 2 回確定しない）。</remarks>
    public EsRectInt? Take()
    {
        var target = _fixed;
        _fixed = null;
        return target;
    }
}

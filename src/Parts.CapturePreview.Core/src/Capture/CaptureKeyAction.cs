
namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

/// <summary>キャプチャオーバーレイ（全画面ウィンドウ）のキー操作で実行する内容を表す。</summary>
/// <remarks>
/// キャプチャのオーバーレイは「Enter = 確定 / Escape = キャンセル」を共通の操作とする。<br/>
/// どのキーがどの操作かの判断は UI フレームワークに依存しない規則であるため <see cref="CaptureKeyActions.Resolve"/> が単一所有する。<br/>
/// UI 層は変換したキーと確定対象の有無を渡し、返された操作の効果を適用するだけにする。
/// </remarks>
public enum CaptureKeyAction
{
    /// <summary>何もしない（対象外のキー・確定対象が無い場合の Enter）。</summary>
    None,

    /// <summary>確定する（Enter / Return）。</summary>
    Confirm,

    /// <summary>キャンセルする（Escape）。</summary>
    Cancel,
}

/// <summary>キャプチャオーバーレイのキー操作の解釈を単一所有する。</summary>
/// <remarks>
/// <b>規則</b>:<br/>
/// 1. Enter（Return を含む）は確定とする。 ただし<b>確定対象が無い場合は何もしない</b><br/>
///    （何も検出していない状態の Enter でキャンセル扱いにしない）。<br/>
/// 2. Escape はキャンセルとする。<br/>
/// 3. その他のキーは何もしない。<br/>
/// <br/>
/// <b>Enter と Return は同一のキー</b>として扱う（UI フレームワークのキーは両者が同値のエイリアスのため、<br/>
/// 名前ベースの相互変換では Return が得られ、UI ごとに届く値が異なる）。<br/>
/// <br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class CaptureKeyActions
{
    /// <summary>押下されたキーから実行すべき操作を解決する。</summary>
    /// <param name="key">押下されたキー（UI 非依存のキー識別子）。</param>
    /// <param name="hasConfirmTarget">確定できる対象（ハイライト矩形・選択矩形）があるかどうか。</param>
    /// <returns>実行すべき操作。 対象外のキー・確定対象が無い場合の Enter は <see cref="CaptureKeyAction.None"/>。</returns>
    /// <remarks>
    /// 確定対象の有無を引数で受けるのは、「確定できるか」の判断がキャプチャ種別に依存するためである<br/>
    /// （ハイライト型は検出矩形、ドラッグ型は選択矩形。 判定は <see cref="CaptureTypeTraits.ResolveRect"/> が単一所有する）。
    /// </remarks>
    public static CaptureKeyAction Resolve(EsKey key, bool hasConfirmTarget) => key switch
    {
        EsKey.Escape => CaptureKeyAction.Cancel,
        EsKey.Enter or EsKey.Return => hasConfirmTarget ? CaptureKeyAction.Confirm : CaptureKeyAction.None,
        _ => CaptureKeyAction.None,
    };
}

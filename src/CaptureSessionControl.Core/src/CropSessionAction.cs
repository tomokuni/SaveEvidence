namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>切り出しセッションの操作（ツールバーの 1 ボタンで開始・確定・キャンセルを切り替える）を表す。</summary>
/// <remarks>
/// 切り出しは「1 つのボタン（キー）」で開始・確定・キャンセルを切り替えるため、現在の状態に応じて<br/>
/// 「何をするか」を決める必要がある。 この判断は UI に依存しない規則であるため <see cref="CropSessionActions.Resolve"/> が単一所有する。
/// </remarks>
public enum CropSessionAction
{
    /// <summary>切り出しを開始する。</summary>
    Start,

    /// <summary>選択済みの矩形で確定する。</summary>
    Confirm,

    /// <summary>切り出しをキャンセルする（選択が無い場合の確定操作）。</summary>
    Cancel,
}

/// <summary>切り出しセッションの操作内容の決定を単一所有する。</summary>
/// <remarks>
/// 1 つの操作（ボタン / キー）で「開始・確定・キャンセル」を切り替える規則は UI フレームワークに依存しないため、<br/>
/// 本クラスが単一所有する（2 つの UI が同じ規則を別々に書くと、片方だけ条件が変わる）。<br/>
/// UI 層は返された操作の効果を適用するだけにする。
/// </remarks>
public static class CropSessionActions
{
    /// <summary>現在の状態から実行すべき操作を解決する。</summary>
    /// <param name="isRunning">切り出しセッションが実行中かどうか。</param>
    /// <param name="hasSelection">確定できる選択矩形があるかどうか。</param>
    /// <returns>実行すべき操作（未実行なら開始、実行中かつ選択ありなら確定、それ以外はキャンセル）。</returns>
    /// <remarks>
    /// 1. 未実行の場合は開始する（同じ操作で切り出しを始められる）。<br/>
    /// 2. 実行中で選択がある場合は確定する。<br/>
    /// 3. 実行中で選択が無い場合はキャンセルする（空の切り出しを作らない）。
    /// </remarks>
    public static CropSessionAction Resolve(bool isRunning, bool hasSelection)
        => !isRunning ? CropSessionAction.Start
            : hasSelection ? CropSessionAction.Confirm
            : CropSessionAction.Cancel;
}

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>セッションの排他スロット（同時に 1 つだけ実行できるセッションの受付）を単一所有する。</summary>
/// <remarks>
/// セッションは「撮影」と「切り出し」の 2 種があり、<b>同時に実行できるのはどちらか 1 つ</b>である。<br/>
/// この排他規則（片方が実行中なら他方も開始できない）は UI 非依存であるため本クラスが単一所有する<br/>
/// （UI ごとに「フィールドが null かどうか」の判定を書くと、2 スロットの相互排他が片方だけ漏れる）。<br/>
/// <br/>
/// <b>役割の分担</b>: 1 セッション内の「実行中／完了済み」は <see cref="CaptureSessionState"/> が持ち、<br/>
/// 本クラスは「どのセッションが実行中か」という受け付け状態だけを持つ（責務が重ならない）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
/// <typeparam name="TCapture">撮影セッションの型。</typeparam>
/// <typeparam name="TCrop">切り出しセッションの型。</typeparam>
public sealed class CaptureSessionSlot<TCapture, TCrop>
    where TCapture : class
    where TCrop : class
{
    /// <summary>進行中の撮影セッション（未実行の場合は null）。</summary>
    private TCapture? _capture;

    /// <summary>進行中の切り出しセッション（未実行の場合は null）。</summary>
    private TCrop? _crop;

    /// <summary>撮影セッションを実行中かどうかを取得する。</summary>
    /// <value>撮影セッションが進行中の場合は true。</value>
    public bool HasCapture => _capture is not null;

    /// <summary>切り出しセッションを実行中かどうかを取得する。</summary>
    /// <value>切り出しセッションが進行中の場合は true。</value>
    public bool HasCrop => _crop is not null;

    /// <summary>いずれかのセッションを実行中かどうかを取得する。</summary>
    /// <value>撮影・切り出しのいずれかが進行中の場合は true。</value>
    /// <remarks>再入の判定は本プロパティを使う（2 つのフィールドを個別に判定しない）。</remarks>
    public bool IsBusy => HasCapture || HasCrop;

    /// <summary>進行中の切り出しセッションを取得する。</summary>
    /// <value>切り出しセッション。 未実行の場合は null。</value>
    public TCrop? Crop => _crop;

    /// <summary>撮影セッションの開始を受け付ける。</summary>
    /// <param name="session">開始する撮影セッション。</param>
    /// <returns>受け付けた場合は true。 既にセッションが実行中の場合は false（<paramref name="session"/> は保持しない）。</returns>
    /// <remarks>他のセッション（切り出し）が実行中の場合も受け付けない（同時実行は 1 つまで）。</remarks>
    public bool TryBeginCapture(TCapture session)
    {
        if (IsBusy)
        {
            return false;
        }

        _capture = session;
        return true;
    }

    /// <summary>撮影セッションの終了を記録する。</summary>
    /// <param name="session">終了する撮影セッション。</param>
    /// <remarks>別のセッションへ差し替わっている場合は何もしない（古いセッションの終了で新しい保持を消さない）。</remarks>
    public void EndCapture(TCapture session)
    {
        if (ReferenceEquals(_capture, session))
        {
            _capture = null;
        }
    }

    /// <summary>切り出しセッションの開始を受け付ける。</summary>
    /// <param name="session">開始する切り出しセッション。</param>
    /// <returns>受け付けた場合は true。 既にセッションが実行中の場合は false（<paramref name="session"/> は保持しない）。</returns>
    /// <remarks>他のセッション（撮影）が実行中の場合も受け付けない（同時実行は 1 つまで）。</remarks>
    public bool TryBeginCrop(TCrop session)
    {
        if (IsBusy)
        {
            return false;
        }

        _crop = session;
        return true;
    }

    /// <summary>切り出しセッションの終了を記録する。</summary>
    /// <param name="session">終了する切り出しセッション。</param>
    /// <remarks>別のセッションへ差し替わっている場合は何もしない（古いセッションの終了で新しい保持を消さない）。</remarks>
    public void EndCrop(TCrop session)
    {
        if (ReferenceEquals(_crop, session))
        {
            _crop = null;
        }
    }

    /// <summary>進行中のセッションの矩形選択の有無を取得する。</summary>
    /// <param name="hasSelection">切り出しセッションの選択の有無を返す関数（未実行の場合は呼ばれない）。</param>
    /// <returns>切り出しセッションが実行中で選択がある場合は true。 それ以外は false。</returns>
    /// <remarks>
    /// 「セッション未実行なら false」という既定値の解決を単一所有する（UI 層で <c>?.</c> と <c>??</c> を書かない）。<br/>
    /// 撮影セッションは選択の有無を公開しないため対象外である。
    /// </remarks>
    public bool HasCropSelection(Func<TCrop, bool> hasSelection)
    {
        ArgumentNullException.ThrowIfNull(hasSelection);
        return _crop is { } crop && hasSelection(crop);
    }

    /// <summary>進行中のセッションをすべてキャンセルする。</summary>
    /// <param name="cancelCapture">撮影セッションのキャンセル。 null の場合は撮影を対象外とする。</param>
    /// <param name="cancelCrop">切り出しセッションのキャンセル。 null の場合は切り出しを対象外とする。</param>
    /// <remarks>
    /// 「実行中のみキャンセルする」判定を単一所有する（UI 層で null 判定を書かない）。<br/>
    /// キャンセル後に保持を解放するのは呼び出し元の終了処理（<see cref="EndCapture"/> / <see cref="EndCrop"/>）である。
    /// </remarks>
    public void CancelAll(Action<TCapture>? cancelCapture, Action<TCrop>? cancelCrop)
    {
        if (_capture is { } capture)
        {
            cancelCapture?.Invoke(capture);
        }

        if (_crop is { } crop)
        {
            cancelCrop?.Invoke(crop);
        }
    }
}

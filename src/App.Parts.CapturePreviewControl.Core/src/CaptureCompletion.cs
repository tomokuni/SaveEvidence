namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>セッションの結果（確定・キャンセル）を 1 回だけ確定させる完了通知。</summary>
/// <typeparam name="TResult">セッションの結果の型。</typeparam>
/// <remarks>
/// 撮影セッションは「オーバーレイの選択確定」「非アクティブ化」「Escape」など複数の経路から完了し得るため、<br/>
/// 最初の 1 件だけを結果として採用する（<see cref="TaskCompletionSource{TResult}"/> の単一確定を型的に表現する）。<br/>
/// <br/>
/// 完了は非同期の継続（バックグラウンドで取得した画像の受け渡し等）からも設定されるため、<br/>
/// 継続を別スレッドで実行する（<see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>）。
/// </remarks>
public sealed class CaptureCompletion<TResult>
{
    /// <summary>完了通知の実体。</summary>
    private readonly TaskCompletionSource<TResult> _source =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>完了を待つタスクを取得する。</summary>
    /// <value>セッションの結果を返すタスク。</value>
    public Task<TResult> Task => _source.Task;

    /// <summary>セッションの結果を設定して完了させる。</summary>
    /// <param name="result">セッションの結果（キャンセルの場合は <c>default</c> 等）。</param>
    /// <returns>この呼び出しで完了した場合は true。 既に完了している場合は false。</returns>
    /// <remarks>最初の 1 件だけが有効になる（2 件目以降は無視される）。</remarks>
    public bool TrySetResult(TResult result) => _source.TrySetResult(result);
}

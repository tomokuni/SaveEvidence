namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>サムネイル生成の実行を優先度付きでスケジュールする（UI 非依存）。</summary>
/// <remarks>
/// <b>目的</b>: 一覧のサムネイル生成を「表示されていないときは負荷を小さく、表示されたら全力」で実行し、<br/>
/// 生成できたものから順に表示できるようにする（生成中は呼び出し側が代替画像を表示する）。<br/>
/// <br/>
/// <b>実行の順序</b>: <see cref="ThumbnailPriority"/> の小さい順に実行する。 すなわち
/// <b>一覧で見えている項目（その中では上方にあるもの）が先</b>、見えていない項目が後になる。
/// 見え方が変わった場合は <see cref="Reprioritize"/> で未実行の要求の優先度を変える。<br/>
/// <b>負荷の切り替え</b>: <see cref="IsActive"/> が false のときは<b>1 件ずつ</b>実行し、1 件ごとに
/// 既定 100 ms（<c>idleInterval</c>）だけ待つ（一覧を表示していない間は CPU・ストレージをほとんど使わない）。
/// true のときは<b>同時実行の上限（既定は CPU 数）まで並列</b>に実行し、待機を入れない。<br/>
/// <b>切り替えの即時性</b>: 非表示の待機中に <see cref="IsActive"/> が true になると、待機を小刻みに
/// 分割して見ているため<b>次の 1 件が直ちに始まる</b>（残りの待機を待たない）。<br/>
/// <b>失敗の扱い</b>: 1 件の失敗で全体を止めない。 例外は <c>onError</c> へ渡す（呼び出し側が記録する）。<br/>
/// <b>中断</b>: <see cref="Clear"/> で未実行の要求を捨て、実行中の要求へ渡したトークンをキャンセルする
/// （一覧を再構築したときに古い結果を反映しない）。<br/>
/// <b>スレッド</b>: <see cref="Enqueue(Func{CancellationToken, ValueTask})"/> /
/// <see cref="Reprioritize"/> は任意のスレッドから呼べる。 生成処理は背景スレッドで実行されるため、
/// <b>UI への反映は生成処理の中で UI スレッドへ marshal すること</b>。
/// </remarks>
internal sealed class ThumbnailScheduler : IDisposable
{
    /// <summary>非表示のときに 1 件ごとに入れる待機（既定）。</summary>
    /// <remarks>10 件/秒を上限の目安にする（一覧を表示していない間の負荷を小さく保つ）。</remarks>
    private static readonly TimeSpan s_defaultIdleInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>非表示の待機を分割して見る間隔。</summary>
    /// <remarks>表示へ切り替わったときに待機の残りを待たずに次を始めるため、小刻みに分割する。</remarks>
    private static readonly TimeSpan s_idleCheckInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>未実行の要求（優先度順に取り出す）。</summary>
    private readonly PriorityQueue<ThumbnailRequest, ThumbnailPriority> _queue =
        new(Comparer<ThumbnailPriority>.Create(ThumbnailPriority.Compare));

    /// <summary><see cref="_queue"/> の操作を直列化するロック。</summary>
    /// <remarks>要求の追加・取り出し・優先度の変更は別々のスレッドから起きるため排他が要る。</remarks>
    private readonly Lock _queueLock = new();

    /// <summary>要求が積まれたことを読み取りループへ伝える信号（要求 1 件につき 1 つ）。</summary>
    private readonly SemaphoreSlim _signal = new(0);

    private readonly Action<Exception> _onError;

    private readonly SemaphoreSlim _slots;

    private readonly TimeSpan _idleInterval;

    private readonly CancellationTokenSource _dispose = new();

    /// <summary>積んだ要求へ割り当てる連番（優先度の同じ要求を積んだ順に実行するため）。</summary>
    private int _sequence;

    private long _generationId;

    private CancellationTokenSource _generation = new();

    private int _pending;

    private int _active;

    /// <summary>スケジューラを生成し、処理ループを開始する。</summary>
    /// <param name="onError">生成処理が投げた例外を受け取る処理（呼び出し側が記録する）。</param>
    /// <param name="maxParallelism">表示中の同時実行数の上限。 0 以下は CPU 数。</param>
    /// <param name="idleInterval">非表示のときに 1 件ごとに入れる待機。 null は既定（100 ms）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="onError"/> が null の場合。</exception>
    /// <remarks>
    /// <b>例外を外へ出さない理由</b>: 1 件の生成失敗（破損画像・アクセス拒否など）で一覧の生成が止まると、
    /// 以降のサムネイルが永久に出なくなる。 失敗は記録して次へ進む。
    /// </remarks>
    public ThumbnailScheduler(Action<Exception> onError, int maxParallelism = 0, TimeSpan? idleInterval = null)
    {
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _idleInterval = idleInterval ?? s_defaultIdleInterval;
        _slots = new SemaphoreSlim(Math.Max(1, maxParallelism > 0 ? maxParallelism : Environment.ProcessorCount));
        _ = RunAsync(_dispose.Token);
    }

    /// <summary>対象の一覧が表示中かどうかを取得または設定する（true で全力、false で低負荷）。</summary>
    /// <value>表示中の場合は true。</value>
    public bool IsActive
    {
        get => Volatile.Read(ref _active) == 1;
        set => Volatile.Write(ref _active, value ? 1 : 0);
    }

    /// <summary>未実行の要求の件数を取得する。</summary>
    /// <value>待ち行列に積まれ、まだ実行を開始していない件数（実行中のものは含めない）。</value>
    public int PendingCount => Volatile.Read(ref _pending);

    /// <summary>サムネイルの生成を要求する（見えていない項目として積む）。</summary>
    /// <param name="work">生成処理。 渡されたトークンがキャンセルされた場合は結果を反映しないこと。</param>
    /// <returns>積んだ要求（<see cref="Reprioritize"/> で優先度を変えるために使う）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> が null の場合。</exception>
    /// <remarks>
    /// 積んだ順に実行する（一覧内の位置が分からない場合に使う。 分かる場合は
    /// <see cref="Enqueue(ThumbnailPriority, Func{CancellationToken, ValueTask})"/> へ位置を渡す）。
    /// </remarks>
    public ThumbnailRequest Enqueue(Func<CancellationToken, ValueTask> work)
        => Enqueue(ThumbnailPriority.Background(Interlocked.Increment(ref _sequence)), work);

    /// <summary>サムネイルの生成を要求する（優先度を指定して積む）。</summary>
    /// <param name="priority">優先度（小さいほど先に実行する）。</param>
    /// <param name="work">生成処理。 渡されたトークンがキャンセルされた場合は結果を反映しないこと。</param>
    /// <returns>積んだ要求（<see cref="Reprioritize"/> で優先度を変えるために使う）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> が null の場合。</exception>
    /// <remarks>
    /// <b>結果の反映は <paramref name="work"/> の中で行う</b>（生成した値の受け渡しを型にせず、
    /// 呼び出し側が対象のアイテムを知っている状態で閉じ込める）。 UI へ反映する場合は
    /// <paramref name="work"/> の中で UI スレッドへ marshal すること。
    /// </remarks>
    public ThumbnailRequest Enqueue(ThumbnailPriority priority, Func<CancellationToken, ValueTask> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        var request = new ThumbnailRequest(Volatile.Read(ref _generationId), work, priority, _generation.Token);
        lock (_queueLock)
        {
            if (_dispose.IsCancellationRequested)
            {
                return request;
            }

            _queue.Enqueue(request, priority);
            request.IsQueued = true;
            Interlocked.Increment(ref _pending);
        }

        _signal.Release();
        return request;
    }

    /// <summary>未実行の要求の優先度を変える（一覧の見え方が変わったときに呼ぶ）。</summary>
    /// <param name="request">対象の要求。</param>
    /// <param name="priority">新しい優先度。</param>
    /// <returns>優先度を変えた場合は true（未実行でない・同じ優先度の場合は false）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> が null の場合。</exception>
    /// <remarks>
    /// <b>実行が始まった要求には効かない</b>（取り出した後は実行中であり、やり直しはしない）。
    /// <b>実行前なら、同時実行の空きを待っている間でも変えられる</b>（表示中は空きを先に確保してから取り出す）。
    /// 同じ優先度を渡した場合は何もしない（レイアウトのたびに呼んでも無駄な並べ替えをしない）。
    /// </remarks>
    public bool Reprioritize(ThumbnailRequest request, ThumbnailPriority priority)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_queueLock)
        {
            if (!request.IsQueued || request.Priority == priority)
            {
                return false;
            }

            if (!_queue.Remove(request, out _, out _))
            {
                // 既に取り出されている（実行中・実行済み）
                request.IsQueued = false;
                return false;
            }

            request.Priority = priority;
            _queue.Enqueue(request, priority);
            return true;
        }
    }

    /// <summary>未実行の要求を捨て、実行中の要求へ中断を伝える（一覧の再構築時に呼ぶ）。</summary>
    /// <remarks>
    /// <b>破棄しない理由</b>: 中断を伝えたトークンを実行中の処理が観測しているため
    /// （<see cref="CancellationTokenSource"/> の破棄はトークンの登録を無効にする）。 一覧の再構築時にだけ呼ぶ。<br/>
    /// 捨てた要求は <see cref="Reprioritize"/> を受け付けない（世代が変わっているため実行もされない）。
    /// </remarks>
    public void Clear()
    {
        Interlocked.Increment(ref _generationId);
        Interlocked.Exchange(ref _generation, new CancellationTokenSource()).Cancel();

        lock (_queueLock)
        {
            while (_queue.TryDequeue(out var drained, out _))
            {
                drained.IsQueued = false;
                Interlocked.Decrement(ref _pending);
            }

            // 捨てた要求の信号を取り除く（残すと読み取りループが空回りする）
            while (_signal.Wait(0))
            {
                // 意図的に空（信号を捨てる）
            }
        }
    }

    /// <summary>処理ループを止める（冪等）。</summary>
    /// <remarks>破棄後の <see cref="Enqueue(Func{CancellationToken, ValueTask})"/> は無視する。</remarks>
    public void Dispose()
    {
        if (_dispose.IsCancellationRequested)
        {
            return;
        }

        _dispose.Cancel();
    }

    /// <summary>優先度の高い順に 1 件取り出す。</summary>
    /// <param name="request">取り出した要求。</param>
    /// <returns>取り出せた場合は true（待ち行列が空の場合は false）。</returns>
    private bool TryDequeue(out ThumbnailRequest request)
    {
        lock (_queueLock)
        {
            if (_queue.TryDequeue(out var item, out _))
            {
                item.IsQueued = false;
                request = item;
                return true;
            }
        }

        request = null!;
        return false;
    }

    /// <summary>待ち行列から取り出し、表示状態に応じた並列度で実行する。</summary>
    /// <param name="token">破棄でキャンセルされるトークン。</param>
    /// <returns>ループが終わるまで完了しないタスク。</returns>
    /// <remarks>
    /// <b>表示中は空きを先に確保してから取り出す</b>: 取り出してから空きを待つと、待っている間の要求は
    /// 実行前にもかかわらず <see cref="Reprioritize"/> を受け付けなくなる（見え方の変化に追従できない）。
    /// </remarks>
    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // 表示状態はループの先頭で 1 度だけ読む（途中で変わっても 1 件の扱いは変えない）
                var active = IsActive;
                if (active)
                {
                    // 全力: 空きを先に確保する（取り出してから空きを待つと、待っている間は優先度を変えられない）
                    await _slots.WaitAsync(token).ConfigureAwait(false);
                }

                if (TryDequeue(out var item))
                {
                    Interlocked.Decrement(ref _pending);

                    if (item.Generation != Volatile.Read(ref _generationId))
                    {
                        // 世代が古い（Clear 済み）要求は実行しない
                        if (active)
                        {
                            _slots.Release();
                        }

                        continue;
                    }

                    if (active)
                    {
                        // 全力: 完了を待たずに次を開始する（同時実行の上限まで）
                        _ = ExecuteAsync(item, token);
                    }
                    else
                    {
                        // 低負荷: 1 件ずつ実行し、間に待機を入れる
                        await ExecuteAsync(item, token).ConfigureAwait(false);
                        await WaitIdleAsync(token).ConfigureAwait(false);
                    }

                    continue;
                }

                if (active)
                {
                    _slots.Release();
                }

                // 待ち行列が空: 次に積まれるまで待つ
                await _signal.WaitAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 破棄による終了（記録不要）
        }
    }

    /// <summary>表示へ切り替わるか破棄されるまで、非表示の待機を分割して待つ。</summary>
    /// <param name="token">破棄でキャンセルされるトークン。</param>
    /// <returns>待機が終わるまで完了しないタスク。</returns>
    private async Task WaitIdleAsync(CancellationToken token)
    {
        var remaining = _idleInterval;
        while (remaining > TimeSpan.Zero && !IsActive && !token.IsCancellationRequested)
        {
            var step = remaining < s_idleCheckInterval ? remaining : s_idleCheckInterval;
            await Task.Delay(step, token).ConfigureAwait(false);
            remaining -= step;
        }
    }

    /// <summary>1 件の要求を実行する（失敗は <c>onError</c> へ渡す）。</summary>
    /// <param name="item">実行する要求。</param>
    /// <param name="token">破棄でキャンセルされるトークン。</param>
    /// <returns>実行が終わるまで完了しないタスク。</returns>
    private async Task ExecuteAsync(ThumbnailRequest item, CancellationToken token)
    {
        try
        {
            // 世代のトークン（Clear でキャンセル）と破棄のトークンを合成して渡す
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, item.Stale);
            await item.Body(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 破棄・Clear による中断（記録不要）
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 1 件の失敗（破損画像・アクセス拒否など）で一覧の生成を止めない（記録は呼び出し側）
            _onError(ex);
        }
        finally
        {
            if (IsActive)
            {
                _slots.Release();
            }
        }
    }

    /// <summary>待ち行列の 1 件分の要求（呼び出し側が保持し、<see cref="Reprioritize"/> の識別子に使う）。</summary>
    /// <param name="generation">積んだ時点の世代（<see cref="Clear"/> で変わる）。</param>
    /// <param name="body">生成処理。</param>
    /// <param name="priority">初期の優先度。</param>
    /// <param name="stale">世代が変わったときにキャンセルされるトークン。</param>
    internal sealed class ThumbnailRequest(
        long generation, Func<CancellationToken, ValueTask> body, ThumbnailPriority priority, CancellationToken stale)
    {
        /// <summary>積んだ時点の世代。</summary>
        public long Generation { get; } = generation;

        /// <summary>生成処理。</summary>
        public Func<CancellationToken, ValueTask> Body { get; } = body;

        /// <summary>世代が変わったときにキャンセルされるトークン。</summary>
        public CancellationToken Stale { get; } = stale;

        /// <summary>現在の優先度。</summary>
        public ThumbnailPriority Priority { get; set; } = priority;

        /// <summary>待ち行列に積まれているかどうか（実行が始まった・捨てられた場合は false）。</summary>
        public bool IsQueued { get; set; }
    }
}

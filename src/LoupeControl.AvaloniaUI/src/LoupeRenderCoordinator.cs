using Avalonia.Threading;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペの再描画要求を Dispatcher へのポストでバッチ化する（1フレームに1回）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> の再描画オーケストレーション（複数プロパティ変更の多重再描画を防止するバッチ化）を<br/>
/// UI 状態（<see cref="Dispatcher.UIThread"/> へのポスト・表示状態・ビットマップ解放・再生成）から分離する。<br/>
/// 表示状態・ビットマップ解放・再生成はコールバックで受け取り、UI コントロールに依存しない。<br/>
/// Dispatcher へのポストもコールバック（既定は <see cref="Dispatcher.UIThread"/>.Post）で注入するため、<br/>
/// ヘッドレスなしで <c>[Fact]</c> の並列テストが可能（テスト容易性・並列性向上）。
/// </remarks>
internal sealed class LoupeRenderCoordinator
{
    private readonly Func<bool> _isVisible;
    private readonly Action _releaseBitmap;
    private readonly Action<long> _startRender;
    private readonly Action<long> _logRenderCore;
    private readonly Action<Action> _post;

    /// <summary>再描画要求の結合フラグ（true の間は追加要求を無視して1回のポストにまとめる）。</summary>
    private bool _renderQueued;

    /// <summary>再描画の世代番号（古い世代のビットマップ破棄判定に使用）。</summary>
    private long _renderGeneration;

    /// <summary>再描画コーディネーターを初期化する。</summary>
    /// <param name="isVisible">現在表示中かどうかを取得するコールバック。</param>
    /// <param name="releaseBitmap">ビットマップを解放して表示をクリアするコールバック。</param>
    /// <param name="startRender">指定した世代番号で自動再描画を開始するコールバック。</param>
    /// <param name="logRenderCore">再描画コア実行の記録コールバック（出力の有無はロガー自身が判定する）。</param>
    /// <param name="post">実行キューへ処理を投入するコールバック（既定は <see cref="Dispatcher.UIThread"/>.Post）。テストでは同期実行を注入して並列化できる。</param>
    public LoupeRenderCoordinator(
        Func<bool> isVisible,
        Action releaseBitmap,
        Action<long> startRender,
        Action<long> logRenderCore,
        Action<Action>? post = null)
    {
        _isVisible = isVisible;
        _releaseBitmap = releaseBitmap;
        _startRender = startRender;
        _logRenderCore = logRenderCore;
        // 再描画要求（RenderCore）は描画フェーズ（Render 優先度）でポストする。
        // マウス移動中の連続再描画では、既定の Normal 優先度のポストが入力イベント（Input 優先度）に
        // 後回しにされ、描画反映が移動停止まで実行されない問題を防ぐ。
        _post = post ?? (action => Dispatcher.UIThread.Post(action, DispatcherPriority.Render));
    }

    /// <summary>現在の世代番号。</summary>
    public long Generation => _renderGeneration;

    /// <summary>指定した世代のビットマップを表示へ反映してよいか判定する。</summary>
    /// <param name="generation">生成したビットマップの世代番号。</param>
    /// <returns>表示中かつ指定世代が最新なら true（古い世代・非表示では破棄）。</returns>
    /// <remarks>
    /// <see cref="LoupeControl.GenerateAsync"/> の古い世代ビットマップ破棄判定を純粋ロジックとして集約する。<br/>
    /// 非表示中、または別世代のビットマップが生成済み（世代が進んでいる）場合は反映せず破棄する。
    /// </remarks>
    public bool ShouldApply(long generation)
        => _isVisible() && generation == _renderGeneration;

    /// <summary>未実行の再描画要求を破棄し、ビットマップを解放する（非表示・明示クリアで共用）。</summary>
    /// <remarks>
    /// 「要求を破棄して表示中の画像を解放する」手順を単一所有する。<br/>
    /// 非表示遷移と明示クリア（既に非表示でイベントが発火しない場合を含む）が本メソッドを呼ぶ。
    /// </remarks>
    public void CancelAndRelease()
    {
        CancelPending();
        _releaseBitmap();
    }

    /// <summary>再描画を要求する（複数要求は Dispatcher のポストにより1回に結合される）。</summary>
    /// <remarks>
    /// プロパティ変更、およびホストからの明示要求（<see cref="LoupeControl.RequestRender"/>）から呼ばれる。<br/>
    /// 非表示時は未実行要求を破棄しビットマップを解放する（描画は行わない）。
    /// </remarks>
    public void RequestRender()
    {
        if (!_isVisible())
        {
            Interlocked.Increment(ref _renderGeneration);
            _releaseBitmap();
            return;
        }

        if (_renderQueued)
        {
            return;
        }

        _renderQueued = true;
        _post(RenderCore);
    }

    /// <summary>ポストされた再描画を実行する。</summary>
    /// <remarks>
    /// 世代番号を採番してログを記録し、描画を開始する。<br/>
    /// ログ出力の有無（無効ロガー）はロガー自身（生成コードの <c>IsEnabled</c> 判定）が担うため、本メソッドは判断しない。
    /// </remarks>
    public void RenderCore()
    {
        if (!_renderQueued)
        {
            return;
        }

        _renderQueued = false;
        var generation = Interlocked.Increment(ref _renderGeneration);
        _logRenderCore(generation);
        _startRender(generation);
    }

    /// <summary>未実行の再描画要求をキャンセルする（非表示遷移時）。</summary>
    public void CancelPending()
    {
        _renderQueued = false;
    }
}

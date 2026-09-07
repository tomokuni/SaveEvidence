using Avalonia.Threading;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペの再描画要求を Dispatcher へのポストでバッチ化する（1フレームに1回）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> の再描画オーケストレーション（複数プロパティ変更の多重再描画を防止するバッチ化）を<br/>
/// UI 状態（<see cref="Dispatcher.UIThread"/> へのポスト・表示状態・ビットマップ解放・再生成）から分離する。<br/>
/// 表示状態・ビットマップ解放・再生成はコールバックで受け取り、UI コントロールに依存しない。<br/>
/// UI スレッドに依存するため <c>[AvaloniaFact]</c> で検証する。
/// </remarks>
internal sealed class LoupeRenderCoordinator
{
    private readonly Func<bool> _isVisible;
    private readonly Action _releaseBitmap;
    private readonly Action<long> _startRender;
    private readonly Action<long> _logRenderCore;
    private readonly Func<bool> _isLogEnabled;

    /// <summary>再描画要求の結合フラグ（true の間は追加要求を無視して1回のポストにまとめる）。</summary>
    private bool _renderQueued;

    /// <summary>再描画の世代番号（古い世代のビットマップ破棄判定に使用）。</summary>
    private long _renderGeneration;

    /// <summary>再描画コーディネーターを初期化する。</summary>
    /// <param name="isVisible">現在表示中かどうかを取得するコールバック。</param>
    /// <param name="releaseBitmap">ビットマップを解放して表示をクリアするコールバック。</param>
    /// <param name="startRender">指定した世代番号で自動再描画を開始するコールバック。</param>
    /// <param name="logRenderCore">再描画コア実行の記録コールバック。</param>
    /// <param name="isLogEnabled">ログを記録するかどうかを取得するコールバック（null ロガーでは false）。</param>
    public LoupeRenderCoordinator(
        Func<bool> isVisible,
        Action releaseBitmap,
        Action<long> startRender,
        Action<long> logRenderCore,
        Func<bool>? isLogEnabled = null)
    {
        _isVisible = isVisible;
        _releaseBitmap = releaseBitmap;
        _startRender = startRender;
        _logRenderCore = logRenderCore;
        _isLogEnabled = isLogEnabled ?? (() => true);
    }

    /// <summary>現在の世代番号。</summary>
    public long Generation => _renderGeneration;

    /// <summary>再描画要求の結合フラグ（テスト用の内部アクセサ）。</summary>
    internal bool IsQueued => _renderQueued;

    /// <summary>新しい世代番号を採番する（同期再描画用）。</summary>
    /// <returns>採番した世代番号。</returns>
    public long NextGeneration() => Interlocked.Increment(ref _renderGeneration);

    /// <summary>再描画を要求する（複数要求は Dispatcher のポストにより1回に結合される）。</summary>
    /// <remarks>
    /// プロパティ変更から呼ばれる。非表示時は未実行要求を破棄しビットマップを解放する。
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
        Dispatcher.UIThread.Post(RenderCore);
    }

    /// <summary>ポストされた再描画を実行する。</summary>
    public void RenderCore()
    {
        if (!_renderQueued)
        {
            return;
        }

        _renderQueued = false;
        var generation = Interlocked.Increment(ref _renderGeneration);
        if (_isLogEnabled())
        {
            _logRenderCore(generation);
        }
        _startRender(generation);
    }

    /// <summary>未実行の再描画要求をキャンセルする（非表示遷移時）。</summary>
    public void CancelPending()
    {
        _renderQueued = false;
    }
}

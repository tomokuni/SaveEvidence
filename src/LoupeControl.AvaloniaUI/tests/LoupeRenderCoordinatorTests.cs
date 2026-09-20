using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeRenderCoordinator"/> の単体テスト（再描画バッチ化・世代番号管理）。</summary>
/// <remarks>
/// Dispatcher へのポストはコンストラクタへ注入した同期実行コールバックに置き換えるため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// 表示状態・ビットマップ解放・再生成はコールバック（モック）で注入する。
/// </remarks>
public class LoupeRenderCoordinatorTests
{
    /// <summary>テスト用のコーディネーターを生成する（表示状態のみ注入）。</summary>
    /// <param name="isVisible">表示状態（モック）。</param>
    private static LoupeRenderCoordinator Create(bool isVisible)
        => new(
            isVisible: () => isVisible,
            releaseBitmap: () => { },
            startRender: _ => { },
            logRenderCore: _ => { },
            post: action => action()); // Dispatcher を同期実行に置き換え、並列テスト可能にする

    [Fact]
    public void ShouldApply_表示中かつ最新世代ならtrueを返す()
    {
        var coordinator = Create(isVisible: true);
        var generation = coordinator.NextGeneration();

        Assert.True(coordinator.ShouldApply(generation));
    }

    [Fact]
    public void ShouldApply_古い世代ならfalseを返す()
    {
        var coordinator = Create(isVisible: true);
        var old = coordinator.NextGeneration();
        coordinator.NextGeneration(); // 世代が進む

        Assert.False(coordinator.ShouldApply(old));
    }

    [Fact]
    public void ShouldApply_非表示ならfalseを返す()
    {
        var coordinator = Create(isVisible: false);
        var generation = coordinator.NextGeneration();

        Assert.False(coordinator.ShouldApply(generation));
    }

    [Fact]
    public void NextGeneration_世代番号が順に増える()
    {
        var coordinator = Create(isVisible: true);

        Assert.Equal(1, coordinator.NextGeneration());
        Assert.Equal(2, coordinator.NextGeneration());
    }

    [Fact]
    public void RequestRender_表示中は再描画を開始し世代が進む()
    {
        long? startGeneration = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: g => startGeneration = g,
            logRenderCore: _ => { },
            post: action => action());

        coordinator.RequestRender();

        Assert.NotNull(startGeneration);
        Assert.Equal(1, startGeneration.Value);
    }

    [Fact]
    public void RequestRender_複数要求は1回の再描画に結合される()
    {
        var startCount = 0;
        Action? pending = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => pending = action); // ポストを遅延実行し、バッチ化を検証する

        // 複数要求は結合フラグにより1つのポストにまとめられる（RenderCore は未実行）
        coordinator.RequestRender();
        coordinator.RequestRender();
        coordinator.RequestRender();
        pending!.Invoke(); // ポストされた RenderCore を 1 回だけ実行する

        Assert.Equal(1, startCount);
    }

    [Fact]
    public void RequestRender_非表示時はビットマップを解放し再描画しない()
    {
        var releaseCount = 0;
        var startCount = 0;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => false,
            releaseBitmap: () => releaseCount++,
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => action());

        coordinator.RequestRender();

        Assert.Equal(1, releaseCount);
        Assert.Equal(0, startCount);
    }

    [Fact]
    public void CancelPending_未実行の再描画要求を破棄する()
    {
        var startCount = 0;
        Action? pending = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => pending = action);

        // 再描画を要求（post で pending に保持）し、キャンセル後に RenderCore を実行しても再描画されない
        coordinator.RequestRender();
        coordinator.CancelPending();
        pending?.Invoke();

        Assert.Equal(0, startCount);
    }
}

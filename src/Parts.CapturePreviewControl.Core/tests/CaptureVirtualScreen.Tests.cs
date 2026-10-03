using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary><see cref="CaptureVirtualScreen"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「タイムアウトで打ち切る」「遅れて完了した未使用の結果を破棄する」という規則を固定する。<br/>
/// 実時間に依存するため、タイムアウトには短い値を渡して検証する。
/// </remarks>
public class CaptureVirtualScreenTests
{
    /// <summary>検証用の画像を生成する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>指定サイズの画像。</returns>
    private static EsBitmap CreateImage(int width, int height) => EsBitmap.Create(width, height);

    [Fact]
    public async Task AwaitAsync_取得が先に完了した場合はその結果を返す()
    {
        using var image = CreateImage(4, 3);
        var captureTask = Task.FromResult<EsBitmap?>(image);

        var result = await CaptureVirtualScreen.AwaitAsync(captureTask, TimeSpan.FromSeconds(5));

        Assert.Same(image, result);
    }

    [Fact]
    public async Task AwaitAsync_取得に失敗した場合はnullを返す()
    {
        // 取得失敗（null）とタイムアウトは呼び出し側で同じ扱い（代替の取得経路へ委ねる）
        var captureTask = Task.FromResult<EsBitmap?>(null);

        var result = await CaptureVirtualScreen.AwaitAsync(captureTask, TimeSpan.FromSeconds(5));

        Assert.Null(result);
    }

    [Fact]
    public async Task AwaitAsync_タイムアウトした場合はnullを返す()
    {
        var pending = new TaskCompletionSource<EsBitmap?>();

        var result = await CaptureVirtualScreen.AwaitAsync(pending.Task, TimeSpan.FromMilliseconds(20));

        Assert.Null(result);
        // 後始末（未完了のタスクを残さない）
        pending.SetResult(null);
    }

    [Fact]
    public async Task AwaitAsync_タイムアウト後に完了した結果は破棄する()
    {
        // 遅れて取得できても表示しないため、保持せず破棄する（メモリを占有しない）
        var pending = new TaskCompletionSource<EsBitmap?>();
        var image = CreateImage(8, 6);

        var result = await CaptureVirtualScreen.AwaitAsync(pending.Task, TimeSpan.FromMilliseconds(20));
        Assert.Null(result);

        pending.SetResult(image);
        // 破棄は継続（バックグラウンド）で行われるため、完了を待つ
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.True(image.IsDisposed);
    }

    [Fact]
    public async Task AwaitAsync_タイムアウト後の取得失敗は破棄しない()
    {
        // null（取得失敗）には破棄する対象がない（例外にならずに完了する）
        var pending = new TaskCompletionSource<EsBitmap?>();

        _ = await CaptureVirtualScreen.AwaitAsync(pending.Task, TimeSpan.FromMilliseconds(20));
        pending.SetResult(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AwaitAsync_タスクがnullの場合は例外を投げる()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CaptureVirtualScreen.AwaitAsync(null!));
    }
}

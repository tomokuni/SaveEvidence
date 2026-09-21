using Xunit;

namespace Estable.CaptureSession.Tests;

/// <summary><see cref="CaptureCompletion{TResult}"/> の単体テスト（単一確定と待機）。</summary>
public class CaptureCompletionTests
{
    [Fact]
    public async Task TrySetResult_最初の結果だけが採用される()
    {
        var completion = new CaptureCompletion<int>();

        Assert.True(completion.TrySetResult(10));
        Assert.False(completion.TrySetResult(20)); // 2 件目以降は無視される

        Assert.Equal(10, await completion.Task);
    }

    [Fact]
    public async Task Task_結果が設定されるまで完了しない()
    {
        var completion = new CaptureCompletion<string>();

        Assert.False(completion.Task.IsCompleted);

        completion.TrySetResult("done");

        Assert.Equal("done", await completion.Task);
    }

    [Fact]
    public async Task TrySetResult_参照型のnullも結果として扱える()
    {
        var completion = new CaptureCompletion<string?>();

        Assert.True(completion.TrySetResult(null));
        Assert.Null(await completion.Task);
    }
}

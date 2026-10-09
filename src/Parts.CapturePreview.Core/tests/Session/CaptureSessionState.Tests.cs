using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Session;

/// <summary><see cref="CaptureSessionState"/> の単体テスト（再入禁止・単一完了・後片付け）。</summary>
public class CaptureSessionStateTests
{
    [Fact]
    internal void TryBegin_最初の開始は成功する()
    {
        var state = new CaptureSessionState();

        Assert.True(state.TryBegin());
        Assert.True(state.IsRunning);
        Assert.False(state.IsCompleted);
    }

    [Fact]
    internal void TryBegin_実行中は再入を拒否する()
    {
        var state = new CaptureSessionState();
        state.TryBegin();

        Assert.False(state.TryBegin());
        Assert.True(state.IsRunning);
    }

    [Fact]
    internal void TryComplete_最初の完了だけを受け付ける()
    {
        var state = new CaptureSessionState();
        state.TryBegin();

        Assert.True(state.TryComplete());
        Assert.True(state.IsCompleted);

        // 2 回目（確定とキャンセルの競合等）は受け付けない
        Assert.False(state.TryComplete());
    }

    [Fact]
    internal void TryComplete_開始していない場合は受け付けない()
    {
        var state = new CaptureSessionState();

        Assert.False(state.TryComplete());
        Assert.False(state.IsCompleted);
    }

    [Fact]
    internal void End_実行中フラグを落として次のセッションを開始できる()
    {
        var state = new CaptureSessionState();
        state.TryBegin();
        state.TryComplete();

        state.End();

        Assert.False(state.IsRunning);
        Assert.False(state.IsCompleted);
        Assert.True(state.TryBegin());
    }

    [Fact]
    internal void End_完了していなくても呼べる()
    {
        var state = new CaptureSessionState();
        state.TryBegin();

        state.End();

        Assert.False(state.IsRunning);
        Assert.True(state.TryBegin());
    }
}


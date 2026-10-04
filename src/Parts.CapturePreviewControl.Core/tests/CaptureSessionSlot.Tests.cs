using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary><see cref="CaptureSessionSlot{TResult, TCrop}"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>「同時に実行できるのは 1 つだけ」という排他規則と、古いセッションの終了で保持を消さない規則を固定する。</remarks>
public class CaptureSessionSlotTests
{
    /// <summary>テスト用のセッション型（参照の同一性だけを検証する）。</summary>
    private sealed class TestSession;

    /// <summary>検証に使うスロットを生成する。</summary>
    /// <returns>空のスロット。</returns>
    private static CaptureSessionSlot<TestSession, TestSession> CreateSlot() => new();

    [Fact]
    internal void TryBeginCapture_空なら受け付ける()
    {
        var slot = CreateSlot();
        var session = new TestSession();

        Assert.True(slot.TryBeginCapture(session));
        Assert.True(slot.HasCapture);
        Assert.True(slot.IsBusy);
    }

    [Fact]
    internal void TryBeginCapture_撮影が実行中なら拒否する()
    {
        var slot = CreateSlot();
        slot.TryBeginCapture(new TestSession());

        Assert.False(slot.TryBeginCapture(new TestSession()));
    }

    [Fact]
    internal void TryBeginCapture_切り出しが実行中なら拒否する()
    {
        var slot = CreateSlot();
        slot.TryBeginCrop(new TestSession());

        Assert.False(slot.TryBeginCapture(new TestSession()));
        Assert.False(slot.HasCapture);
    }

    [Fact]
    internal void TryBeginCrop_空なら受け付ける()
    {
        var slot = CreateSlot();
        var session = new TestSession();

        Assert.True(slot.TryBeginCrop(session));
        Assert.True(slot.HasCrop);
        Assert.Same(session, slot.Crop);
    }

    [Fact]
    internal void TryBeginCrop_撮影が実行中なら拒否する()
    {
        var slot = CreateSlot();
        slot.TryBeginCapture(new TestSession());

        Assert.False(slot.TryBeginCrop(new TestSession()));
        Assert.False(slot.HasCrop);
    }

    [Fact]
    internal void EndCapture_保持を解放して次の開始を受け付ける()
    {
        var slot = CreateSlot();
        var first = new TestSession();
        slot.TryBeginCapture(first);

        slot.EndCapture(first);

        Assert.False(slot.HasCapture);
        Assert.False(slot.IsBusy);
        Assert.True(slot.TryBeginCapture(new TestSession()));
    }

    [Fact]
    internal void EndCapture_別のセッションなら保持を消さない()
    {
        var slot = CreateSlot();
        var current = new TestSession();
        slot.TryBeginCapture(current);

        slot.EndCapture(new TestSession());

        Assert.True(slot.HasCapture);
    }

    [Fact]
    internal void EndCrop_別のセッションなら保持を消さない()
    {
        var slot = CreateSlot();
        var current = new TestSession();
        slot.TryBeginCrop(current);

        slot.EndCrop(new TestSession());

        Assert.True(slot.HasCrop);
    }

    [Fact]
    internal void HasCropSelection_未実行ならfalseを返し関数を呼ばない()
    {
        var slot = CreateSlot();
        var called = false;

        var result = slot.HasCropSelection(_ =>
        {
            called = true;
            return true;
        });

        Assert.False(result);
        Assert.False(called);
    }

    [Fact]
    internal void HasCropSelection_実行中は関数の結果を返す()
    {
        var slot = CreateSlot();
        slot.TryBeginCrop(new TestSession());

        Assert.True(slot.HasCropSelection(_ => true));
        Assert.False(slot.HasCropSelection(_ => false));
    }

    [Fact]
    internal void HasCropSelection_関数がnullなら例外()
    {
        var slot = CreateSlot();

        Assert.Throws<ArgumentNullException>(() => slot.HasCropSelection(null!));
    }

    [Fact]
    internal void CancelAll_実行中のものだけキャンセルする()
    {
        var slot = CreateSlot();
        var capture = new TestSession();
        slot.TryBeginCapture(capture);
        var cancelledCapture = false;
        var cancelledCrop = false;

        slot.CancelAll(_ => cancelledCapture = true, _ => cancelledCrop = true);

        Assert.True(cancelledCapture);
        Assert.False(cancelledCrop);
    }

    [Fact]
    internal void CancelAll_未実行でも例外にならない()
    {
        var slot = CreateSlot();

        slot.CancelAll(null, null);

        Assert.False(slot.IsBusy);
    }
}

using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.SaveEvidence.CaptureSession.Tests;

/// <summary><see cref="CaptureClickSelection"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「押下で固定・解放で 1 回だけ取り出す」「退化した矩形は固定しない」という規則を固定する。
/// </remarks>
public class CaptureClickSelectionTests
{
    [Fact]
    public void Fix_押下で固定して解放で取り出す()
    {
        var selection = new CaptureClickSelection();

        Assert.True(selection.Fix(new EsRectInt(10, 20, 30, 40)));
        Assert.Equal(new EsRectInt(10, 20, 30, 40), selection.Take());
    }

    [Theory]
    [InlineData(0, 0, 0, 40)]
    [InlineData(0, 0, 30, 0)]
    [InlineData(0, 0, -30, -40)]
    public void Fix_退化した矩形は固定しない(int x, int y, int width, int height)
    {
        // 退化した矩形を固定すると、解放時に空の切り出しを確定することになる
        var selection = new CaptureClickSelection();

        Assert.False(selection.Fix(new EsRectInt(x, y, width, height)));
        Assert.Null(selection.Take());
    }

    [Fact]
    public void Take_固定が無ければnullを返す()
    {
        var selection = new CaptureClickSelection();

        Assert.Null(selection.Take());
    }

    [Fact]
    public void Take_2回目はnullを返す()
    {
        // 取り出しは 1 回限り（同じ押下で 2 回確定しない）
        var selection = new CaptureClickSelection();
        selection.Fix(new EsRectInt(1, 2, 3, 4));

        Assert.Equal(new EsRectInt(1, 2, 3, 4), selection.Take());
        Assert.Null(selection.Take());
    }

    [Fact]
    public void Fix_再押下では前回の固定を置き換える()
    {
        var selection = new CaptureClickSelection();
        selection.Fix(new EsRectInt(1, 2, 3, 4));
        selection.Fix(new EsRectInt(5, 6, 7, 8));

        Assert.Equal(new EsRectInt(5, 6, 7, 8), selection.Take());
    }

    [Fact]
    public void Fix_退化した矩形は固定せず前回の固定も破棄する()
    {
        // 古い固定が解放時に確定されるのを防ぐ（固定は常に最後の押下の結果になる）
        var selection = new CaptureClickSelection();
        selection.Fix(new EsRectInt(1, 2, 3, 4));

        Assert.False(selection.Fix(default));
        Assert.Null(selection.Take());
    }
}

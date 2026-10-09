using EsUtil.SaveEvidence.App.Core.Models;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary>
/// <see cref="ThumbnailPriority"/> の単体テスト（見えている部分の優先と、その中での上方優先）。
/// </summary>
/// <remarks>
/// 可視判定は表示範囲の重なりだけで決まる純粋関数のため、画面なしで境界まで検証できる。
/// </remarks>
public class ThumbnailPriorityTests
{
    [Fact]
    internal void 表示範囲が重なれば見えている扱いになる()
    {
        // 可視領域 100〜200 に対して、上端が内側・下端が内側・全体を覆う・一部だけの 4 通り
        Assert.Equal(ThumbnailPriority.VisibleBand, ThumbnailPriority.FromRange(120, 180, 100, 200, 0).Band);
        Assert.Equal(ThumbnailPriority.VisibleBand, ThumbnailPriority.FromRange(50, 150, 100, 200, 0).Band);
        Assert.Equal(ThumbnailPriority.VisibleBand, ThumbnailPriority.FromRange(150, 250, 100, 200, 0).Band);
        Assert.Equal(ThumbnailPriority.VisibleBand, ThumbnailPriority.FromRange(0, 1000, 100, 200, 0).Band);
    }

    [Fact]
    internal void 表示範囲の外は見えていない扱いになる()
    {
        Assert.Equal(ThumbnailPriority.BackgroundBand, ThumbnailPriority.FromRange(0, 90, 100, 200, 0).Band);
        Assert.Equal(ThumbnailPriority.BackgroundBand, ThumbnailPriority.FromRange(210, 300, 100, 200, 0).Band);
    }

    [Fact]
    internal void 端が一致するだけでは見えている扱いにしない()
    {
        Assert.Equal(ThumbnailPriority.BackgroundBand, ThumbnailPriority.FromRange(0, 100, 100, 200, 0).Band);
        Assert.Equal(ThumbnailPriority.BackgroundBand, ThumbnailPriority.FromRange(200, 300, 100, 200, 0).Band);
    }

    [Fact]
    internal void 見えている項目は見えていない項目より先になる()
    {
        var background = ThumbnailPriority.Background(0);
        var visible = ThumbnailPriority.Visible(100);

        Assert.True(ThumbnailPriority.Compare(visible, background) < 0);
        Assert.True(ThumbnailPriority.Compare(background, visible) > 0);
    }

    [Fact]
    internal void 同じ帯では上方の項目が先になる()
    {
        Assert.True(ThumbnailPriority.Compare(ThumbnailPriority.Visible(1), ThumbnailPriority.Visible(2)) < 0);
        Assert.True(ThumbnailPriority.Compare(ThumbnailPriority.Background(1), ThumbnailPriority.Background(2)) < 0);
        Assert.Equal(0, ThumbnailPriority.Compare(ThumbnailPriority.Visible(3), ThumbnailPriority.Visible(3)));
    }
}

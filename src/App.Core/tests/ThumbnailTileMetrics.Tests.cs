using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ThumbnailTileMetrics"/> の仕様テスト（サムネイル 1 件の見た目の寸法）。</summary>
/// <remarks>
/// 4 つの UI 版が同じ値を使うため、値そのものを固定する（片方だけ変えると見た目がずれる）。
/// </remarks>
public class ThumbnailTileMetricsTests
{
    [Fact]
    internal void 名前の行_は2行分の高さを確保する()
        => Assert.Equal(30, ThumbnailTileMetrics.LabelHeight);

    [Fact]
    internal void 表示高さ_は縦横比を保つ()
    {
        Assert.Equal(120, ThumbnailTileMetrics.ImageHeight(240, 640, 320));
        Assert.Equal(720, ThumbnailTileMetrics.ImageHeight(240, 320, 960));
    }

    [Fact]
    internal void 表示高さ_は幅や画素が不正なら幅を返す()
    {
        Assert.Equal(240, ThumbnailTileMetrics.ImageHeight(240, 0, 100));
        Assert.Equal(240, ThumbnailTileMetrics.ImageHeight(240, 100, 0));
        Assert.Equal(0, ThumbnailTileMetrics.ImageHeight(0, 100, 100));
    }
}

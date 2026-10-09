using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Capture;

/// <summary><see cref="CaptureResult"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>「キャンセルは 0x0 の空結果として返す」規則（<see cref="CaptureResult.From"/>）を固定する。</remarks>
public class CaptureResultTests
{
    [Fact]
    internal void From_画像と領域があればそのまま保持する()
    {
        using var image = EsWritableBitmap.Create(10, 20);

        var result = CaptureResult.From(image, new EsRectInt(1, 2, 10, 20));

        Assert.Same(image, result.Image);
        Assert.Equal(new EsRectInt(1, 2, 10, 20), result.SourceBounds);
    }

    [Fact]
    internal void From_画像が無ければ領域0x0の空結果にする()
    {
        // キャンセルは「画像 null・領域 0x0」で表す（null ではなく 0x0 の空矩形を与える）
        var result = CaptureResult.From(null, new EsRectInt(1, 2, 10, 20));

        Assert.Null(result.Image);
        Assert.Equal<EsRectInt?>(new EsRectInt(0, 0, 0, 0), result.SourceBounds);
    }

    [Fact]
    internal void From_領域が無ければキャンセルの空結果にする()
    {
        using var image = EsWritableBitmap.Create(10, 20);

        var result = CaptureResult.From(image, null);

        Assert.Null(result.Image);
        Assert.Equal<EsRectInt?>(new EsRectInt(0, 0, 0, 0), result.SourceBounds);
    }

    [Fact]
    internal void From_両方無ければキャンセルの空結果にする()
    {
        var result = CaptureResult.From(null, null);

        Assert.Null(result.Image);
        Assert.Equal<EsRectInt?>(new EsRectInt(0, 0, 0, 0), result.SourceBounds);
    }
}

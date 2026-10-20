using EsUtil.SaveEvidence.App.Core.Icons;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="CaptureIconFiles"/> の仕様テスト。</summary>
public class CaptureIconFilesTests
{
    [Fact]
    internal void All_埋め込みリソースに存在する()
    {
        var resources = SvgIconLoader.Enumerate();

        Assert.Contains(CaptureIconFiles.Save, resources);
        Assert.Contains(CaptureIconFiles.ScreenSelect, resources);
        Assert.Contains(CaptureIconFiles.WindowSelect, resources);
        Assert.Contains(CaptureIconFiles.AreaSelect, resources);
        Assert.Contains(CaptureIconFiles.Crop, resources);
    }
}

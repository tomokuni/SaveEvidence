using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationPanelPlacement"/> の規則を検証する。</summary>
public sealed class AnnotationPanelPlacementTests
{
    /// <summary>アンカーをそのままパネルの左上にする（ウィンドウの内側へ収めない）。</summary>
    [Fact]
    internal void ResolveInitial_アンカーをそのまま左上にする()
    {
        var position = AnnotationPanelPlacement.ResolveInitial(new EsPointInt(1234, -56));

        Assert.Equal(1234, position.X);
        Assert.Equal(-56, position.Y);
    }

    /// <summary>ドラッグではポインターの移動量だけを足す（ウィンドウ内座標に依存しない）。</summary>
    [Fact]
    internal void ResolveDrag_ポインターの移動量だけを足す()
    {
        var position = AnnotationPanelPlacement.ResolveDrag(
            windowOrigin: new EsPointInt(500, 400),
            cursorOrigin: new EsPointInt(520, 410),
            cursorCurrent: new EsPointInt(480, 470));

        Assert.Equal(460, position.X);
        Assert.Equal(460, position.Y);
    }

    /// <summary>移動していないときは位置が変わらない。</summary>
    [Fact]
    internal void ResolveDrag_移動していないときは変わらない()
    {
        var origin = new EsPointInt(700, 300);

        var position = AnnotationPanelPlacement.ResolveDrag(origin, origin, origin);

        Assert.Equal(origin, position);
    }
}

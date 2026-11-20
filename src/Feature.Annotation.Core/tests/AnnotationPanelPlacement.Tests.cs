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

    /// <summary>本文の上へ余白を空けて置く（本文に被せない）。</summary>
    [Fact]
    internal void ResolveOutside_本文の上へ置く()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(200, 400, 300, 200),
            panel: new EsSizeInt(400, 40),
            workArea: new EsRect(0, 0, 1920, 1040),
            gap: 8);

        Assert.Equal(200, position.X);
        Assert.Equal(400 - 8 - 40, position.Y);
    }

    /// <summary>上に入りきらないときは本文の下へ置く。</summary>
    [Fact]
    internal void ResolveOutside_上に入らなければ下へ置く()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(200, 10, 300, 200),
            panel: new EsSizeInt(400, 40),
            workArea: new EsRect(0, 0, 1920, 1040),
            gap: 8);

        Assert.Equal(10 + 200 + 8, position.Y);
    }

    /// <summary>上下とも入りきらないときは本文の右へ置く。</summary>
    [Fact]
    internal void ResolveOutside_上下に入らなければ右へ置く()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(200, 10, 300, 1000),
            panel: new EsSizeInt(400, 40),
            workArea: new EsRect(0, 0, 1920, 1040),
            gap: 8);

        Assert.Equal(200 + 300 + 8, position.X);
    }

    /// <summary>画面の作業領域からはみ出さない（掴めなくならない）。</summary>
    [Fact]
    internal void ResolveOutside_作業領域からはみ出さない()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(1800, 400, 100, 100),
            panel: new EsSizeInt(400, 40),
            workArea: new EsRect(0, 0, 1920, 1040),
            gap: 8);

        Assert.InRange(position.X, 0, 1920 - 400);
        Assert.InRange(position.Y, 0, 1040 - 40);
    }
}

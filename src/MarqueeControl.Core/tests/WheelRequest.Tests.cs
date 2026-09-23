using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Marquee.Tests;

/// <summary>ホイール要求（<see cref="WheelScrollRequest"/> / <see cref="WheelZoomRequest"/>）の仕様テスト。</summary>
/// <remarks>
/// 回転量をベクターで受け取る API へ渡すための <see cref="WheelScrollRequest.Delta"/> を固定する<br/>
/// （コアの型同士の変換を本契約が単一所有し、UI が <c>new EsVector(...)</c> を書かない）。
/// </remarks>
public class WheelRequestTests
{
    [Fact]
    public void Delta_横と縦の回転量をベクターとして返す()
    {
        var request = new WheelScrollRequest(DeltaX: 2.0, DeltaY: -1.5, Shift: true, Position: new EsPoint(10, 20));

        var delta = request.Delta;

        Assert.Equal(2.0, delta.X);
        Assert.Equal(-1.5, delta.Y);
    }

    [Fact]
    public void Delta_回転量が無い場合は零ベクターを返す()
    {
        var request = new WheelScrollRequest(0, 0, Shift: false, Position: default);

        Assert.Equal(default, request.Delta);
    }

    [Fact]
    public void WheelZoomRequest_方向と位置を保持する()
    {
        var request = new WheelZoomRequest(ZoomIn: true, Position: new EsPoint(3, 4));

        Assert.True(request.ZoomIn);
        Assert.Equal(new EsPoint(3, 4), request.Position);
    }
}

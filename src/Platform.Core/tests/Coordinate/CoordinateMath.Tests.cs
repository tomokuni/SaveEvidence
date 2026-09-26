using EsUtil.Platform.Core.Coordinate;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coordinate;

/// <summary><see cref="CoordMath"/> の単体テスト（座標変換の純粋計算）。</summary>
public class CoordinateMathTests
{
    // ====================================================================
    // CalculateUniformLayout（レターボックス計算の純粋ロジック）
    // ====================================================================

    [Fact]
    public void CalculateUniformLayout_同一アスペクト比で等倍レイアウトを返す()
    {
        var (rw, rh, ox, oy) = CoordMath.CalculateUniformLayout(100, 100, 100, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
    }

    [Fact]
    public void CalculateUniformLayout_横長表示領域で左右に余白を算出する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）
        var (rw, rh, ox, oy) = CoordMath.CalculateUniformLayout(200, 100, 100, 100);
        Assert.Equal(100, rh);   // 高さでフィット
        Assert.Equal(100, rw);   // 幅 = 高さ * 1.0
        Assert.Equal(50, ox);    // 左右に余白
        Assert.Equal(0, oy);
    }

    [Fact]
    public void CalculateUniformLayout_縦長表示領域で上下に余白を算出する()
    {
        // 表示領域 100x200（縦長）、元画像 100x100（正方形）
        var (rw, rh, ox, oy) = CoordMath.CalculateUniformLayout(100, 200, 100, 100);
        Assert.Equal(100, rw);   // 幅でフィット
        Assert.Equal(100, rh);   // 高さ = 幅 * 1.0
        Assert.Equal(0, ox);
        Assert.Equal(50, oy);    // 上下に余白
    }

    [Fact]
    public void CalculateUniformLayout_横長画像を横長表示領域にフィットする()
    {
        // 表示領域 200x200、元画像 200x100（横長）
        var (rw, rh, ox, oy) = CoordMath.CalculateUniformLayout(200, 200, 200, 100);
        Assert.Equal(200, rw);   // 幅でフィット（アスペクト比 2.0 で高さ 100）
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(50, oy);    // 上下に余白
    }

    // ====================================================================
    // ComputeImageRenderInfo（レイアウト + スケール係数の一元計算）
    // ====================================================================

    [Fact]
    public void ComputeImageRenderInfo_同一アスペクトで等倍スケールを返す()
    {
        var (rw, rh, ox, oy, sx, sy) = CoordMath.ComputeImageRenderInfo(100, 100, 100, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_レターボックス時にスケール係数を算出する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）→ 表示 100x100、スケール 1.0
        var (rw, rh, ox, oy, sx, sy) = CoordMath.ComputeImageRenderInfo(200, 100, 100, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(50, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_縮小時にスケール係数を算出する()
    {
        // 表示領域 100x100、元画像 200x100（横長）→ 表示 100x50、スケール 2.0
        var (rw, rh, ox, oy, sx, sy) = CoordMath.ComputeImageRenderInfo(100, 100, 200, 100);
        Assert.Equal(100, rw);
        Assert.Equal(50, rh);
        Assert.Equal(0, ox);
        Assert.Equal(25, oy);
        Assert.Equal(2.0, sx);
        Assert.Equal(2.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_非UniformでFillスケールを算出する()
    {
        // 表示領域 100x50、元画像 200x100、Fill → 表示領域いっぱい、スケール 2.0
        var (rw, rh, ox, oy, sx, sy) = CoordMath.ComputeImageRenderInfo(100, 50, 200, 100, uniform: false);
        Assert.Equal(100, rw);
        Assert.Equal(50, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(2.0, sx);
        Assert.Equal(2.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_表示領域サイズ0時は等倍を返す()
    {
        var (rw, rh, ox, oy, sx, sy) = CoordMath.ComputeImageRenderInfo(0, 100, 100, 100);
        Assert.Equal(0, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_元画像サイズ0時は等倍を返す()
    {
        var (rw, rh, ox, oy, sx, sy) = CoordMath.ComputeImageRenderInfo(100, 100, 0, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }
}

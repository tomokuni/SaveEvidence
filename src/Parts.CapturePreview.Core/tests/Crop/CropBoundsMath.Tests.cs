using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Crop;

/// <summary><see cref="CropBoundsMath"/> の単体テスト（クランプ・丸め・最小サイズ・閉区間）。</summary>
/// <remarks>
/// Avalonia 版（<c>CropSession</c>）と MewUI 版（<c>CaptureSelectionMath</c>）から<br/>
/// 移した境界値テストを 1 箇所へ集約した。<br/>
/// 選択矩形は画像ピクセル座標で得られるため、画像の外へはみ出す入力・端数の丸め・退化した矩形を固定する。
/// </remarks>
public class CropBoundsMathTests
{
    // ====================================================================
    // ToPixelRect（半開区間・クランプ・丸め）
    // ====================================================================

    [Theory]
    [InlineData(10, 20, 100, 50, 10, 20, 100, 50)]
    [InlineData(0, 0, 200, 100, 0, 0, 200, 100)]
    [InlineData(0.4, 0.6, 10.4, 10.6, 0, 1, 11, 10)]
    [InlineData(10.5, 10.5, 4, 4, 11, 11, 4, 4)]
    [InlineData(10.4, 10.4, 4, 4, 10, 10, 4, 4)]
    internal void ToPixelRect_画像内の矩形は四捨五入して変換する(
        double x, double y, double width, double height,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var result = CropBoundsMath.ToPixelRect(new EsRect(x, y, width, height), new EsSize(400, 300));

        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Fact]
    internal void ToPixelRect_画像の外へはみ出す矩形は画像内へクランプする()
    {
        var result = CropBoundsMath.ToPixelRect(new EsRect(-50, -30, 500, 400), new EsSize(200, 150));

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(200, result.Width);
        Assert.Equal(150, result.Height);
    }

    [Fact]
    internal void ToPixelRect_一部が画像の外にある矩形は重なる範囲だけを返す()
    {
        var result = CropBoundsMath.ToPixelRect(new EsRect(150, 100, 200, 200), new EsSize(200, 150));

        Assert.Equal(150, result.X);
        Assert.Equal(100, result.Y);
        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Theory]
    [InlineData(500, 500, 10, 10)]
    [InlineData(-100, 0, 50, 50)]
    [InlineData(0, -100, 50, 50)]
    internal void ToPixelRect_画像と重ならない矩形は空を返す(
        double x, double y, double width, double height)
    {
        Assert.True(CropBoundsMath.ToPixelRect(new EsRect(x, y, width, height), new EsSize(200, 150)).IsEmpty);
    }

    [Theory]
    [InlineData(10, 10, 0, 50)]
    [InlineData(10, 10, 0.4, 50)]
    [InlineData(10, 10, 50, 0)]
    [InlineData(10, 10, -20, -20)]
    internal void ToPixelRect_1ピクセル未満の矩形は空を返す(
        double x, double y, double width, double height)
    {
        Assert.True(CropBoundsMath.ToPixelRect(new EsRect(x, y, width, height), new EsSize(200, 150)).IsEmpty);
    }

    [Theory]
    [InlineData(0, 150)]
    [InlineData(200, 0)]
    [InlineData(-10, -10)]
    internal void ToPixelRect_画像サイズが0以下なら空を返す(int imageWidth, int imageHeight)
    {
        Assert.True(CropBoundsMath.ToPixelRect(new EsRect(0, 0, 50, 50), new EsSize(imageWidth, imageHeight)).IsEmpty);
    }

    [Fact]
    internal void ToPixelRect_画像全体の選択は全画素を返す()
    {
        var result = CropBoundsMath.ToPixelRect(new EsRect(0.2, 0.2, 199.7, 149.7), new EsSize(200, 150));

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(200, result.Width);
        Assert.Equal(150, result.Height);
    }

    // ====================================================================
    // ToClosedInterval（終点の 1 ピクセルを加える）
    // ====================================================================

    [Fact]
    internal void ToClosedInterval_終点の1ピクセルを加える()
    {
        var result = CropBoundsMath.ToClosedInterval(new EsRectInt(10, 20, 40, 30), new EsSize(400, 300));

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(41, result.Width);
        Assert.Equal(31, result.Height);
    }

    [Fact]
    internal void ToClosedInterval_右端下端の1ピクセルは画像サイズを超えない()
    {
        var result = CropBoundsMath.ToClosedInterval(new EsRectInt(199, 149, 1, 1), new EsSize(200, 150));

        Assert.Equal(199, result.X);
        Assert.Equal(149, result.Y);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    internal void ToClosedInterval_画像全体の選択は画像サイズを維持する()
    {
        var result = CropBoundsMath.ToClosedInterval(new EsRectInt(0, 0, 200, 150), new EsSize(200, 150));

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(200, result.Width);
        Assert.Equal(150, result.Height);
    }

    [Fact]
    internal void ToClosedInterval_画像サイズ無しはクランプしない()
    {
        // 画像サイズを知らない呼び出し（画面全体が切り出し対象と一致する場合等）は +1 のみ行う
        var result = CropBoundsMath.ToClosedInterval(new EsRectInt(10, 20, 40, 30));

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(41, result.Width);
        Assert.Equal(31, result.Height);
    }

    // ====================================================================
    // RoundToPixel（丸め規則の単一所有）
    // ====================================================================

    [Theory]
    [InlineData(10.4, 10)]
    [InlineData(10.5, 11)]
    [InlineData(10.6, 11)]
    [InlineData(-0.5, -1)]
    [InlineData(0.0, 0)]
    internal void RoundToPixel_0_5は切り上げる(double value, int expected)
    {
        // 銀行丸め（0.5 を偶数へ）にならないこと（ピクセル座標の慣例）
        Assert.Equal(expected, CropBoundsMath.RoundToPixel(value));
    }

    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(3.5, 4)]
    [InlineData(4.5, 5)]
    internal void RoundToPixel_偶数へ丸めない(double value, int expected)
    {
        // Math.Round の既定（銀行丸め）なら 2.5→2 / 3.5→4 / 4.5→4 になる。 AwayFromZero は常に切り上げる
        Assert.Equal(expected, CropBoundsMath.RoundToPixel(value));
    }

    [Theory]
    [InlineData(199, 149, 1, 1)]
    [InlineData(0, 0, 200, 150)]
    [InlineData(10, 20, 40, 30)]
    internal void ToClosedInterval_画像サイズ付きは幅高さの上限だけを変える(int x, int y, int width, int height)
    {
        // 画像サイズ付きは、画像サイズ無しの結果を画像の端でクランプした値になる（規則を共有している）
        var size = new EsSize(200, 150);
        var plain = CropBoundsMath.ToClosedInterval(new EsRectInt(x, y, width, height));
        var clamped = CropBoundsMath.ToClosedInterval(new EsRectInt(x, y, width, height), size);

        Assert.Equal(plain.X, clamped.X);
        Assert.Equal(plain.Y, clamped.Y);
        Assert.Equal(Math.Min(plain.Width, Math.Max(0, 200 - x)), clamped.Width);
        Assert.Equal(Math.Min(plain.Height, Math.Max(0, 150 - y)), clamped.Height);
    }

    // ====================================================================
    // ComputeCropBounds（最小サイズ判定 + 区間の流儀に応じた終点の加算）
    // ====================================================================

    [Fact]
    internal void ComputeCropBounds_閉区間で始点終点のピクセルを含む()
    {
        // 選択矩形 (10,10,40,40) = 半開区間 [10,50)。 閉区間では [10,50] = 41×41 ピクセル
        var result = CropBoundsMath.ComputeCropBounds(
            new EsRect(10, 10, 40, 40, EsRectInterval.Closed), new EsSize(100, 100), 1.0);

        Assert.Equal(new EsRectInt(10, 10, 41, 41), result);
    }

    [Fact]
    internal void ComputeCropBounds_画像右端では幅が画像サイズにクランプされる()
    {
        // 選択矩形が画像右端 (80,80,20,20) = [80,100)。 閉区間では [80,100] = 21 ピクセルだが、
        // 画像サイズ 100 を超えないよう 20 ピクセルにクランプする
        var result = CropBoundsMath.ComputeCropBounds(
            new EsRect(80, 80, 20, 20, EsRectInterval.Closed), new EsSize(100, 100), 1.0);

        Assert.Equal(new EsRectInt(80, 80, 20, 20), result);
    }

    [Fact]
    internal void ComputeCropBounds_最小サイズ未満は空を返す()
    {
        // 幅・高さが 1 未満（MinSelectionSize の既定値 1.0 未満）→ 空を返す
        var result = CropBoundsMath.ComputeCropBounds(new EsRect(10, 10, 0.5, 0.5), new EsSize(100, 100), 1.0);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    internal void ComputeCropBounds_負座標は0にクランプする()
    {
        // 選択矩形が画像外（負座標）→ 0 にクランプして閉区間で切り出す
        var result = CropBoundsMath.ComputeCropBounds(
            new EsRect(-5, -5, 20, 20, EsRectInterval.Closed), new EsSize(100, 100), 1.0);

        Assert.Equal(new EsRectInt(0, 0, 16, 16), result);
    }

    [Fact]
    internal void ComputeCropBounds_完全に範囲外なら空を返す()
    {
        var result = CropBoundsMath.ComputeCropBounds(new EsRect(150, 150, 20, 20), new EsSize(100, 100), 1.0);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    internal void ComputeCropBounds_最小サイズを指定するとそれ未満を空とみなす()
    {
        // MinSelectionSize = 10 の場合、幅 5 は空を返す
        var result = CropBoundsMath.ComputeCropBounds(new EsRect(10, 10, 5, 20), new EsSize(100, 100), 10.0);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    internal void ComputeCropBounds_半開区間の矩形では終点のピクセルを加えない()
    {
        // スクリーン選択・ウィンドウ選択のハイライト矩形は全ピクセルを含むため加算しない
        var result = CropBoundsMath.ComputeCropBounds(
            new EsRect(10, 10, 40, 40), new EsSize(100, 100), 1.0);

        Assert.Equal(new EsRectInt(10, 10, 40, 40), result);
    }

    [Fact]
    internal void ComputeCropBounds_画像サイズが0以下なら空を返す()
    {
        Assert.True(CropBoundsMath.ComputeCropBounds(new EsRect(0, 0, 10, 10), new EsSize(0, 100), 1.0).IsEmpty);
        Assert.True(CropBoundsMath.ComputeCropBounds(new EsRect(0, 0, 10, 10), new EsSize(100, 0), 1.0).IsEmpty);
    }
}

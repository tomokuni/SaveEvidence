using Estable.Platform.Geometry;
using SaveEvidence.MewUI.Services;
using Xunit;

namespace SaveEvidence.MewUI.Tests;

/// <summary><see cref="CaptureSelectionMath"/> の単体テスト（クランプ・丸め・空の判定）。</summary>
/// <remarks>
/// 選択矩形は画像ピクセル座標で得られるため、画像の外へはみ出す入力・端数の丸め・退化した矩形を固定する。
/// </remarks>
public class CaptureSelectionMathTests
{
    [Theory]
    [InlineData(10, 20, 100, 50, 10, 20, 100, 50)]
    [InlineData(0, 0, 200, 100, 0, 0, 200, 100)]
    [InlineData(0.4, 0.6, 10.4, 10.6, 0, 1, 11, 10)]
    [InlineData(10.5, 10.5, 4, 4, 11, 11, 4, 4)]
    [InlineData(10.4, 10.4, 4, 4, 10, 10, 4, 4)]
    public void ToPixelRect_画像内の矩形は四捨五入して変換する(
        double x, double y, double width, double height,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var result = CaptureSelectionMath.ToPixelRect(new(x, y, width, height), 400, 300);

        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Fact]
    public void ToPixelRect_画像の外へはみ出す矩形は画像内へクランプする()
    {
        var result = CaptureSelectionMath.ToPixelRect(new(-50, -30, 500, 400), 200, 150);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(200, result.Width);
        Assert.Equal(150, result.Height);
    }

    [Fact]
    public void ToPixelRect_一部が画像の外にある矩形は重なる範囲だけを返す()
    {
        var result = CaptureSelectionMath.ToPixelRect(new(150, 100, 200, 200), 200, 150);

        Assert.Equal(150, result.X);
        Assert.Equal(100, result.Y);
        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Theory]
    [InlineData(500, 500, 10, 10)]
    [InlineData(-100, 0, 50, 50)]
    [InlineData(0, -100, 50, 50)]
    public void ToPixelRect_画像と重ならない矩形は空を返す(
        double x, double y, double width, double height)
    {
        Assert.True(CaptureSelectionMath.ToPixelRect(new(x, y, width, height), 200, 150).IsEmpty);
    }

    [Theory]
    [InlineData(10, 10, 0, 50)]
    [InlineData(10, 10, 0.4, 50)]
    [InlineData(10, 10, 50, 0)]
    [InlineData(10, 10, -20, -20)]
    public void ToPixelRect_1ピクセル未満の矩形は空を返す(
        double x, double y, double width, double height)
    {
        Assert.True(CaptureSelectionMath.ToPixelRect(new(x, y, width, height), 200, 150).IsEmpty);
    }

    [Theory]
    [InlineData(0, 150)]
    [InlineData(200, 0)]
    [InlineData(-10, -10)]
    public void ToPixelRect_画像サイズが0以下なら空を返す(int imageWidth, int imageHeight)
    {
        Assert.True(CaptureSelectionMath.ToPixelRect(new(0, 0, 50, 50), imageWidth, imageHeight).IsEmpty);
    }

    [Fact]
    public void ToPixelRect_画像全体の選択は全画素を返す()
    {
        var result = CaptureSelectionMath.ToPixelRect(new(0.2, 0.2, 199.7, 149.7), 200, 150);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(200, result.Width);
        Assert.Equal(150, result.Height);
    }

    // ====================================================================
    // ToClosedInterval（終点の 1 ピクセルを加える）
    // ====================================================================

    [Fact]
    public void ToClosedInterval_終点の1ピクセルを加える()
    {
        // 半開区間 (10, 20, 40, 30) は [10, 50) × [20, 50)。閉区間では 41 x 31 ピクセル
        var result = CaptureSelectionMath.ToClosedInterval(new(10, 20, 40, 30), 400, 300);

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(41, result.Width);
        Assert.Equal(31, result.Height);
    }

    [Fact]
    public void ToClosedInterval_右端下端の1ピクセルは画像サイズを超えない()
    {
        // 半開区間 (199, 149, 1, 1) は画像の最後の 1 ピクセル。+1 すると画像外になるためクランプする
        var result = CaptureSelectionMath.ToClosedInterval(new(199, 149, 1, 1), 200, 150);

        Assert.Equal(199, result.X);
        Assert.Equal(149, result.Y);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void ToClosedInterval_画像全体の選択は画像サイズを維持する()
    {
        var result = CaptureSelectionMath.ToClosedInterval(new(0, 0, 200, 150), 200, 150);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(200, result.Width);
        Assert.Equal(150, result.Height);
    }

    [Fact]
    public void SelectScreen_座標を含むスクリーンを返す()
    {
        // プライマリ (0,0,3840,2160) と その上に並ぶセカンダリ (1914,-1200,1920,1200)
        EsPixelRect[] screens =
        [
            new(0, 0, 3840, 2160),
            new(1914, -1200, 1920, 1200),
        ];

        Assert.Equal(screens[0], CaptureSelectionMath.SelectScreen(new EsPixelPoint(100, 100), screens));
        Assert.Equal(screens[1], CaptureSelectionMath.SelectScreen(new EsPixelPoint(2000, -600), screens));
    }

    [Fact]
    public void SelectScreen_左端と上端は含み右端と下端は含めない()
    {
        EsPixelRect[] screens = [new(0, 0, 1920, 1080)];

        Assert.NotNull(CaptureSelectionMath.SelectScreen(new EsPixelPoint(0, 0), screens));
        Assert.NotNull(CaptureSelectionMath.SelectScreen(new EsPixelPoint(1919, 1079), screens));
        Assert.Null(CaptureSelectionMath.SelectScreen(new EsPixelPoint(1920, 1079), screens));
        Assert.Null(CaptureSelectionMath.SelectScreen(new EsPixelPoint(1919, 1080), screens));
    }

    [Fact]
    public void SelectScreen_どのスクリーンにも含まれなければnullを返す()
    {
        EsPixelRect[] screens = [new(0, 0, 1920, 1080)];

        Assert.Null(CaptureSelectionMath.SelectScreen(new EsPixelPoint(-10, 100), screens));
        Assert.Null(CaptureSelectionMath.SelectScreen(new EsPixelPoint(100, -10), screens));
        Assert.Null(CaptureSelectionMath.SelectScreen(new EsPixelPoint(5000, 5000), screens));
    }

    [Fact]
    public void SelectScreen_一覧が空ならnullを返す()
        => Assert.Null(CaptureSelectionMath.SelectScreen(new EsPixelPoint(0, 0), []));
}

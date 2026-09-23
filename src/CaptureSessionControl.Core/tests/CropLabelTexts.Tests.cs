using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.CaptureSession.Tests;

/// <summary><see cref="CropLabelTexts"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>切り出し固有の書式（閉区間のピクセル数とズーム率）と、未選択時の表示可否を固定する。</remarks>
public class CropLabelTextsTests
{
    [Fact]
    public void ResolveSizeLabel_選択があれば閉区間のピクセル数を表示する()
    {
        var label = CropLabelTexts.ResolveSizeLabel(hasSelection: true, new EsRect(10, 20, 30, 40), zoomPercent: 100);

        Assert.Equal("LT: (10, 20),  RB: (40, 60),  WH: (31, 41), Zoom: (100%)", label);
    }

    [Fact]
    public void ResolveSizeLabel_未選択の場合はnullを返す()
    {
        // 「未選択なら描かない」規則をコアが単一所有する
        Assert.Null(CropLabelTexts.ResolveSizeLabel(hasSelection: false, new EsRect(10, 20, 30, 40), zoomPercent: 100));
    }

    [Fact]
    public void FormatCropSizeLabel_閉区間のピクセル数を表示する()
    {
        var label = CropLabelTexts.FormatCropSizeLabel(new EsRect(10, 20, 30, 40), zoomPercent: 100);

        // WH は終点のピクセルを含む（幅 30 → 31、高さ 40 → 41）
        Assert.Equal("LT: (10, 20),  RB: (40, 60),  WH: (31, 41), Zoom: (100%)", label);
    }

    [Theory]
    [InlineData(99.4, 99)]
    [InlineData(99.5, 100)]
    [InlineData(110.6, 111)]
    [InlineData(0.0, 0)]
    public void FormatCropSizeLabel_ズーム率は四捨五入して整数表示する(double zoomPercent, int expected)
    {
        var label = CropLabelTexts.FormatCropSizeLabel(new EsRect(0, 0, 1, 1), zoomPercent);

        Assert.Contains($"Zoom: ({expected}%)", label);
    }

    [Fact]
    public void FormatCropSizeLabel_小数座標は切り捨てる()
    {
        var label = CropLabelTexts.FormatCropSizeLabel(new EsRect(10.9, 20.9, 30.9, 40.9), zoomPercent: 50);

        // Right = 10.9 + 30.9 = 41.8 → 41、Bottom = 20.9 + 40.9 = 61.8 → 61
        Assert.Equal("LT: (10, 20),  RB: (41, 61),  WH: (32, 42), Zoom: (50%)", label);
    }

    [Fact]
    public void FormatCropSizeLabel_幅高さ0は1ピクセルになる()
    {
        var label = CropLabelTexts.FormatCropSizeLabel(new EsRect(5, 5, 0, 0), zoomPercent: 100);

        // 閉区間では幅 0 でも 1 ピクセル（終点のみ）を切り出す
        Assert.Contains("WH: (1, 1)", label);
    }
}

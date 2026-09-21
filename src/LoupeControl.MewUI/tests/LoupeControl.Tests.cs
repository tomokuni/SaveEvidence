using Aprillz.MewUI;
using Estable.Loupe;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using MewUIColor = Aprillz.MewUI.Color;

namespace Estable.MewUI.Loupe.Tests;

/// <summary><see cref="LoupeControl"/> のプロパティとレイアウトのテスト。</summary>
/// <remarks>
/// MewUI の要素生成はプラットフォームホストの登録を必要としないため、UI を表示せずに検証できる。<br/>
/// 描画結果の検証は <see cref="LoupeSkiaRendererTests"/> が担う。
/// </remarks>
public class LoupeControlTests
{
    /// <summary>既定値がコアの既定値と一致することを検証する。</summary>
    [Fact]
    public void 既定値がコアの既定値と一致する()
    {
        var control = new LoupeControl();

        Assert.Null(control.Source);
        Assert.Null(control.CenterPoint);
        Assert.Equal(LoupeValueCoercer.RequestedSizeDefault, control.RequestedSize);
        Assert.Equal(LoupeValueCoercer.ZoomLevelDefault, control.ZoomLevel);
        Assert.Equal(LoupeValueCoercer.RenderScalingDefault, control.RenderScaling);
        Assert.Equal(LoupeValueCoercer.CheckerSizeDefault, control.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthDefault, control.FrameWidth);
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, control.CoordLabelFontSize);
        Assert.True(control.ShowCoordLabel);
    }

    /// <summary>数値プロパティがコアの安全域へ補正されることを検証する。</summary>
    /// <param name="min">安全域の最小値。</param>
    /// <param name="max">安全域の最大値。</param>
    [Theory]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(0, 100_000)]
    public void 表示サイズが安全域へ補正される(int min, int max)
    {
        var control = new LoupeControl { RequestedSize = min };
        Assert.Equal(LoupeValueCoercer.RequestedSizeMin, control.RequestedSize);

        control.RequestedSize = max;
        Assert.Equal(LoupeValueCoercer.RequestedSizeMax, control.RequestedSize);
    }

    /// <summary>拡大率が安全域へ補正されることを検証する。</summary>
    [Fact]
    public void 拡大率が安全域へ補正される()
    {
        var control = new LoupeControl { ZoomLevel = 0 };
        Assert.Equal(LoupeValueCoercer.ZoomLevelMin, control.ZoomLevel);

        control.ZoomLevel = 1000;
        Assert.Equal(LoupeValueCoercer.ZoomLevelMax, control.ZoomLevel);
    }

    /// <summary>スケーリング係数が非有限値のとき既定値へ補正されることを検証する。</summary>
    /// <param name="value">設定する値。</param>
    /// <param name="expected">期待する補正結果。</param>
    [Theory]
    [InlineData(double.NaN, LoupeValueCoercer.RenderScalingDefault)]
    [InlineData(double.PositiveInfinity, LoupeValueCoercer.RenderScalingDefault)]
    [InlineData(0.0, LoupeValueCoercer.RenderScalingMin)]
    [InlineData(100.0, LoupeValueCoercer.RenderScalingMax)]
    public void スケーリング係数が安全域へ補正される(double value, double expected)
    {
        var control = new LoupeControl { RenderScaling = value };

        Assert.Equal(expected, control.RenderScaling);
    }

    /// <summary>座標ラベルのフォントサイズが安全域へ補正されることを検証する。</summary>
    [Fact]
    public void 座標ラベルのフォントサイズが安全域へ補正される()
    {
        var control = new LoupeControl { CoordLabelFontSize = 0 };
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeMin, control.CoordLabelFontSize);

        control.CoordLabelFontSize = double.NaN;
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, control.CoordLabelFontSize);
    }

    /// <summary>市松模様と外枠のサイズが安全域へ補正されることを検証する。</summary>
    [Fact]
    public void 市松模様と外枠のサイズが安全域へ補正される()
    {
        var control = new LoupeControl
        {
            CheckerSize = 0,
            FrameWidth = 0,
        };

        Assert.Equal(LoupeValueCoercer.CheckerSizeMin, control.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthMin, control.FrameWidth);

        control.CheckerSize = 1000;
        control.FrameWidth = 1000;

        Assert.Equal(LoupeValueCoercer.CheckerSizeMax, control.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthMax, control.FrameWidth);
    }

    /// <summary>要求サイズが <see cref="LoupeControl.RequestedSize"/> の正方形になることを検証する。</summary>
    [Fact]
    public void 要求サイズが一辺RequestedSizeの正方形になる()
    {
        var control = new LoupeControl { RequestedSize = 120 };

        control.Measure(new Size(1000, 1000));

        Assert.Equal(120, control.DesiredSize.Width);
        Assert.Equal(120, control.DesiredSize.Height);
    }

    /// <summary>要求サイズを変更すると測定結果へ反映されることを検証する。</summary>
    [Fact]
    public void 要求サイズの変更が測定結果へ反映される()
    {
        var control = new LoupeControl { RequestedSize = 80 };
        control.Measure(new Size(1000, 1000));
        Assert.Equal(80, control.DesiredSize.Width);

        control.RequestedSize = 200;
        control.Measure(new Size(1000, 1000));

        Assert.Equal(200, control.DesiredSize.Width);
        Assert.Equal(200, control.DesiredSize.Height);
    }

    /// <summary>外観プロパティを設定できることを検証する（描画は <see cref="LoupeSkiaRendererTests"/> が検証する）。</summary>
    [Fact]
    public void 外観プロパティを設定できる()
    {
        var control = new LoupeControl
        {
            CenterPoint = new EsPoint(10, 20),
            CrossColor = MewUIColor.FromArgb(255, 255, 0, 0),
            CheckerColorA = MewUIColor.FromArgb(255, 255, 255, 255),
            CheckerColorB = MewUIColor.FromArgb(255, 0, 0, 0),
            FrameColor = MewUIColor.FromArgb(255, 0, 0, 255),
            CoordLabelColor = MewUIColor.FromArgb(255, 255, 255, 0),
            CoordLabelBackgroundColor = MewUIColor.FromArgb(128, 0, 0, 0),
            ShowCoordLabel = false,
        };

        Assert.Equal(new EsPoint(10, 20), control.CenterPoint);
        Assert.Equal(MewUIColor.FromArgb(255, 255, 0, 0), control.CrossColor);
        Assert.Equal(MewUIColor.FromArgb(255, 0, 0, 0), control.CheckerColorB);
        Assert.False(control.ShowCoordLabel);
    }

    /// <summary>ソース画像を設定できることを検証する。</summary>
    [Fact]
    public void ソース画像を設定できる()
    {
        using var source = TestHelper.CreateBitmap(8, SKColors.Red);
        var control = new LoupeControl { Source = source };

        Assert.Same(source, control.Source);
    }
}

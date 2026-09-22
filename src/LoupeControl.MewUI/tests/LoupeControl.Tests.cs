using Aprillz.MewUI;
using Estable.Loupe;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

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
        Assert.Equal(LoupeValueCoercer.RequestedSizeDefault, control.Options.RequestedSize);
        Assert.Equal(LoupeValueCoercer.ZoomLevelDefault, control.Options.ZoomLevel);
        Assert.Equal(LoupeValueCoercer.RenderScalingDefault, control.RenderScaling);

        // 外観の既定値はコア（LoupeAppearance）が単一所有する（両 UI で同一）
        Assert.Equal(new LoupeAppearance(), control.Options.Appearance);
        Assert.Equal(LoupeValueCoercer.CheckerSizeDefault, control.Options.Appearance.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthDefault, control.Options.Appearance.FrameWidth);
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, control.Options.Appearance.CoordLabelFontSize);
        Assert.True(control.Options.Appearance.ShowCoordLabel);
    }

    /// <summary>数値プロパティがコアの安全域へ補正されることを検証する。</summary>
    /// <param name="min">安全域の最小値。</param>
    /// <param name="max">安全域の最大値。</param>
    [Theory]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(0, 100_000)]
    public void 表示サイズが安全域へ補正される(int min, int max)
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = min } };
        Assert.Equal(LoupeValueCoercer.RequestedSizeMin, control.Options.RequestedSize);

        control.Options = control.Options with { RequestedSize = max };
        Assert.Equal(LoupeValueCoercer.RequestedSizeMax, control.Options.RequestedSize);
    }

    /// <summary>拡大率が安全域へ補正されることを検証する。</summary>
    [Fact]
    public void 拡大率が安全域へ補正される()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { ZoomLevel = 0 } };
        Assert.Equal(LoupeValueCoercer.ZoomLevelMin, control.Options.ZoomLevel);

        control.Options = control.Options with { ZoomLevel = 1000 };
        Assert.Equal(LoupeValueCoercer.ZoomLevelMax, control.Options.ZoomLevel);
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
        var control = new LoupeControl { Options = LoupeOptions.Default with { Appearance = new LoupeAppearance { CoordLabelFontSize = 0 } } };
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeMin, control.Options.Appearance.CoordLabelFontSize);

        control.Options = control.Options with { Appearance = control.Options.Appearance with { CoordLabelFontSize = double.NaN } };
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, control.Options.Appearance.CoordLabelFontSize);
    }

    /// <summary>市松模様と外枠のサイズが安全域へ補正されることを検証する。</summary>
    [Fact]
    public void 市松模様と外枠のサイズが安全域へ補正される()
    {
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { Appearance = new LoupeAppearance { CheckerSize = 0, FrameWidth = 0 } },
        };

        Assert.Equal(LoupeValueCoercer.CheckerSizeMin, control.Options.Appearance.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthMin, control.Options.Appearance.FrameWidth);

        control.Options = control.Options with { Appearance = control.Options.Appearance with { CheckerSize = 1000, FrameWidth = 1000 } };

        Assert.Equal(LoupeValueCoercer.CheckerSizeMax, control.Options.Appearance.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthMax, control.Options.Appearance.FrameWidth);
    }

    /// <summary>要求サイズが <see cref="LoupeOptions.RequestedSize"/> の正方形になることを検証する。</summary>
    [Fact]
    public void 要求サイズが一辺RequestedSizeの正方形になる()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 120 } };

        control.Measure(new Size(1000, 1000));

        Assert.Equal(120, control.DesiredSize.Width);
        Assert.Equal(120, control.DesiredSize.Height);
    }

    /// <summary>要求サイズを変更すると測定結果へ反映されることを検証する。</summary>
    [Fact]
    public void 要求サイズの変更が測定結果へ反映される()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 80 } };
        control.Measure(new Size(1000, 1000));
        Assert.Equal(80, control.DesiredSize.Width);

        control.Options = control.Options with { RequestedSize = 200 };
        control.Measure(new Size(1000, 1000));

        Assert.Equal(200, control.DesiredSize.Width);
        Assert.Equal(200, control.DesiredSize.Height);
    }

    /// <summary>外観を設定できることを検証する（描画は <see cref="LoupeSkiaRendererTests"/> が検証する）。</summary>
    [Fact]
    public void 外観を設定できる()
    {
        var control = new LoupeControl
        {
            CenterPoint = new EsPoint(10, 20),
            Options = LoupeOptions.Default with
            {
                Appearance = new LoupeAppearance
                {
                    CrossColor = DrawingColor.FromArgb(255, 255, 0, 0),
                    CheckerColorA = DrawingColor.FromArgb(255, 255, 255, 255),
                    CheckerColorB = DrawingColor.FromArgb(255, 0, 0, 0),
                    FrameColor = DrawingColor.FromArgb(255, 0, 0, 255),
                    CoordLabelColor = DrawingColor.FromArgb(255, 255, 255, 0),
                    CoordLabelBackgroundColor = DrawingColor.FromArgb(128, 0, 0, 0),
                    ShowCoordLabel = false,
                },
            },
        };

        Assert.Equal(new EsPoint(10, 20), control.CenterPoint);
        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 0), control.Options.Appearance.CrossColor);
        Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 0), control.Options.Appearance.CheckerColorB);
        Assert.False(control.Options.Appearance.ShowCoordLabel);
    }

    /// <summary>ソース画像を設定できることを検証する。</summary>
    [Fact]
    public void ソース画像を設定できる()
    {
        using var source = TestHelper.CreateBitmap(8, SKColors.Red);
        var control = new LoupeControl { Source = source };

        Assert.Same(source, control.Source);
    }

    /// <summary>プロパティの setter を経由しない設定でもコアの安全域へ補正されることを検証する。</summary>
    /// <remarks>
    /// MewUI のバインディングはプロパティの setter を通さずに値を書き込む。<br/>
    /// 設定（<see cref="LoupeOptions"/>）の数値項目は<b>値の型の生成時</b>に補正されるため、
    /// 設定経路（オブジェクト初期化子・<c>with</c> 式・バインディング）によらず範囲内の値だけが保持される。<br/>
    /// 環境値の <see cref="LoupeControl.RenderScaling"/> はプロパティの補正コールバック（<c>coerce</c>）で補正する。<br/>
    /// setter を経由しない設定は <c>MewObject.SetValue</c>（protected）しかないため、リフレクションで呼ぶ。
    /// </remarks>
    [Fact]
    public void 補正はプロパティの全設定経路へ適用される()
    {
        var control = new LoupeControl();

        TestHelper.SetValueWithoutSetter(control, LoupeControl.OptionsProperty, LoupeOptions.Default with
        {
            RequestedSize = 100_000,
            ZoomLevel = 1000,
        });
        TestHelper.SetValueWithoutSetter(control, LoupeControl.RenderScalingProperty, 100.0);

        Assert.Equal(LoupeValueCoercer.RequestedSizeMax, control.Options.RequestedSize);
        Assert.Equal(LoupeValueCoercer.ZoomLevelMax, control.Options.ZoomLevel);
        Assert.Equal(LoupeValueCoercer.RenderScalingMax, control.RenderScaling);
    }

    /// <summary>プロパティ変更がコアのログ定義で記録されることを検証する。</summary>
    /// <param name="propertyName">変更するプロパティ名（ログの設定名と一致する）。</param>
    /// <remarks>
    /// 記録はプロパティの変更通知（<c>changed</c> コールバック）が行う（setter を経由しない設定でも記録される）。<br/>
    /// 配線が外れるとビルドもテストも緑のままログだけ消えるため、全プロパティの記録を検証する。<br/>
    /// 設定のログ名はコアのログ定義（変更された項目ごと）が単一所有するため、項目名で照合する。
    /// </remarks>
    [Theory]
    [InlineData(nameof(LoupeControl.Source))]
    [InlineData(nameof(LoupeControl.CenterPoint))]
    [InlineData(nameof(LoupeOptions.RequestedSize))]
    [InlineData(nameof(LoupeControl.RenderScaling))]
    [InlineData(nameof(LoupeOptions.ZoomLevel))]
    public void プロパティ変更がログへ記録される(string propertyName)
    {
        var logger = new RecordingLogger();
        var control = new LoupeControl { Log = logger };
        using var source = TestHelper.CreateBitmap(8, SKColors.Red);

        switch (propertyName)
        {
            case nameof(LoupeControl.Source):
                control.Source = source;
                break;
            case nameof(LoupeControl.CenterPoint):
                control.CenterPoint = new EsPoint(1, 2);
                break;
            case nameof(LoupeOptions.RequestedSize):
                control.Options = control.Options with { RequestedSize = 200 };
                break;
            case nameof(LoupeControl.RenderScaling):
                control.RenderScaling = 2.0;
                break;
            default:
                control.Options = control.Options with { ZoomLevel = 8 };
                break;
        }

        Assert.Contains(logger.Messages, message => message.Contains(propertyName, StringComparison.Ordinal));
    }

    /// <summary>変更ログに記録される値が補正後の値であることを検証する。</summary>
    [Fact]
    public void 変更ログは補正後の値を記録する()
    {
        var logger = new RecordingLogger();
        var control = new LoupeControl { Log = logger };

        control.Options = control.Options with { RequestedSize = 100_000 };

        Assert.Equal(LoupeValueCoercer.RequestedSizeMax, control.Options.RequestedSize);
        Assert.Contains(
            logger.Messages,
            message => message.Contains(LoupeValueCoercer.RequestedSizeMax.ToString(), StringComparison.Ordinal));
    }
}

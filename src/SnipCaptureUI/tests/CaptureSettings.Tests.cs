using Avalonia.Media;
using Estable.AvaloniaUI.Platform;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Estable.AvaloniaUI.SnipCaptureUI.Tests;

/// <summary><see cref="CaptureSettings"/> の単体テスト（検証・デフォルトマージ）。</summary>
[Collection("AvaloniaHeadless")]
public class CaptureSettingsTests
{
    [AvaloniaFact]
    public void Validate_範囲外の値で例外を投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(MinSelectionSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeZoomLevel: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeOffset: -1).Validate());
        // 追加メンバー（DragRect / Loupe / FadeMask 外観）の範囲チェック
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(HandleSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(HandleHitRadius: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(MarkerLength: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeRenderScaling: 0.05).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeRenderScaling: 9.0).Validate());
    }

    [AvaloniaFact]
    public void Validate_正常な値では例外を投げない()
    {
        new CaptureSettings(MinSelectionSize: 10, LoupeSize: 160, LoupeZoomLevel: 4).Validate();
        new CaptureSettings(HandleSize: 10, HandleHitRadius: 12, MarkerLength: 16,
            LoupeRenderScaling: 1.0, DisplayScaleScaling: 0).Validate();
    }

    [AvaloniaFact]
    public void ToNonNull_デフォルト値で埋める()
    {
        var s = new CaptureSettings().ToNonNull();

        Assert.Equal(6, s.MinSelectionSize);
        Assert.Equal(160, s.LoupeSize);
        Assert.Equal(4, s.LoupeZoomLevel);
        // 枠線の既定は DarkGray（設定画面の選択枠の色と一致）
        Assert.Equal(Color.FromAColor(0xFF, Colors.DarkGray), s.BorderColor);
        Assert.NotNull(s.WindowCaptureMode);
        Assert.True(s.LoupeVisible);
        // 追加メンバーのデフォルト値
        Assert.Equal(Colors.White, s.HandleColor);
        Assert.Equal(10.0, s.HandleSize);
        Assert.Equal(12.0, s.HandleHitRadius);
        Assert.True(s.ClampToDomainBounds);
        Assert.Equal(Colors.White, s.MarkerColor);
        Assert.Equal(16.0, s.MarkerLength);
        Assert.Equal(1.0, s.LoupeRenderScaling);
        // 市松模様の既定は Gray 30%（#4C808080）/ DarkGray 30%（#4CA9A9A9）（設定画面の現在値）
        Assert.Equal(Color.FromAColor(0x4C, Colors.Gray), s.LoupeCheckerColorA);
        Assert.Equal(Color.FromAColor(0x4C, Colors.DarkGray), s.LoupeCheckerColorB);
        // 選択矩形破線の既定は白 / 黒（アルファ 255）
        Assert.Equal(Colors.White, s.LoupeSelectionRectColorA);
        Assert.Equal(Colors.Black, s.LoupeSelectionRectColorB);
        Assert.True(s.SizeLabelVisible);
        Assert.True(s.InfoLabelVisible);
        Assert.Null(s.SizeLabelOverride);
        Assert.Equal(0.0, s.DisplayScaleScaling);
    }

    [AvaloniaFact]
    public void ToNonNull_指定した値を維持する()
    {
        var s = new CaptureSettings(MinSelectionSize: 25, LoupeSize: 200, BorderColor: Colors.Red,
            HandleColor: Colors.Blue, HandleSize: 14.0, ClampToDomainBounds: true,
            LoupeRenderScaling: 2.0, SizeLabelVisible: false, InfoLabelVisible: false).ToNonNull();

        Assert.Equal(25, s.MinSelectionSize);
        Assert.Equal(200, s.LoupeSize);
        Assert.Equal(Colors.Red, s.BorderColor);
        Assert.Equal(Colors.Blue, s.HandleColor);
        Assert.Equal(14.0, s.HandleSize);
        Assert.True(s.ClampToDomainBounds);
        Assert.Equal(2.0, s.LoupeRenderScaling);
        Assert.False(s.SizeLabelVisible);
        Assert.False(s.InfoLabelVisible);
        Assert.Equal(160, new CaptureSettings().ToNonNull().LoupeSize);
    }
}


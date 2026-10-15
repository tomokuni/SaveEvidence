using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="CaptureSettingsMapper"/> の単体テスト（設定 → 撮影スタックの外観設定）。</summary>
/// <remarks>
/// <b>本テストが本プロジェクトにある理由</b>: 対象の型はコア（<c>App.Core</c>）が単一所有するが、<br/>
/// その検証は <c>App.Core.Tests</c> に無い。 本アプリの撮影設定の写像（色・描画スケール）を保証するため、<br/>
/// 本プロジェクト側で検証する。<br/>
/// ウィンドウを必要としない純粋関数のためヘッドレスで検証できる。<br/>
/// 既定値では一致しない値（色・数値・真偽値）と、実行時に決まる値を固定する。
/// </remarks>
public class CaptureSettingsMapperTests
{
    /// <summary>設定の値が外観設定へそのまま写ることを検証する。</summary>
    [Fact]
    internal void 設定の値が外観設定へ写る()
    {
        var settings = new Settings
        {
            HideMainWindowOnCapture = false,
            MinSelectionSize = 12,
            CaptureBorderColor = "#80112233",
            CaptureBorderThickness = 3.5,
            ClampToSourceBounds = false,
            LoupeSize = 200,
            LoupeZoomLevel = 8,
            LoupeFrameWidth = 4,
            SizeLabelVisible = false,
            ShowSubLines = false,
            InfoLabelVisible = false,
        };

        var result = CaptureSettingsMapper.From(settings, 1.25);

        Assert.Equal(false, result.HideOwnerWindow);
        Assert.Equal(12, result.MinSelectionSize);
        Assert.Equal(3.5, result.BorderThickness);
        Assert.Equal(false, result.ClampToSourceBounds);
        Assert.Equal(200, result.LoupeSize);
        Assert.Equal(8, result.LoupeZoomLevel);
        Assert.Equal(4, result.LoupeFrameThickness);
        Assert.Equal(false, result.SizeLabelVisible);
        Assert.Equal(false, result.ShowSubLines);
        Assert.Equal(false, result.InfoLabelVisible);
        Assert.Equal((byte)0x80, result.BorderColor!.Value.A);
        Assert.Equal((byte)0x11, result.BorderColor!.Value.R);
        Assert.Equal((byte)0x22, result.BorderColor!.Value.G);
        Assert.Equal((byte)0x33, result.BorderColor!.Value.B);
    }

    /// <summary>描画スケールがルーペの倍率へ写り、座標ラベルの倍率は自動（0）になることを検証する。</summary>
    /// <param name="scale">描画スケール。</param>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(2.0)]
    internal void 描画スケールがルーペの倍率へ写る(double scale)
    {
        var result = CaptureSettingsMapper.From(new Settings(), scale);

        Assert.Equal(scale, result.LoupeClientToPixelScaling);
        Assert.Equal(0, result.DisplayScaleScaling);
    }

    /// <summary>解析できない色は透明として扱われることを検証する。</summary>
    [Fact]
    internal void 解析できない色は透明になる()
    {
        var settings = new Settings { CaptureBorderColor = "not-a-color" };

        var result = CaptureSettingsMapper.From(settings, 1.0);

        Assert.Equal((byte)0, result.BorderColor!.Value.A);
    }

    /// <summary>サイズ ラベルの文字色が「キャプチャ文字色」と共用されることを検証する。</summary>
    [Fact]
    internal void サイズラベルの文字色はキャプチャ文字色と共用する()
    {
        var settings = new Settings { CaptureTextColor = "#FF445566" };

        var result = CaptureSettingsMapper.From(settings, 1.0);

        Assert.Equal((byte)0x44, result.SizeLabelColor!.Value.R);
        Assert.Equal((byte)0x55, result.SizeLabelColor!.Value.G);
        Assert.Equal((byte)0x66, result.SizeLabelColor!.Value.B);
    }
}
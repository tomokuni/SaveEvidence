using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary><see cref="CaptureSettingsMapper"/>（<c>App.Core</c> の単一所有）の写像を検証するテスト。</summary>
/// <remarks>
/// <b>本テストが本プロジェクトにある理由</b>: 対象の型は <c>App.Core</c> の <c>internal</c> 型であり、<br/>
/// その写像を検証するテストは <c>App.Core.Tests</c> に存在しない。 本プロジェクトは撮影セッションへ設定を<br/>
/// 渡す唯一の利用者であるため、<b>利用側の責任で写像と色の解析を固定する</b><br/>
/// （<c>App.Core.Tests</c> へ同等のテストが追加された場合は、本ファイルを削除できる）。<br/>
/// 色の文字列の桁数の境界は <c>App.Core</c> 側の解析が持ち、<c>SettingsColorMath</c> のテストが<br/>
/// 同じ規則（8 桁 = ARGB / 6 桁 = RGB・アルファ 255 / 解析不可はフォールバック）を固定する。
/// </remarks>
public class CaptureSettingsMapperTests
{
    /// <summary>代表的な設定が撮影設定へ写ることを検証する。</summary>
    [Fact]
    public void 設定が撮影設定へ写る()
    {
        var settings = new Settings
        {
            HideMainWindowOnCapture = false,
            MinSelectionSize = 12,
            CaptureBorderThickness = 3.5,
            HandleSize = 14,
            LoupeSize = 256,
            LoupeZoomLevel = 8,
            ClampToSourceBounds = false,
            ShowCoordLabel = true,
            SizeLabelVisible = false,
        };

        var capture = CaptureSettingsMapper.From(settings, 1.5);

        Assert.False(capture.HideOwnerWindow);
        Assert.Equal(12, capture.MinSelectionSize);
        Assert.Equal(3.5, capture.BorderThickness);
        Assert.Equal(14, capture.HandleSize);
        Assert.Equal(256, capture.LoupeSize);
        Assert.Equal(8, capture.LoupeZoomLevel);
        Assert.False(capture.ClampToSourceBounds);
        Assert.True(capture.ShowCoordLabel);
        Assert.False(capture.SizeLabelVisible);
        Assert.Equal(1.5, capture.LoupeClientToPixelScaling);
    }

    /// <summary>色の文字列が色へ変換されることを検証する。</summary>
    [Fact]
    public void 色の文字列が色へ変換される()
    {
        var settings = new Settings { CaptureBorderColor = "#FF102030" };

        var capture = CaptureSettingsMapper.From(settings, 1.0);

        Assert.Equal(Color.FromArgb(0xFF, 0x10, 0x20, 0x30), capture.BorderColor);
    }

    /// <summary>解析できない色は透明へフォールバックすることを検証する。</summary>
    [Fact]
    public void 解析できない色は透明へフォールバックする()
    {
        var settings = new Settings { CaptureBorderColor = "not-a-color" };

        var capture = CaptureSettingsMapper.From(settings, 1.0);

        Assert.Equal(Color.Transparent, capture.BorderColor);
    }

    /// <summary>描画スケールがルーペの倍率として写り、表示倍率は自動（0）になることを検証する。</summary>
    [Fact]
    public void 描画スケールがルーペの倍率として写る()
    {
        var capture = CaptureSettingsMapper.From(new Settings(), 2.0);

        Assert.Equal(2.0, capture.LoupeClientToPixelScaling);
        Assert.Equal(0, capture.DisplayScaleScaling);
    }
}

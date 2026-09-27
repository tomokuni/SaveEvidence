using System.Drawing;
using Xunit;

namespace EsUtil.SaveEvidence.CaptureSession.Tests;

/// <summary><see cref="CaptureSettings"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値の検証（<see cref="CaptureSettings.Validate"/>）と、null をデフォルトで埋める規則<br/>
/// （<see cref="CaptureSettings.ToNonNull"/>）を固定する。UI 初期化は不要なため <c>[Fact]</c> で並列実行する。
/// </remarks>
public class CaptureSettingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MinSelectionSizeが1未満で例外を投げる(int value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(MinSelectionSize: value).Validate());

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ルーペのサイズが1未満で例外を投げる(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeSize: value).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeFrameThickness: value).Validate());
    }

    [Fact]
    public void Validate_ルーペの倍率が1未満で例外を投げる()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeZoomLevel: 0).Validate());

    [Fact]
    public void Validate_ルーペのオフセットが負で例外を投げる()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeOffset: -1).Validate());

    [Fact]
    public void Validate_操作部品のサイズが1未満で例外を投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(HandleSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(HandleHitRadius: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(MarkerLength: 0).Validate());
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(9.0)]
    public void Validate_ルーペの描画倍率が許容範囲外で例外を投げる(double value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(LoupeDipToDeviceScaling: value).Validate());

    [Fact]
    public void Validate_枠線の太さが負で例外を投げる()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureSettings(BorderThickness: -1).Validate());

    [Fact]
    public void Validate_nullの項目は検証しない()
    {
        // null は「現在値の維持」を表すため検証対象外（部分更新の入力として使える）
        new CaptureSettings().Validate();
    }

    [Fact]
    public void Validate_境界値は例外を投げない()
    {
        // 1 は下限ちょうど・0 は下限ちょうど（0 以上が許容される項目）
        new CaptureSettings(
            MinSelectionSize: 1, LoupeSize: 1, LoupeZoomLevel: 1, LoupeFrameThickness: 1,
            HandleSize: 1, HandleHitRadius: 1, MarkerLength: 1, BorderThickness: 0, LoupeOffset: 0,
            LoupeDipToDeviceScaling: 0.1).Validate();
    }

    [Fact]
    public void ToNonNull_未指定の項目をデフォルトで埋める()
    {
        var s = new CaptureSettings().ToNonNull();

        Assert.Equal(6, s.MinSelectionSize);
        Assert.Equal(160, s.LoupeSize);
        Assert.Equal(4, s.LoupeZoomLevel);
        Assert.Equal(20.0, s.LoupeOffset);
        Assert.True(s.HideOwnerWindow);
        Assert.Equal(0.0, s.DisplayScaleScaling);
    }

    [Fact]
    public void ToNonNull_外観の既定値は設定画面の現在値と一致する()
    {
        // 既定値は設定画面（app）の現在値に合わせる（UI ごとに変わらない）
        var s = new CaptureSettings().ToNonNull();

        Assert.Equal(Color.FromArgb(0xFF, 169, 169, 169), s.BorderColor);
        Assert.Equal(2.0, s.BorderThickness);
        Assert.Equal(10.0, s.HandleSize);
        Assert.Equal(12.0, s.HandleHitRadius);
        Assert.True(s.ClampToDomainBounds);
        Assert.Equal(16.0, s.MarkerLength);
        Assert.Equal(1.0, s.LoupeDipToDeviceScaling);
        Assert.Equal(Color.FromArgb(0xFF, 0x53, 0x53, 0x53), s.LoupePasteboardColor);
        Assert.Equal(Color.White, s.LoupeSelectionRectColorA);
        Assert.Equal(Color.Black, s.LoupeSelectionRectColorB);
        Assert.True(s.SizeLabelVisible);
        Assert.True(s.InfoLabelVisible);
        Assert.True(s.ShowSubLines);
    }

    [Fact]
    public void ToNonNull_指定した値は維持する()
    {
        var s = new CaptureSettings(
            MinSelectionSize: 25, LoupeSize: 200, BorderColor: Color.Red,
            HandleColor: Color.Blue, HandleSize: 14.0, ClampToDomainBounds: true,
            LoupeDipToDeviceScaling: 2.0, SizeLabelVisible: false, InfoLabelVisible: false).ToNonNull();

        Assert.Equal(25, s.MinSelectionSize);
        Assert.Equal(200, s.LoupeSize);
        Assert.Equal(Color.Red, s.BorderColor);
        Assert.Equal(14.0, s.HandleSize);
        Assert.True(s.ClampToDomainBounds);
        Assert.Equal(2.0, s.LoupeDipToDeviceScaling);
        Assert.False(s.SizeLabelVisible);
        Assert.False(s.InfoLabelVisible);
        // 指定していない項目は既定値のまま
        Assert.Equal(12.0, s.CoordLabelFontSize);
    }

    [Fact]
    public void ToNonNull_複数回呼んでも同じ既定値になる()
    {
        // 既定値は単一の所有者（ToNonNull）が持ち、呼び出しごとに変わらない
        Assert.Equal(new CaptureSettings().ToNonNull(), new CaptureSettings().ToNonNull());
    }
}

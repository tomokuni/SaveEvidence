using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.DragRect.Decoration.Tests;

/// <summary><see cref="OuterDecoLabels"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ラベル表示の判定と操作テキストの選定のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 文言は各 UI（Avalonia / MewUI）で共用する。
/// </remarks>
public class OuterDecoLabelsTests
{
    /// <summary>WindowSelect でタイトルがあり表示有効なら true を返すことを検証する。</summary>
    [Fact]
    public void ShouldShowInfoLabel_WindowSelectでタイトルがあり表示有効ならtrueを返す()
    {
        Assert.True(OuterDecoLabels.ShouldShowInfoLabel(OuterDecoRenderMode.WindowSelect, true, "メモ帳"));
    }

    /// <summary>WindowSelect 以外のモードでは false を返すことを検証する。</summary>
    [Theory]
    [InlineData(OuterDecoRenderMode.AreaSelect)]
    [InlineData(OuterDecoRenderMode.ScreenSelect)]
    [InlineData(OuterDecoRenderMode.Crop)]
    public void ShouldShowInfoLabel_モードがWindowSelect以外ならfalseを返す(OuterDecoRenderMode mode)
    {
        Assert.False(OuterDecoLabels.ShouldShowInfoLabel(mode, true, "メモ帳"));
    }

    /// <summary>表示が無効またはタイトルが空なら false を返すことを検証する。</summary>
    [Fact]
    public void ShouldShowInfoLabel_表示無効またはタイトル空ならfalseを返す()
    {
        Assert.False(OuterDecoLabels.ShouldShowInfoLabel(OuterDecoRenderMode.WindowSelect, false, "メモ帳"));
        Assert.False(OuterDecoLabels.ShouldShowInfoLabel(OuterDecoRenderMode.WindowSelect, true, ""));
    }

    /// <summary>スクリーン選択・ウィンドウ選択は Enter 系の操作ラベルを返すことを検証する。</summary>
    [Fact]
    public void GetOperationText_ScreenSelectとWindowSelectはEnter系を返す()
    {
        Assert.Equal(
            "Enter / クリック: 確定  Esc: キャンセル",
            OuterDecoLabels.GetOperationText(OuterDecoRenderMode.ScreenSelect));
        Assert.Equal(
            "Enter / クリック: 確定  Esc: キャンセル",
            OuterDecoLabels.GetOperationText(OuterDecoRenderMode.WindowSelect));
    }

    /// <summary>範囲選択・切出しはダブルクリック系の操作ラベルを返すことを検証する。</summary>
    [Fact]
    public void GetOperationText_AreaSelectとCropはダブルクリック系を返す()
    {
        Assert.Equal(
            "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル",
            OuterDecoLabels.GetOperationText(OuterDecoRenderMode.AreaSelect));
        Assert.Equal(
            "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル",
            OuterDecoLabels.GetOperationText(OuterDecoRenderMode.Crop));
    }

    /// <summary>表示スケールが 0 以下の場合は代替値（座標換算の倍率）を使うことを検証する。</summary>
    /// <param name="displayScaling">指定された表示スケール（無効値）。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void ResolveDisplayScaling_0以下なら代替値を使う(double displayScaling)
    {
        Assert.Equal(1.25, OuterDecoLabels.ResolveDisplayScaling(displayScaling, scaling: 1.25));
    }

    /// <summary>表示スケールが正の値ならそのまま使うことを検証する。</summary>
    [Fact]
    public void ResolveDisplayScaling_正の値ならそのまま使う()
    {
        Assert.Equal(2.0, OuterDecoLabels.ResolveDisplayScaling(displayScaling: 2.0, scaling: 1.25));
    }

    /// <summary>サイズラベルが座標・サイズ・表示倍率を物理ピクセルで整形することを検証する。</summary>
    /// <remarks>切り捨てで整数化し、倍率は % で表示する（Avalonia / MewUI で同一の文言）。</remarks>
    [Fact]
    public void FormatSizeLabel_座標とサイズと倍率を整形する()
    {
        var text = OuterDecoLabels.FormatSizeLabel(new EsRect(10, 20, 100, 50), scaling: 1.0);

        Assert.Equal("(10, 20) - (110, 70),  (100×50), DisplayScale: (100%)", text);
    }

    /// <summary>DPI スケーリングを掛けて物理ピクセルへ変換することを検証する。</summary>
    [Theory]
    [InlineData(1.0, 100, "(10, 20) - (110, 70)")]
    [InlineData(1.25, 125, "(12, 25) - (137, 87)")]
    [InlineData(1.5, 150, "(15, 30) - (165, 105)")]
    public void FormatSizeLabel_スケーリングを掛けて物理ピクセルへ変換する(double scaling, int expectedPercent, string expectedRange)
    {
        var text = OuterDecoLabels.FormatSizeLabel(new EsRect(10, 20, 100, 50), scaling);

        Assert.Contains(expectedRange, text, StringComparison.Ordinal);
        Assert.Contains($"DisplayScale: ({expectedPercent}%)", text, StringComparison.Ordinal);
    }

    /// <summary>スケーリングが 0 以下の場合は 1.0 として扱うことを検証する。</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void FormatSizeLabel_スケーリングが0以下なら等倍として扱う(double scaling)
    {
        var text = OuterDecoLabels.FormatSizeLabel(new EsRect(10, 20, 100, 50), scaling);

        Assert.Equal("(10, 20) - (110, 70),  (100×50), DisplayScale: (100%)", text);
    }

    /// <summary>DisplayScale 用の係数を指定すると、座標は画像ピクセルのまま百分率だけ実表示スケールになることを検証する。</summary>
    /// <remarks>画像ピクセル（ドメイン座標）の矩形に、画面の実表示スケール（例: 125%）を表示する用途。</remarks>
    [Fact]
    public void FormatSizeLabel_表示スケールを指定すると百分率だけが変わる()
    {
        var text = OuterDecoLabels.FormatSizeLabel(new EsRect(10, 20, 100, 50), scaling: 1.0, displayScaling: 1.25);

        Assert.Equal("(10, 20) - (110, 70),  (100×50), DisplayScale: (125%)", text);
    }

    /// <summary>DisplayScale 用の係数が未指定・0 以下の場合は座標換算の係数と同じ値を表示することを検証する。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void FormatSizeLabel_表示スケールが未指定なら座標換算の係数を使う(double? displayScaling)
    {
        var text = OuterDecoLabels.FormatSizeLabel(new EsRect(10, 20, 100, 50), scaling: 1.25, displayScaling: displayScaling);

        Assert.Equal("(12, 25) - (137, 87),  (125×62), DisplayScale: (125%)", text);
    }

    /// <summary>負のサイズ（退化した矩形）でも例外にせず整形することを検証する。</summary>
    [Fact]
    public void FormatSizeLabel_退化した矩形でも例外にしない()
    {
        var text = OuterDecoLabels.FormatSizeLabel(new EsRect(5, 5, -10, -20), scaling: 1.0);

        Assert.Equal("(5, 5) - (-5, -15),  (-10×-20), DisplayScale: (100%)", text);
    }
}

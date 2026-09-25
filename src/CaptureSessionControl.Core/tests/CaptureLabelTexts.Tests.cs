using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.FadeMaskDragRect;
using Xunit;

namespace EsUtil.SaveEvidence.CaptureSession.Tests;

/// <summary><see cref="CaptureLabelTexts"/> の単体テスト（モード写像・操作ラベル・情報ラベル）。</summary>
/// <remarks>
/// キャプチャ固有の文言は本クラスが単一所有するため、両 UI のホストが使う文言と表示可否をここで固定する。<br/>
/// 文言は UI 非依存のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class CaptureLabelTextsTests
{
    // ====================================================================
    // モード写像（キャプチャ種別 → 動作モード）
    // ====================================================================

    /// <summary>キャプチャ種別が対応する動作モードへ変換されることを検証する。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="expected">期待する動作モード。</param>
    [Theory]
    [InlineData(CaptureType.ScreenSelect, FadeMaskDragRectMode.ScreenSelect)]
    [InlineData(CaptureType.WindowSelect, FadeMaskDragRectMode.WindowSelect)]
    [InlineData(CaptureType.AreaSelect, FadeMaskDragRectMode.AreaSelect)]
    public void ToSelectionMode_キャプチャ種別に対応するモードを返す(
        CaptureType captureType, FadeMaskDragRectMode expected)
        => Assert.Equal(expected, CaptureLabelTexts.ToSelectionMode(captureType));

    /// <summary>未定義の種別は範囲選択として扱うことを検証する。</summary>
    [Fact]
    public void ToSelectionMode_未定義の種別は範囲選択として扱う()
        => Assert.Equal(
            FadeMaskDragRectMode.AreaSelect, CaptureLabelTexts.ToSelectionMode((CaptureType)999));

    // ====================================================================
    // 操作ラベル
    // ====================================================================

    /// <summary>スクリーン選択・ウィンドウ選択は Enter 系の操作ラベルを返すことを検証する。</summary>
    [Fact]
    public void GetOperationText_ScreenSelectとWindowSelectはEnter系を返す()
    {
        Assert.Equal(
            "Enter / クリック: 確定  Esc: キャンセル",
            CaptureLabelTexts.GetOperationText(FadeMaskDragRectMode.ScreenSelect));
        Assert.Equal(
            "Enter / クリック: 確定  Esc: キャンセル",
            CaptureLabelTexts.GetOperationText(FadeMaskDragRectMode.WindowSelect));
    }

    /// <summary>範囲選択・切出しはダブルクリック系の操作ラベルを返すことを検証する。</summary>
    [Fact]
    public void GetOperationText_AreaSelectとCropはダブルクリック系を返す()
    {
        Assert.Equal(
            "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル",
            CaptureLabelTexts.GetOperationText(FadeMaskDragRectMode.AreaSelect));
        Assert.Equal(
            "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル",
            CaptureLabelTexts.GetOperationText(FadeMaskDragRectMode.Crop));
    }

    // ====================================================================
    // 情報ラベル
    // ====================================================================

    /// <summary>ウィンドウ選択ではウィンドウタイトルをそのまま返すことを検証する。</summary>
    [Fact]
    public void GetInfoText_WindowSelectはウィンドウタイトルを返す()
        => Assert.Equal(
            "メモ帳", CaptureLabelTexts.GetInfoText(FadeMaskDragRectMode.WindowSelect, "メモ帳"));

    /// <summary>ウィンドウ選択以外では情報ラベルを返さないことを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop)]
    [InlineData(FadeMaskDragRectMode.AreaSelect)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect)]
    public void GetInfoText_WindowSelect以外はnullを返す(FadeMaskDragRectMode mode)
        => Assert.Null(CaptureLabelTexts.GetInfoText(mode, "メモ帳"));

    // ====================================================================
    // サイズラベルの文言
    // ====================================================================

    /// <summary>確定対象がある場合は矩形（ドメイン座標）からラベルを生成することを検証する。</summary>
    [Fact]
    public void ResolveSizeLabel_確定対象があれば矩形から生成する()
    {
        var label = CaptureLabelTexts.ResolveSizeLabel(
            new EsRect(0, 0, 100, 50), overrideText: "指定文言", displayScaling: _ => 1.0);

        // 既定の書式（MarqueeSizeLabel.Format）で生成される（明示指定は使わない）
        Assert.NotNull(label);
        Assert.NotEqual("指定文言", label);
    }

    /// <summary>確定対象が無い場合はホスト指定の文言を使うことを検証する。</summary>
    [Fact]
    public void ResolveSizeLabel_確定対象が無ければホスト指定の文言を返す()
    {
        var label = CaptureLabelTexts.ResolveSizeLabel(null, overrideText: "指定文言", displayScaling: _ => 1.0);

        Assert.Equal("指定文言", label);
    }

    /// <summary>確定対象が無く明示指定も無い場合は null（自動生成に委ねる）を返すことを検証する。</summary>
    [Fact]
    public void ResolveSizeLabel_確定対象も明示指定も無ければnullを返す()
        => Assert.Null(CaptureLabelTexts.ResolveSizeLabel(null, overrideText: null, displayScaling: _ => 1.0));

    /// <summary>表示倍率のデリゲートは確定対象がある場合のみ評価されることを検証する。</summary>
    [Fact]
    public void ResolveSizeLabel_表示倍率は確定対象がある場合のみ評価する()
    {
        // 無駄な計算（モニター判定）を行わないことを固定する
        var called = 0;

        CaptureLabelTexts.ResolveSizeLabel(null, overrideText: null, displayScaling: _ => { called++; return 1.0; });

        Assert.Equal(0, called);
    }

    /// <summary>表示倍率のデリゲートが null の場合は例外を投げることを検証する。</summary>
    [Fact]
    public void ResolveSizeLabel_表示倍率がnullなら例外を投げる()
        => Assert.Throws<ArgumentNullException>(
            () => CaptureLabelTexts.ResolveSizeLabel(null, overrideText: null, displayScaling: null!));
}

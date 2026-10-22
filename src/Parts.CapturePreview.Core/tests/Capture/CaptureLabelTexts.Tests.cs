using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Capture;

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
    [InlineData(CaptureType.ScreenSelect, SelectionOverlayMode.ScreenSelect)]
    [InlineData(CaptureType.WindowSelect, SelectionOverlayMode.WindowSelect)]
    [InlineData(CaptureType.AreaSelect, SelectionOverlayMode.AreaSelect)]
    internal void ToSelectionMode_キャプチャ種別に対応するモードを返す(
        CaptureType captureType, SelectionOverlayMode expected)
        => Assert.Equal(expected, CaptureLabelTexts.ToSelectionMode(captureType));

    /// <summary>未定義の種別は範囲選択として扱うことを検証する。</summary>
    [Fact]
    internal void ToSelectionMode_未定義の種別は範囲選択として扱う()
        => Assert.Equal(
            SelectionOverlayMode.AreaSelect, CaptureLabelTexts.ToSelectionMode((CaptureType)999));

    // ====================================================================
    // 操作ラベル
    // ====================================================================

    /// <summary>スクリーン選択・ウィンドウ選択は Enter 系の操作ラベルを返すことを検証する。</summary>
    [Fact]
    internal void GetOperationText_ScreenSelectとWindowSelectはEnter系を返す()
    {
        Assert.Equal(
            "Enter / クリック: 確定  Esc: キャンセル",
            CaptureLabelTexts.GetOperationText(SelectionOverlayMode.ScreenSelect));
        Assert.Equal(
            "Enter / クリック: 確定  Esc: キャンセル",
            CaptureLabelTexts.GetOperationText(SelectionOverlayMode.WindowSelect));
    }

    /// <summary>範囲選択・切出しはダブルクリック系の操作ラベルを返すことを検証する。</summary>
    [Fact]
    internal void GetOperationText_AreaSelectとCropはダブルクリック系を返す()
    {
        Assert.Equal(
            "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル",
            CaptureLabelTexts.GetOperationText(SelectionOverlayMode.AreaSelect));
        Assert.Equal(
            "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル",
            CaptureLabelTexts.GetOperationText(SelectionOverlayMode.Crop));
    }

    // ====================================================================
    // 情報ラベル
    // ====================================================================

    /// <summary>ウィンドウ選択ではウィンドウタイトルをそのまま返すことを検証する。</summary>
    [Fact]
    internal void GetInfoText_WindowSelectはウィンドウタイトルを返す()
        => Assert.Equal(
            "メモ帳", CaptureLabelTexts.GetInfoText(SelectionOverlayMode.WindowSelect, "メモ帳"));

    /// <summary>ウィンドウ選択以外では情報ラベルを返さないことを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    [Theory]
    [InlineData(SelectionOverlayMode.Crop)]
    [InlineData(SelectionOverlayMode.AreaSelect)]
    [InlineData(SelectionOverlayMode.ScreenSelect)]
    internal void GetInfoText_WindowSelect以外はnullを返す(SelectionOverlayMode mode)
        => Assert.Null(CaptureLabelTexts.GetInfoText(mode, "メモ帳"));

}

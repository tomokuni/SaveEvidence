using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Session;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Tests.Session;

/// <summary>Parts.CapturePreview.ControlMewUI の Facade（<see cref="CaptureSessionControl"/>）の状態テスト。</summary>
/// <remarks>
/// 対象: コンストラクタ・<see cref="CaptureSessionControl.AppearanceSettings"/>・<see cref="CaptureSessionControl.Dispose"/>・<br/>
/// <see cref="CaptureSessionControl.Confirm"/> / <see cref="CaptureSessionControl.Cancel"/> /<br/>
/// <see cref="CaptureSessionControl.IsCropping"/> / <see cref="CaptureSessionControl.ResolveCropAction"/>（セッションなし）。<br/>
/// コンストラクタは撮影サービス（<c>WinCaptureService</c> / <c>WinElementFinder</c> / <c>WinWindowService</c>。<br/>
/// いずれもロガーの保持のみで P/Invoke を実行しない）を生成するため、ウィンドウ生成なしで実行できる。<br/>
/// ウィンドウ・要素を生成するテスト（切り出しセッションの実行）は <c>[Collection("MewUI")]</c> の対象とする。<br/>
/// 切り出し範囲の計算（閉区間化・クランプ・丸め・最小サイズ）は UI 非依存の純粋関数
/// <see cref="CropBoundsMath"/> としてコアへ集約したため、<c>Parts.CapturePreview.Core.Tests</c> が検証する。
/// </remarks>
[Collection("MewUI")]
public class CaptureSessionControlStateTests
{
    // ====================================================================
    // CaptureSessionControl（コンストラクタ・設定・ライフサイクル）
    // ====================================================================

    [Fact]
    internal void コンストラクタ_デフォルト設定を適用する()
    {
        var capture = new CaptureSessionControl();

        Assert.Equal(6, capture.AppearanceSettings.MinSelectionSize);
        Assert.Equal(160, capture.AppearanceSettings.LoupeSize);
        Assert.True(capture.AppearanceSettings.HideOwnerWindow);
    }

    [Fact]
    internal void AppearanceSettings_部分更新でnull以外を反映する()
    {
        var capture = new CaptureSessionControl();
        var original = capture.AppearanceSettings;

        capture.AppearanceSettings = new CaptureSettings(LoupeSize: 200);

        Assert.Equal(200, capture.AppearanceSettings.LoupeSize);
        Assert.Equal(original.MinSelectionSize, capture.AppearanceSettings.MinSelectionSize); // 未指定は維持
    }

    [Fact]
    internal void AppearanceSettings_同じ値の再設定では更新をスキップする()
    {
        var capture = new CaptureSessionControl();
        var current = capture.AppearanceSettings;

        // 現在値と同じ値の設定（全メンバー指定）を再適用しても、参照が変わらない（同一値スキップ）
        var same = new CaptureSettings(
            MinSelectionSize: current.MinSelectionSize,
            LoupeSize: current.LoupeSize,
            LoupeZoomLevel: current.LoupeZoomLevel,
            BorderColor: current.BorderColor,
            OverlayColor: current.OverlayColor);
        capture.AppearanceSettings = same;

        Assert.Same(current, capture.AppearanceSettings);
    }

    [Fact]
    internal void AppearanceSettings_範囲外の値で例外を投げる()
    {
        var capture = new CaptureSessionControl();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => capture.AppearanceSettings = new CaptureSettings(LoupeZoomLevel: 0));
    }

    [Fact]
    internal void AppearanceSettings_破棄後は例外を投げる()
    {
        var capture = new CaptureSessionControl();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.AppearanceSettings = new CaptureSettings());
        Assert.Throws<ObjectDisposedException>(() => _ = capture.AppearanceSettings);
    }

    [Fact]
    internal void IsCropping_セッションなしではfalse()
    {
        var capture = new CaptureSessionControl();

        Assert.False(capture.IsCropping);
    }

    [Fact]
    internal void ResolveCropAction_セッションなしでは開始を返す()
    {
        var capture = new CaptureSessionControl();

        // 未開始なら開始（規則はコアの CropSessionActions が単一所有する）
        Assert.Equal(CropSessionAction.Start, capture.ResolveCropAction());
    }

    [Fact]
    internal void Confirm_セッションなしでは例外なく実行される()
    {
        var capture = new CaptureSessionControl();

        // 操作なし → 何もしない（例外なし）
        capture.Confirm();
    }

    [Fact]
    internal void Cancel_セッションなしでは例外なく実行される()
    {
        var capture = new CaptureSessionControl();

        // 操作なし → 何もしない（例外なし）
        capture.Cancel();
    }

    [Fact]
    internal void NotifyViewportChanged_セッションなしでは例外なく実行される()
    {
        var capture = new CaptureSessionControl();

        // 切り出し未実行 → 何もしない（例外なし）
        capture.NotifyViewportChanged();
    }

    [Fact]
    internal void Confirm_破棄後は例外を投げる()
    {
        var capture = new CaptureSessionControl();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.Confirm());
    }

    [Fact]
    internal void Cancel_破棄後は例外を投げる()
    {
        var capture = new CaptureSessionControl();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.Cancel());
    }

    [Fact]
    internal void Dispose_複数回呼んでも安全()
    {
        var capture = new CaptureSessionControl();

        capture.Dispose();
        capture.Dispose(); // 2 回目は何もしない
    }
}

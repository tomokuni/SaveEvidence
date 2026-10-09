using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests.Session;

/// <summary>Parts.CapturePreview.ControlAvaloniaUI のヘッドレス不要テスト（設定・ライフサイクル）。</summary>
/// <remarks>
/// 対象: <see cref="CaptureSessionControl"/> のコンストラクタ・<see cref="CaptureSessionControl.AppearanceSettings"/>・<br/>
/// <see cref="CaptureSessionControl.Dispose"/>・<see cref="CaptureSessionControl.Confirm"/> / <see cref="CaptureSessionControl.Cancel"/>（セッションなし）。<br/>
/// <see cref="CaptureSessionControl"/> のコンストラクタは <c>WinWindowService</c>（ロガー設定のみ・P/Invoke を実行しない）を<br/>
/// 生成するため、<c>[AvaloniaFact]</c>（ヘッドレス）を必要とせず <c>[Fact]</c> で並列実行できる。<br/>
/// ヘッドレス必須のテスト（ウィンドウ生成・セッション実行）は <see cref="CaptureSessionControlTests"/> が直列コレクションで検証する。<br/>
/// 切り出し範囲の計算（閉区間化・クランプ・丸め・最小サイズ）は UI 非依存の純粋関数
/// <see cref="CropBoundsMath"/> としてコアへ集約したため、<c>Parts.CapturePreview.Core.Tests</c> が検証する。
/// </remarks>
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

        Assert.Throws<ArgumentOutOfRangeException>(() => capture.AppearanceSettings = new CaptureSettings(LoupeZoomLevel: 0));
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


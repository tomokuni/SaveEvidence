using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Session;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Tests.Session;

/// <summary>Facade（<see cref="CaptureSessionControl"/>）の状態テスト。</summary>
/// <remarks>
/// 対象: コンストラクタ・<see cref="CaptureSessionControl.AppearanceSettings"/>・<see cref="CaptureSessionControl.Dispose"/>・<br/>
/// <see cref="CaptureSessionControl.Confirm"/> / <see cref="CaptureSessionControl.Cancel"/> /<br/>
/// <see cref="CaptureSessionControl.IsCropping"/> / <see cref="CaptureSessionControl.ResolveCropAction"/>（セッションなし）。<br/>
/// コンストラクタは撮影サービス（<c>WinCaptureService</c> / <c>WinElementFinder</c> / <c>WinWindowService</c>）を生成するが、<br/>
/// いずれもロガーの保持のみで P/Invoke を実行しないため、ウィンドウ生成なしで実行できる。<br/>
/// 切り出し範囲の計算（閉区間化・クランプ・丸め・最小サイズ）は UI 非依存の純粋関数<br/>
/// <see cref="CropBoundsMath"/> としてコアへ集約したため、<c>Parts.CapturePreview.Core.Tests</c> が検証する。
/// </remarks>
public sealed class CaptureSessionControlStateTests
{
    /// <summary>コンストラクタが既定の設定を適用することを検証する。</summary>
    [Fact]
    internal void コンストラクタ_デフォルト設定を適用する()
    {
        using var capture = new CaptureSessionControl();

        var settings = capture.AppearanceSettings;
        Assert.True(settings.MinSelectionSize > 0);
        Assert.NotNull(settings.LoupeSize);
        Assert.NotNull(settings.HideOwnerWindow);
    }

    /// <summary>設定の部分更新が null 以外のメンバーだけを反映することを検証する。</summary>
    [Fact]
    internal void AppearanceSettings_部分更新でnull以外を反映する()
    {
        using var capture = new CaptureSessionControl();
        var original = capture.AppearanceSettings;

        capture.AppearanceSettings = new CaptureSettings(LoupeSize: 200);

        Assert.Equal(200, capture.AppearanceSettings.LoupeSize);
        Assert.Equal(original.MinSelectionSize, capture.AppearanceSettings.MinSelectionSize); // 未指定は維持
    }

    /// <summary>範囲外の設定値が例外になることを検証する。</summary>
    [Fact]
    internal void AppearanceSettings_範囲外の値で例外を投げる()
    {
        using var capture = new CaptureSessionControl();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => capture.AppearanceSettings = new CaptureSettings(LoupeZoomLevel: 0));
    }

    /// <summary>破棄後の設定の読み書きが例外になることを検証する。</summary>
    [Fact]
    internal void AppearanceSettings_破棄後は例外を投げる()
    {
        var capture = new CaptureSessionControl();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.AppearanceSettings = new CaptureSettings());
        Assert.Throws<ObjectDisposedException>(() => _ = capture.AppearanceSettings);
    }

    /// <summary>セッションがない間は切り出し中ではないことを検証する。</summary>
    [Fact]
    internal void IsCropping_セッションなしではfalse()
    {
        using var capture = new CaptureSessionControl();

        Assert.False(capture.IsCropping);
    }

    /// <summary>セッションがない間は「開始」を返すことを検証する。</summary>
    /// <remarks>規則（未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）はコアが単一所有する。</remarks>
    [Fact]
    internal void ResolveCropAction_セッションなしでは開始を返す()
    {
        using var capture = new CaptureSessionControl();

        Assert.Equal(CropSessionAction.Start, capture.ResolveCropAction());
    }

    /// <summary>セッションがない間の確定が例外なく受け付けられることを検証する。</summary>
    [Fact]
    internal void Confirm_セッションなしでは何もしない()
    {
        using var capture = new CaptureSessionControl();

        capture.Confirm();
    }

    /// <summary>セッションがない間のキャンセルが例外なく受け付けられることを検証する。</summary>
    [Fact]
    internal void Cancel_セッションなしでは何もしない()
    {
        using var capture = new CaptureSessionControl();

        capture.Cancel();
    }

    /// <summary>切り出し未実行のビューポート変化の通知が例外なく受け付けられることを検証する。</summary>
    [Fact]
    internal void NotifyViewportChanged_セッションなしでは何もしない()
    {
        using var capture = new CaptureSessionControl();

        capture.NotifyViewportChanged();
    }

    /// <summary>破棄後の確定・キャンセルが例外になることを検証する。</summary>
    [Fact]
    internal void 破棄後の操作は例外を投げる()
    {
        var capture = new CaptureSessionControl();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.Confirm());
        Assert.Throws<ObjectDisposedException>(() => capture.Cancel());
        Assert.Throws<ObjectDisposedException>(() => capture.NotifyViewportChanged());
    }

    /// <summary>破棄を複数回呼んでも安全であることを検証する。</summary>
    [Fact]
    internal void Dispose_複数回呼んでも安全()
    {
        var capture = new CaptureSessionControl();

        capture.Dispose();
        capture.Dispose();
    }
}

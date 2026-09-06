using Avalonia;
using Rect = Estable.Primitives.RectD;
using Size = Estable.Primitives.SizeD;
using Estable.Primitives;
using Xunit;

namespace Estable.AvaloniaUI.SnipCaptureUI.Tests;

/// <summary>SnipCaptureUI のヘッドレス不要テスト（純粋計算・設定・ライフサイクル）。</summary>
/// <remarks>
/// 対象: <see cref="CropSession.ComputeCropBounds"/>（切り出し範囲計算）と、<see cref="SnipCapture"/> の<br/>
/// コンストラクタ・<see cref="SnipCapture.AppearanceSettings"/>・<see cref="SnipCapture.Dispose"/>・<br/>
/// <see cref="SnipCapture.Confirm"/> / <see cref="SnipCapture.Cancel"/>（セッションなし）。<br/>
/// <see cref="SnipCapture"/> のコンストラクタは <c>PlatformFactory.CreateWindowService</c>（ロガー設定のみ・P/Invoke を実行しない）を<br/>
/// 生成するため、<c>[AvaloniaFact]</c>（ヘッドレス）を必要とせず <c>[Fact]</c> で並列実行できる。<br/>
/// ヘッドレス必須のテスト（ウィンドウ生成・セッション実行）は <see cref="SnipCaptureTests"/> が直列コレクションで検証する。
/// </remarks>
public class SnipCaptureStateTests
{
    // ====================================================================
    // ComputeCropBounds（選択矩形 → 閉区間切り出し範囲）
    // ====================================================================

    [Fact]
    public void ComputeCropBounds_閉区間で始点終点のピクセルを含む()
    {
        // 選択矩形 (10,10,40,40) = 半開区間 [10,50)。閉区間では [10,50] = 41×41 ピクセル
        var result = CropSession.ComputeCropBounds(new Rect(10, 10, 40, 40), new Size(100, 100), 1.0);

        Assert.Equal(new PixelRect(10, 10, 41, 41), result);
    }

    [Fact]
    public void ComputeCropBounds_画像右端では幅が画像サイズにクランプされる()
    {
        // 選択矩形が画像右端 (80,80,20,20) = [80,100)。閉区間では [80,100] = 21 ピクセルだが、
        // 画像サイズ 100 を超えないよう 20 ピクセルにクランプする
        var result = CropSession.ComputeCropBounds(new Rect(80, 80, 20, 20), new Size(100, 100), 1.0);

        Assert.Equal(new PixelRect(80, 80, 20, 20), result);
    }

    [Fact]
    public void ComputeCropBounds_最小サイズ未満は空を返す()
    {
        // 幅・高さが 1 未満（MinSelectionSize の既定値 1.0 未満）→ 空を返す
        var result = CropSession.ComputeCropBounds(new Rect(10, 10, 0.5, 0.5), new Size(100, 100), 1.0);

        Assert.Equal(default, result);
    }

    [Fact]
    public void ComputeCropBounds_負座標は0にクランプする()
    {
        // 選択矩形が画像外（負座標）→ 0 にクランプして閉区間で切り出す
        var result = CropSession.ComputeCropBounds(new Rect(-5, -5, 20, 20), new Size(100, 100), 1.0);

        Assert.Equal(new PixelRect(0, 0, 16, 16), result);
    }

    [Fact]
    public void ComputeCropBounds_完全に範囲外なら空を返す()
    {
        var result = CropSession.ComputeCropBounds(new Rect(150, 150, 20, 20), new Size(100, 100), 1.0);

        Assert.Equal(default, result);
    }

    [Fact]
    public void ComputeCropBounds_最小サイズを指定するとそれ未満を空とみなす()
    {
        // MinSelectionSize = 10 の場合、幅 5 は空を返す
        var result = CropSession.ComputeCropBounds(new Rect(10, 10, 5, 20), new Size(100, 100), 10.0);

        Assert.Equal(default, result);
    }

    // ====================================================================
    // SnipCapture（コンストラクタ・設定・ライフサイクル）
    // ====================================================================

    [Fact]
    public void コンストラクタ_デフォルト設定を適用する()
    {
        var capture = new SnipCapture();

        Assert.Equal(6, capture.AppearanceSettings.MinSelectionSize);
        Assert.Equal(160, capture.AppearanceSettings.LoupeSize);
        Assert.True(capture.AppearanceSettings.LoupeVisible);
    }

    [Fact]
    public void AppearanceSettings_部分更新でnull以外を反映する()
    {
        var capture = new SnipCapture();
        var original = capture.AppearanceSettings;

        capture.AppearanceSettings = new CaptureSettings(LoupeSize: 200);

        Assert.Equal(200, capture.AppearanceSettings.LoupeSize);
        Assert.Equal(original.MinSelectionSize, capture.AppearanceSettings.MinSelectionSize); // 未指定は維持
    }

    [Fact]
    public void AppearanceSettings_同じ値の再設定では更新をスキップする()
    {
        var capture = new SnipCapture();
        var current = capture.AppearanceSettings;

        // 現在値と同じ値の設定（全メンバー指定）を再適用しても、参照が変わらない（同一値スキップ）
        var same = new CaptureSettings(
            MinSelectionSize: current.MinSelectionSize,
            LoupeSize: current.LoupeSize,
            LoupeZoomLevel: current.LoupeZoomLevel,
            LoupeVisible: current.LoupeVisible,
            BorderColor: current.BorderColor,
            OverlayColor: current.OverlayColor);
        capture.AppearanceSettings = same;

        Assert.Same(current, capture.AppearanceSettings);
    }

    [Fact]
    public void AppearanceSettings_範囲外の値で例外を投げる()
    {
        var capture = new SnipCapture();

        Assert.Throws<ArgumentOutOfRangeException>(() => capture.AppearanceSettings = new CaptureSettings(LoupeZoomLevel: 0));
    }

    [Fact]
    public void AppearanceSettings_破棄後は例外を投げる()
    {
        var capture = new SnipCapture();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.AppearanceSettings = new CaptureSettings());
        Assert.Throws<ObjectDisposedException>(() => _ = capture.AppearanceSettings);
    }

    [Fact]
    public void Confirm_セッションなしでは例外なく実行される()
    {
        var capture = new SnipCapture();

        // 操作なし → 何もしない（例外なし）
        capture.Confirm();
    }

    [Fact]
    public void Cancel_セッションなしでは例外なく実行される()
    {
        var capture = new SnipCapture();

        // 操作なし → 何もしない（例外なし）
        capture.Cancel();
    }

    [Fact]
    public void Confirm_破棄後は例外を投げる()
    {
        var capture = new SnipCapture();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.Confirm());
    }

    [Fact]
    public void Cancel_破棄後は例外を投げる()
    {
        var capture = new SnipCapture();
        capture.Dispose();

        Assert.Throws<ObjectDisposedException>(() => capture.Cancel());
    }

    [Fact]
    public void Dispose_複数回呼んでも安全()
    {
        var capture = new SnipCapture();

        capture.Dispose();
        capture.Dispose(); // 2 回目は何もしない
    }
}

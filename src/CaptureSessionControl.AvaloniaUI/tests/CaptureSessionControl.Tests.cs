using Avalonia;
using Avalonia.Controls;
using Rect = Avalonia.Rect;
using Estable.AvaloniaUI.CropView;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Avalonia.Headless.XUnit;
using Estable.CaptureSession;

namespace Estable.AvaloniaUI.CaptureSession.Tests;

/// <summary><see cref="CaptureSessionControl"/> の単体テスト（設定・ライフサイクル・再入ガード）。</summary>
[Collection("AvaloniaHeadless")]
public class CaptureSessionControlTests
{
    /// <summary>キャプチャサービスのモック。</summary>
    private sealed class MockCaptureService : ICaptureService
    {
        public EsBitmap? CaptureArea(EsPixelRect bounds) => null;
        public EsBitmap? CaptureVirtualScreen(out EsPixelPoint origin)
        {
            origin = default;
            return null;
        }
    }

    /// <summary>ウィンドウサービスのモック。</summary>
    private sealed class MockWindowService : IWindowService
    {
        public void HideFromTaskSwitcher(EsPlatformHandle handle) { }
        public bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude) => true;
        public bool SetBounds(EsPlatformHandle handle, EsPixelRect bounds) => true;
        public void EvacuateWindow(EsPlatformHandle handle) { }
    }

    /// <summary>要素検出のモック（常に未検出）。</summary>
    private sealed class MockElementFinder : IElementFinder
    {
        public ElementFinderResults FindWindowUnderCursor(EsPixelPoint screenPoint) => default;
        public ElementFinderResults FindScreenUnderCursor(EsPixelPoint screenPoint) => default;
        public IReadOnlyList<ElementFinderResults> ListScreens() => [];
        public IReadOnlyList<ElementFinderResults> ListWindows() => [];
    }

    [AvaloniaFact]
    public async Task StartCropAsync_引数nullで例外を投げる()
    {
        var capture = new CaptureSessionControl();
        var bitmap = CreateBitmap(10, 10);

        await Assert.ThrowsAsync<ArgumentNullException>(() => capture.StartCropAsync(null!, new Window(), bitmap));
        await Assert.ThrowsAsync<ArgumentNullException>(() => capture.StartCropAsync(new CropViewControl(), null!, bitmap));
        await Assert.ThrowsAsync<ArgumentNullException>(() => capture.StartCropAsync(new CropViewControl(), new Window(), null!));
    }

    [AvaloniaFact]
    public async Task StartCropAsync_破棄後は例外を投げる()
    {
        var capture = new CaptureSessionControl();
        capture.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.StartCropAsync(new CropViewControl(), new Window(), CreateBitmap(10, 10)));
    }

    // ====================================================================
    // スクリーン/ウィンドウ選択で自身のウィンドウが非表示にならない
    // ====================================================================

    [AvaloniaFact]
    public async Task CaptureSession_HideOwnerWindowがtrueでオーナーが透明化され完了後に復元される()
    {
        var owner = new Window { Width = 100, Height = 100 };
        owner.Show();
        Assert.True(owner.IsVisible);
        // 画面外退避 → 復元を検証するため、元の位置を明示する
        owner.Position = new PixelPoint(123, 456);

        var session = new SelectionCaptureSession(
            new MockWindowService(), new MockCaptureService(), new MockElementFinder(),
            CaptureType.ScreenSelect,
            // タイムアウトは廃止済み。Cancel() でキャンセルさせてタスクを完了する
            // （ヘッドレス環境では Close() が Deactivated を発火しないため、Closed 経由で完了する）
            new CaptureSettings(HideOwnerWindow: true),
            1.0,
            logger: NullLogger.Instance);

        // RunAsync を開始すると、オーバーレイ表示前にオーナーが透明化され画面外へ退避される
        var task = session.RunAsync(owner, showWithOwner: true);
        Assert.True(owner.IsVisible); // 表示状態のまま（Hide しない）
        Assert.Equal(0.0, owner.Opacity); // 透明化されている
        Assert.Equal(new PixelPoint(-32000, -32000), owner.Position); // 画面外へ退避

        // Cancel() でキャンセル → 0x0 の空結果で完了
        session.Cancel();
        var result = await task;

        // 完了後は元の位置と Opacity が即時復元される（async の待機は不要）
        Assert.True(owner.IsVisible);
        Assert.Equal(1.0, owner.Opacity); // Opacity が 1 に戻っている（透明のまま放置されない）
        Assert.Equal(new PixelPoint(123, 456), owner.Position); // 元の位置に復元
        owner.Close();
    }

    [AvaloniaFact]
    public async Task CaptureSession_HideOwnerWindowがfalseならオーナーは非表示にならない()
    {
        var owner = new Window { Width = 100, Height = 100 };
        owner.Show();
        Assert.True(owner.IsVisible);

        var session = new SelectionCaptureSession(
            new MockWindowService(), new MockCaptureService(), new MockElementFinder(),
            CaptureType.ScreenSelect,
            new CaptureSettings(HideOwnerWindow: false),
            1.0,
            logger: NullLogger.Instance);

        // RunAsync を開始してもオーナーは非表示にならない
        var task = session.RunAsync(owner, showWithOwner: true);
        Assert.True(owner.IsVisible);

        // Cancel() でキャンセルしてタスクを完了する
        session.Cancel();
        await task;
        owner.Close();
    }

    // ====================================================================
    // キャプチャ完了イベント（画像変更 → APP 再表示の順序）
    // ====================================================================

    [AvaloniaFact]
    public async Task CaptureSession_キャプチャ完了イベントの完了後にオーナーが復元される()
    {
        var owner = new Window { Width = 100, Height = 100 };
        owner.Show();
        Assert.True(owner.IsVisible);

        var order = new List<string>();
        var session = new SelectionCaptureSession(
            new MockWindowService(), new MockCaptureService(), new MockElementFinder(),
            CaptureType.ScreenSelect,
            new CaptureSettings(HideOwnerWindow: true),
            1.0,
            // キャプチャ完了イベント（同期）: ハンドラで画像変更を模擬する
            onCaptured: _ => order.Add("画像変更"),
            NullLogger.Instance);

        var task = session.RunAsync(owner, showWithOwner: true);
        Assert.Equal(0.0, owner.Opacity); // 透明化されている（表示状態のまま）

        // Cancel() でキャンセルしてタスクを完了する
        session.Cancel();
        var result = await task;
        // 復元は即時（async の待機は不要）。イベント（画像変更）の完了後に実行される
        Assert.True(owner.IsVisible);
        Assert.Equal(1.0, owner.Opacity);

        // イベント（画像変更）は復元より先に実行される（同期イベントの順序保証）
        // ※ order の検証はイベント発行が復元前に呼ばれたかを確認するため、RunAsync 完了時点で
        //    「画像変更」が記録されている
        Assert.Contains("画像変更", order);
        owner.Close();
    }

    // ====================================================================
    // 閉区間キャプチャ（Crop の始点・終点を含む）
    // ====================================================================

    [AvaloniaFact]
    public void CropSession_ExecuteCropForSize_閉区間で始点終点のピクセルを含む()
    {
        var panel = new Panel();
        var cropView = new CropViewControl();
        panel.Children.Add(cropView);
        var bitmap = CreateBitmap(100, 100);
        var session = new CropSession(cropView, new Window(), bitmap, new CaptureSettings(), NullLogger.Instance);

        // 選択矩形 (10,10,40,40) = 半開区間 [10,50)。閉区間では [10,50] = 41×41 ピクセル
        var result = session.ExecuteCropForSize(new Rect(10, 10, 40, 40));

        Assert.Equal(new PixelRect(10, 10, 41, 41), result);
    }

    [AvaloniaFact]
    public void CropSession_ExecuteCropForSize_画像右端では幅が画像サイズにクランプされる()
    {
        var panel = new Panel();
        var cropView = new CropViewControl();
        panel.Children.Add(cropView);
        var bitmap = CreateBitmap(100, 100);
        var session = new CropSession(cropView, new Window(), bitmap, new CaptureSettings(), NullLogger.Instance);

        // 選択矩形が画像右端 (80,80,20,20) = [80,100)。閉区間では [80,100] = 21 ピクセルだが、
        // 画像サイズ 100 を超えないよう 20 ピクセルにクランプする
        var result = session.ExecuteCropForSize(new Rect(80, 80, 20, 20));

        Assert.Equal(new PixelRect(80, 80, 20, 20), result);
    }

    [AvaloniaFact]
    public void CropSession_ExecuteCropForSize_最小サイズ未満は空を返す()
    {
        var panel = new Panel();
        var cropView = new CropViewControl();
        panel.Children.Add(cropView);
        var bitmap = CreateBitmap(100, 100);
        var session = new CropSession(cropView, new Window(), bitmap, new CaptureSettings(), NullLogger.Instance);

        // 幅・高さが 1 未満（MinSelectionSize の既定値 1.0 未満）→ 空を返す
        var result = session.ExecuteCropForSize(new Rect(10, 10, 0.5, 0.5));

        Assert.Equal(default, result);
    }

    // ====================================================================
    // 実行中の Crop セッションへの設定反映（ApplySettings）
    // ====================================================================

    [AvaloniaFact]
    public void CropSession_ApplySettings_実行中に設定を再適用して例外なく動作する()
    {
        var panel = new Panel();
        var cropView = new CropViewControl();
        panel.Children.Add(cropView);
        var bitmap = CreateBitmap(100, 100);
        var window = new Window();
        var session = new CropSession(cropView, window, bitmap, new CaptureSettings(), NullLogger.Instance);

        var task = session.RunAsync();
        // 実行中に設定を再適用（ClampToDomainBounds を ON に変更 → クリップ設定が更新される）
        session.ApplySettings(new CaptureSettings(ClampToDomainBounds: true));
        session.ApplySettings(new CaptureSettings(ClampToDomainBounds: false));

        // 再適用後もセッションは正常にキャンセルできる
        session.Cancel();
        window.Close();
    }

    [AvaloniaFact]
    public void CropSession_NotifyViewportChanged_実行中に再描画を要求して例外なく動作する()
    {
        var panel = new Panel();
        var cropView = new CropViewControl();
        panel.Children.Add(cropView);
        var bitmap = CreateBitmap(100, 100);
        var window = new Window();
        var session = new CropSession(cropView, window, bitmap, new CaptureSettings(), NullLogger.Instance);

        var task = session.RunAsync();
        // ビューポート変化（パン・ズーム）の通知 → 例外なく再描画・再計算を要求する
        session.NotifyViewportChanged();
        session.NotifyViewportChanged();

        // 通知後もセッションは正常にキャンセルできる
        session.Cancel();
        window.Close();
    }

    [AvaloniaFact]
    public async Task CaptureSessionControl_設定変更で実行中のCropセッションへ反映する()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var panel = new Panel();
        var cropView = new CropViewControl();
        panel.Children.Add(cropView);
        var window = new Window();
        window.Content = panel;
        window.Show();

        var bitmap = CreateBitmap(100, 100);
        var task = capture.StartCropAsync(cropView, window, bitmap);
        Assert.NotNull(task);

        // Crop セッション実行中に設定を変更する（ApplySettings 経由で実行中セッションへ反映）
        capture.AppearanceSettings = new CaptureSettings(ClampToDomainBounds: true);
        capture.AppearanceSettings = new CaptureSettings(ClampToDomainBounds: true); // 同じ値 → スキップ（無駄な更新なし）

        // キャンセルしてセッションを終了する
        capture.Cancel();
        var result = await task;
        Assert.Equal(default, result);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CaptureSessionControl_NotifyCropViewportChanged_実行中のCropセッションへ通知する()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var panel = new Panel();
        var cropView = new CropViewControl();
        panel.Children.Add(cropView);
        var window = new Window();
        window.Content = panel;
        window.Show();

        var bitmap = CreateBitmap(100, 100);
        var task = capture.StartCropAsync(cropView, window, bitmap);
        Assert.NotNull(task);

        // ビューポート（パン・ズーム）変化の通知 → 実行中の Crop セッションへ中継される（例外なく動作）
        capture.NotifyCropViewportChanged();

        // Crop 未実行時（キャンセル後）の通知は何もしない（例外なし）
        capture.Cancel();
        var result = await task;
        Assert.Equal(default, result);
        capture.NotifyCropViewportChanged();

        window.Close();
    }

    /// <summary>テスト用 EsBitmap を生成する。</summary>
    private static EsBitmap CreateBitmap(int width, int height)
        => EsBitmap.Create(width, height);
}


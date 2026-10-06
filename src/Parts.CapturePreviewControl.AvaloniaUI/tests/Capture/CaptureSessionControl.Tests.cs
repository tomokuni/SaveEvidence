using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Rect = Avalonia.Rect;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Sessions;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests.Capture;

/// <summary><see cref="CaptureSessionControl"/> の単体テスト（設定・ライフサイクル・再入ガード）。</summary>
[Collection("AvaloniaHeadless")]
public class CaptureSessionControlTests
{
    /// <summary>キャプチャサービスのモック。</summary>
    private sealed class MockCaptureService : ICaptureService
    {
        public EsBitmap? CaptureArea(EsRectInt bounds) => null;
        public EsBitmap? CaptureVirtualScreen(out EsPointInt origin)
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
        public bool SetBounds(EsPlatformHandle handle, EsRectInt bounds) => true;
        public void EvacuateWindow(EsPlatformHandle handle) { }
    }

    /// <summary>要素検出のモック（常に未検出）。</summary>
    private sealed class MockElementFinder : IElementFinder
    {
        public ElementFinderResults FindWindowUnderCursor(EsPointInt screenPoint) => default;
        public ElementFinderResults FindScreenUnderCursor(EsPointInt screenPoint) => default;
        public IReadOnlyList<ElementFinderResults> ListScreens() => [];
        public IReadOnlyList<ElementFinderResults> ListWindows() => [];
    }

    [AvaloniaFact]
    internal async Task StartCropAsync_引数nullで例外を投げる()
    {
        var capture = new CaptureSessionControl();

        await Assert.ThrowsAsync<ArgumentNullException>(() => capture.StartCropAsync(null!, new Window()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => capture.StartCropAsync(new CropViewControl(), null!));
    }

    [AvaloniaFact]
    internal async Task StartCropAsync_破棄後は例外を投げる()
    {
        var capture = new CaptureSessionControl();
        capture.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.StartCropAsync(new CropViewControl(), new Window()));
    }

    /// <summary>切り出しの対象は「表示中の画像」（CropViewControl.Source）であり、画像が無い場合は開始しない。</summary>
    [AvaloniaFact]
    internal async Task StartCropAsync_画像が未設定なら開始せずnullを返す()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var window = new Window();

        // 画像なし（Source 未設定）では開始できないことを null の結果で表す（実行中の再呼び出しと同じ表現）
        EsRectInt? result = await capture.StartCropAsync(new CropViewControl(), window);

        Assert.Null(result);
        Assert.False(capture.IsCropping);
        window.Close();
    }

    // ====================================================================
    // スクリーン/ウィンドウ選択で自身のウィンドウが非表示にならない
    // ====================================================================

    [AvaloniaFact]
    internal async Task CaptureSession_HideOwnerWindowがtrueでオーナーが透明化され完了後に復元される()
    {
        var owner = new Window { Width = 100, Height = 100 };
        owner.Show();
        Assert.True(owner.IsVisible);
        // 画面外退避 → 復元を検証するため、元の位置を明示する
        owner.Position = new PixelPoint(123, 456);

        var session = new SelectionCaptureSession(
            new MockWindowService(), new MockCaptureService(), new MockElementFinder(),
            CaptureType.ScreenSelect,
            // タイムアウトは廃止済み。 Cancel() でキャンセルさせてタスクを完了する
            // （ヘッドレス環境では Close() が Deactivated を発火しないため、Closed 経由で完了する）
            new CaptureSettings(HideOwnerWindow: true),
            1.0,
            logger: NullLogger.Instance);

        // RunAsync を開始すると、オーバーレイ表示前にオーナーが透明化され画面外へ退避される
        var task = session.RunAsync(owner);
        Assert.True(owner.IsVisible); // 表示状態のまま（Hide しない）
        Assert.Equal(0.0, owner.Opacity); // 透明化されている
        Assert.Equal(new PixelPoint(-32000, -32000), owner.Position); // 画面外へ退避

        // Cancel() でキャンセル → 0x0 の空結果で完了
        session.Cancel();
        _ = await task;

        // 完了後は元の位置と Opacity が即時復元される（async の待機は不要）
        Assert.True(owner.IsVisible);
        Assert.Equal(1.0, owner.Opacity); // Opacity が 1 に戻っている（透明のまま放置されない）
        Assert.Equal(new PixelPoint(123, 456), owner.Position); // 元の位置に復元
        owner.Close();
    }

    [AvaloniaFact]
    internal async Task CaptureSession_HideOwnerWindowがfalseならオーナーは非表示にならない()
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
        var task = session.RunAsync(owner);
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
    internal async Task CaptureSession_キャプチャ完了イベントの完了後にオーナーが復元される()
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

        var task = session.RunAsync(owner);
        Assert.Equal(0.0, owner.Opacity); // 透明化されている（表示状態のまま）

        // Cancel() でキャンセルしてタスクを完了する
        session.Cancel();
        var result = await task;
        // 復元は即時（async の待機は不要）。 イベント（画像変更）の完了後に実行される
        Assert.True(owner.IsVisible);
        Assert.Equal(1.0, owner.Opacity);

        // イベント（画像変更）は復元より先に実行される（同期イベントの順序保証）
        // ※ order の検証はイベント発行が復元前に呼ばれたかを確認するため、RunAsync 完了時点で
        //    「画像変更」が記録されている
        Assert.Contains("画像変更", order);
        owner.Close();
    }

    // ====================================================================
    // 実行中の Crop セッションへの設定反映（ApplySettings）
    // ====================================================================

    [AvaloniaFact]
    internal void CropSession_ApplySettings_実行中に設定を再適用して例外なく動作する()
    {
        var panel = new Panel();
        var cropView = CreateCropView();
        panel.Children.Add(cropView);
        var window = new Window();
        var session = new CropSession(cropView, window, new CaptureSettings(), NullLogger.Instance);
        _ = session.RunAsync(cropView.CurrentImage()!);
        // 実行中に設定を再適用（ClampToSourceBounds を ON に変更 → クリップ設定が更新される）
        session.ApplySettings(new CaptureSettings(ClampToSourceBounds: true));
        session.ApplySettings(new CaptureSettings(ClampToSourceBounds: false));

        // 再適用後もセッションは正常にキャンセルできる
        session.Cancel();
        window.Close();
    }

    [AvaloniaFact]
    internal void CropSession_NotifyViewportChanged_実行中に再描画を要求して例外なく動作する()
    {
        var panel = new Panel();
        var cropView = CreateCropView();
        panel.Children.Add(cropView);
        var window = new Window();
        var session = new CropSession(cropView, window, new CaptureSettings(), NullLogger.Instance);
        _ = session.RunAsync(cropView.CurrentImage()!);
        // ビューポート変化（パン・ズーム）の通知 → 例外なく再描画・再計算を要求する
        session.NotifyViewportChanged();
        session.NotifyViewportChanged();

        // 通知後もセッションは正常にキャンセルできる
        session.Cancel();
        window.Close();
    }

    [AvaloniaFact]
    internal async Task CaptureSessionControl_設定変更で実行中のCropセッションへ反映する()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var panel = new Panel();
        var cropView = CreateCropView();
        panel.Children.Add(cropView);
        var window = new Window
        {
            Content = panel
        };
        window.Show();

        var task = capture.StartCropAsync(cropView, window);
        Assert.NotNull(task);

        // Crop セッション実行中に設定を変更する（ApplySettings 経由で実行中セッションへ反映）
        capture.AppearanceSettings = new CaptureSettings(ClampToSourceBounds: true);
        capture.AppearanceSettings = new CaptureSettings(ClampToSourceBounds: true); // 同じ値 → スキップ（無駄な更新なし）

        // キャンセルしてセッションを終了する
        capture.Cancel();
        var result = await task;
        // キャンセルは 0x0 の空矩形（公開契約）
        Assert.True(result!.Value.IsEmpty);

        window.Close();
    }

    [AvaloniaFact]
    internal async Task CaptureSessionControl_NotifyCropViewportChanged_実行中のCropセッションへ通知する()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var panel = new Panel();
        var cropView = CreateCropView();
        panel.Children.Add(cropView);
        var window = new Window
        {
            Content = panel
        };
        window.Show();

        var task = capture.StartCropAsync(cropView, window);
        Assert.NotNull(task);

        // ビューポート（パン・ズーム）変化の通知 → 実行中の Crop セッションへ中継される（例外なく動作）
        capture.NotifyCropViewportChanged();

        // Crop 未実行時（キャンセル後）の通知は何もしない（例外なし）
        capture.Cancel();
        var result = await task;
        // キャンセルは 0x0 の空矩形（公開契約）
        Assert.True(result!.Value.IsEmpty);
        capture.NotifyCropViewportChanged();

        window.Close();
    }

    /// <summary>テスト用 EsBitmap を生成する。</summary>
    private static EsBitmap CreateBitmap(int width, int height)
        => EsBitmap.Create(width, height);

    /// <summary>表示中の画像を設定した CropViewControl を生成する（切り出しの対象は表示中の画像）。</summary>
    private static CropViewControl CreateCropView(int width = 100, int height = 100)
        => new() { Source = new EsBitmapAvaloniaImage(CreateBitmap(width, height)) };

    // ====================================================================
    // 切り出し結果の型（コア型で返る）と確定の成立
    // ====================================================================

    /// <summary>切り出しの確定がコア型の矩形で返り、UI 型へ変換せずに切り出しコマンドへ渡せることを検証する。</summary>
    /// <remarks>
    /// 確定の結果を UI 型（Avalonia の <c>PixelRect</c>）で返すと、ホストが切り出しコマンド（コア型
    /// <see cref="EsRectInt"/> を要求する）へ渡す際に型が一致せず例外になる。<br/>
    /// 境界（UI 層）では UI 型へ変換するだけで、<b>境界より内側はコア型で引き回す</b>契約をここで固定する。
    /// </remarks>
    [AvaloniaFact]
    internal async Task StartCropAsync_確定はコア型の矩形で返る()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var panel = new Panel();
        var cropView = CreateCropView();
        panel.Children.Add(cropView);
        var window = new Window
        {
            Content = panel
        };
        window.Show();

        var task = capture.StartCropAsync(cropView, window);
        Assert.NotNull(task);

        // 選択矩形を設定して確定する（Enter キー相当の要求）
        cropView.SetSelection(new Rect(0, 0, 10, 10));
        cropView.RequestConfirm();

        // コア型で受ける（UI 型ならここでコンパイルできない＝境界の外へ UI 型を出さない）
        EsRectInt? result = await task;

        Assert.NotNull(result);
        Assert.False(result!.Value.IsEmpty);

        window.Close();
    }

    /// <summary>切り出しのキャンセルが空のコア型矩形（0x0）で返ることを検証する。</summary>
    [AvaloniaFact]
    internal async Task StartCropAsync_キャンセルは空のコア型矩形で返る()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var panel = new Panel();
        var cropView = CreateCropView();
        panel.Children.Add(cropView);
        var window = new Window
        {
            Content = panel
        };
        window.Show();

        var task = capture.StartCropAsync(cropView, window);
        Assert.NotNull(task);

        capture.Cancel();
        EsRectInt? result = await task;

        Assert.NotNull(result);
        Assert.True(result!.Value.IsEmpty);

        window.Close();
    }

    /// <summary>切り出しの境界は開始時には固定されず、確定時に「現在の画像」から都度解決されることを検証する。</summary>
    /// <remarks>
    /// 開始時の画像サイズを固定して保持すると、実行中に画像が差し替わった場合に差し替え前の範囲でクランプしてしまう<br/>
    /// （100x100 → 40x40 の差し替え後も 81x81 の矩形が返る）。 現在の画像（40x40）でクランプされることを固定する。
    /// </remarks>
    [AvaloniaFact]
    internal async Task StartCropAsync_画像差し替え後は新しい画像のサイズで切り出す()
    {
        var capture = new CaptureSessionControl(null, NullLogger.Instance);
        var panel = new Panel();
        var cropView = CreateCropView();
        panel.Children.Add(cropView);
        var window = new Window
        {
            Content = panel
        };
        window.Show();

        var task = capture.StartCropAsync(cropView, window);
        Assert.NotNull(task);

        // 差し替え前の画像（100x100）に収まる選択矩形（80x80）を設定する
        cropView.SetSelection(new Rect(0, 0, 80, 80));

        // 設定後に画像を 40x40 へ差し替える（切り出しの対象が変わる）
        cropView.Source = new EsBitmapAvaloniaImage(CreateBitmap(40, 40));

        cropView.RequestConfirm();
        EsRectInt? result = await task;

        // 差し替え後の画像（40x40）へクランプされた矩形が返る（開始時の 100x100 固定ではない）
        Assert.NotNull(result);
        Assert.Equal(new EsRectInt(0, 0, 40, 40), result!.Value);

        window.Close();
    }
}

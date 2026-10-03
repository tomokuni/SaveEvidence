using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Avalonia.Headless.XUnit;
using BindingFlags = System.Reflection.BindingFlags;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Sessions;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests;

/// <summary><see cref="OverlayWindow"/> の単体テスト（回帰検証: 描画反映・背景フィット・座標変換）。</summary>
/// <remarks>
/// 過去に発生した実機バグの回帰を検出するためのテストを含む:<br/>
/// 1. 暗転・枠・テキストが表示されない → 外側の装飾（MarqueeControl）へ反映され、再描画が要求されるか<br/>
/// 2. 背景画像が拡大表示される → VirtualScreenImage がウィンドウ（DIP）にフィットするか<br/>
/// 3. 選択矩形と取得画像がずれる → 対象座標 → 画面物理座標の変換が正しいか
/// </remarks>
[Collection("AvaloniaHeadless")]
public class OverlayWindowTests
{
    /// <summary>キャプチャサービスのモック。</summary>
    private sealed class MockCaptureService : ICaptureService
    {
        /// <summary>CaptureArea の呼び出し回数（確定時の再キャプチャが廃止されたことの検証用）。</summary>
        public int CaptureAreaCallCount { get; private set; }

        public EsBitmap? CaptureArea(EsRectInt bounds)
        {
            CaptureAreaCallCount++;
            return null;
        }

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
        /// <summary>スクリーン検出の結果（既定は未検出）。</summary>
        public ElementFinderResults ScreenResult { get; set; }

        /// <summary>ウィンドウ検出の結果（既定は未検出）。</summary>
        public ElementFinderResults WindowResult { get; set; }

        public ElementFinderResults FindWindowUnderCursor(EsPointInt screenPoint) => WindowResult;
        public ElementFinderResults FindScreenUnderCursor(EsPointInt screenPoint) => ScreenResult;
        public IReadOnlyList<ElementFinderResults> ListScreens() => [];
        public IReadOnlyList<ElementFinderResults> ListWindows() => [];
    }

    /// <summary>テスト用の OverlayWindow を生成する。</summary>
    private static OverlayWindow CreateWindow(
        CaptureType captureType = CaptureType.AreaSelect,
        double scaling = 1.0,
        Action<EsBitmap?, EsRectInt?>? onComplete = null,
        MockCaptureService? captureService = null,
        MockElementFinder? elementFinder = null)
        => new(
            new MockWindowService(),
            captureService ?? new MockCaptureService(),
            elementFinder ?? new MockElementFinder(),
            captureType,
            new CaptureSettings(),
            scaling,
            onComplete ?? ((_, _) => { }),
            NullLogger.Instance);

    // ====================================================================
    // 描画反映（暗転・枠・テキスト表示）
    // ====================================================================

    [AvaloniaFact]
    public void コンストラクタ_モード別に複合コントロールのモードが設定される()
    {
        Assert.Equal(FadeMaskDragRectMode.AreaSelect, CreateWindow(CaptureType.AreaSelect).SelectionOverlay.Mode);
        Assert.Equal(FadeMaskDragRectMode.ScreenSelect, CreateWindow(CaptureType.ScreenSelect).SelectionOverlay.Mode);
        Assert.Equal(FadeMaskDragRectMode.WindowSelect, CreateWindow(CaptureType.WindowSelect).SelectionOverlay.Mode);
    }

    [AvaloniaFact]
    public void コンストラクタ_AreaSelectではドラッグ前から全面暗転を表示する()
    {
        var window = CreateWindow(CaptureType.AreaSelect);

        // 選択なし（HasSelection / HasHighlight とも false）でも全面暗転が描画される
        Assert.True(window.SelectionOverlay.ShowFullOverlay);
    }

    [AvaloniaFact]
    public void コンストラクタ_ScreenSelectとWindowSelectでは全面暗転を表示しない()
    {
        // ScreenSelect / WindowSelect はカーソル位置の要素をハイライトするため、全面暗転は不要
        Assert.False(CreateWindow(CaptureType.ScreenSelect).SelectionOverlay.ShowFullOverlay);
        Assert.False(CreateWindow(CaptureType.WindowSelect).SelectionOverlay.ShowFullOverlay);
    }

    [AvaloniaFact]
    public void RequestOverlayRender_例外なく再描画を要求する()
    {
        var window = CreateWindow();

        // ヘッドレス環境でも InvalidateVisual は安全に実行される（例外なし）
        window.RequestOverlayRender();
    }

    // ====================================================================
    // 背景画像の Stretch フィット
    // ====================================================================

    [AvaloniaFact]
    public void コンストラクタ_背景画像がウィンドウにフィットするStretchになる()
    {
        var window = CreateWindow();

        // Stretch.None（既定）だと物理ピクセルサイズの Bitmap が拡大表示されるため、Fill でフィットさせる
        Assert.Equal(Stretch.Fill, window.VirtualScreenImageControl.Stretch);
    }

    // ====================================================================
    // 閉区間キャプチャ（始点・終点のピクセルを含む）
    // ====================================================================
    // 高速化: 確定時の切り出し（仮想スクリーンから。 CaptureArea を呼ばない）
    // ====================================================================

    [AvaloniaFact]
    public async Task CompleteSelection_仮想スクリーンから切り出しCaptureAreaを呼ばない()
    {
        var captureService = new MockCaptureService();
        EsBitmap? captured = null;
        EsRectInt? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; },
            captureService: captureService);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = EsBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 確定: 選択矩形 (2,2)〜(5,5)（半開区間幅 3）→ 閉区間 (2,2,4,4)
        // 切り出しはバックグラウンド（Task.Run + Dispatcher.Post）で実行されるため、完了をポーリングで待つ
        window.CompleteSelection(new EsRect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => capturedBounds is not null);

        // CaptureArea は呼ばれない（仮想スクリーンからの切り出しのみ）
        Assert.Equal(0, captureService.CaptureAreaCallCount);

        // 切り出し結果は閉区間の矩形（SourceBounds は画面物理座標）
        Assert.NotNull(capturedBounds);
        Assert.Equal(new EsRectInt(2, 2, 4, 4), capturedBounds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CompleteSelection_仮想スクリーン未設定ならCaptureAreaでフォールバックする()
    {
        var captureService = new MockCaptureService();
        EsBitmap? captured = null;
        EsRectInt? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; },
            captureService: captureService);
        window.Show();

        // 仮想スクリーンを設定しない（オーバーレイを先に表示した直後 = キャプチャ未完了を模擬）
        // この状態で確定すると、CaptureArea でフォールバックする（バックグラウンド実行）
        window.CompleteSelection(new EsRect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => capturedBounds is not null);

        // CaptureArea が呼ばれる（フォールバック）
        Assert.Equal(1, captureService.CaptureAreaCallCount);

        // SourceBounds は閉区間の矩形（画面物理座標）
        Assert.NotNull(capturedBounds);
        Assert.Equal(new EsRectInt(2, 2, 4, 4), capturedBounds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CompleteSelection_マルチモニタの負の原点でも画面物理座標で切り出す()
    {
        // 回帰テスト: 選択矩形（画面物理座標）と切り出し元画像の占有範囲（画面物理座標）は同じ座標空間で
        // 渡す（画像ローカル座標と混ぜると原点の分だけずれて切り出せなくなる）。
        var captureService = new MockCaptureService();
        EsRectInt? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.WindowSelect, scaling: 1.0,
            onComplete: (_, bounds) => capturedBounds = bounds, captureService: captureService);
        window.Show();

        // セカンダリが左・上にある構成（仮想スクリーンの原点が負値）。 画像は (0,0)-(99,99) のローカル座標を持つ
        var virtualScreen = EsBitmap.Create(100, 100);
        window.SetVirtualScreen(virtualScreen, new PixelPoint(-50, -60));

        // 画面物理座標 (0,0)-(9,9)（画像ローカルでは (50,60)-(59,69)）を確定する
        window.CompleteSelection(new EsRect(0, 0, 10, 10));
        await WaitForCompletionAsync(() => capturedBounds is not null);

        // 画面物理座標のまま切り出される（ウィンドウ選択は閉区間化しない）
        Assert.Equal(new EsRectInt(0, 0, 10, 10), capturedBounds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ホバー_事前切り出しが実行される()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(new EsRectInt(2, 2, 3, 3), IsDetected: true, Title: "W"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, scaling: 1.0, elementFinder: finder);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = EsBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // ホバー（検出枠の更新）→ 事前切り出しが実行される（Task.Run + Post のため完了をポーリングで待つ）
        InvokeOnClickMoved(window, CreatePointerMoved(window));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        Assert.Equal(1, window.PreExtractCount);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ホバー_同じ矩形なら事前切り出しをスキップする()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(new EsRectInt(2, 2, 3, 3), IsDetected: true, Title: "W"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, scaling: 1.0, elementFinder: finder);
        window.Show();

        var virtualScreen = EsBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 同じ矩形で 2 回ホバー → 事前切り出しは 1 回だけ（2 回目はスキップ）
        InvokeOnClickMoved(window, CreatePointerMoved(window));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        InvokeOnClickMoved(window, CreatePointerMoved(window));

        Assert.Equal(1, window.PreExtractCount);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ホバー_矩形が変われば事前切り出しが再実行される()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(new EsRectInt(2, 2, 3, 3), IsDetected: true, Title: "W"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, scaling: 1.0, elementFinder: finder);
        window.Show();

        var virtualScreen = EsBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 異なる矩形で 2 回ホバー → 事前切り出しは 2 回
        InvokeOnClickMoved(window, CreatePointerMoved(window));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        finder.WindowResult = new ElementFinderResults(new EsRectInt(5, 5, 2, 2), IsDetected: true, Title: "W");
        InvokeOnClickMoved(window, CreatePointerMoved(window));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 2);

        Assert.Equal(2, window.PreExtractCount);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CompleteSelection_事前切り出しキャッシュを再利用する()
    {
        var captureService = new MockCaptureService();
        EsBitmap? captured = null;
        EsRectInt? capturedBounds = null;
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(new EsRectInt(2, 2, 3, 3), IsDetected: true, Title: "W"),
        };
        var window = CreateWindow(
            captureType: CaptureType.WindowSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; },
            captureService: captureService,
            elementFinder: finder);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = EsBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // ホバー（事前切り出し）→ 完了をポーリングで待つ
        InvokeOnClickMoved(window, CreatePointerMoved(window));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        Assert.Equal(1, window.PreExtractCount);

        // 確定: 同じ矩形 → 事前切り出しキャッシュを再利用（追加切り出しなし）
        window.CompleteSelection(new EsRect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => captured is not null);
        Assert.Equal(1, window.PreExtractCount); // 確定時は切り出しスキップ

        // キャッシュが渡される（null でない）
        Assert.NotNull(captured);
        Assert.NotNull(capturedBounds);
        Assert.Equal(new EsRectInt(2, 2, 3, 3), capturedBounds);
        window.Close();
    }

    // ====================================================================
    // AreaSelect の Enter/Esc/ダブルクリックによる確定・キャンセル
    // ====================================================================

    [AvaloniaFact]
    public async Task AreaSelect_確定要求でキャプチャが確定する()
    {
        EsBitmap? captured = null;
        EsRectInt? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; });
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = EsBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 矩形を設定してから確定要求（Enter キー相当）を発行する
        // → SetSelection → ConfirmRequested → OverlayWindow.OnAreaConfirmRequested → CompleteSelection
        window.SelectionOverlay.SetSelection(new Rect(2, 2, 3, 3));
        window.SelectionOverlay.RequestConfirm();

        // 切り出しはバックグラウンド（Task.Run + Dispatcher.Post）で実行されるため、完了をポーリングで待つ
        await WaitForCompletionAsync(() => capturedBounds is not null);

        // 確定された（SourceBounds は閉区間 (2,2,4,4) = 画面物理座標）
        Assert.NotNull(capturedBounds);
        Assert.Equal(new EsRectInt(2, 2, 4, 4), capturedBounds);
        window.Close();
    }

    [AvaloniaFact]
    public void AreaSelect_キャンセル要求でキャンセルされる()
    {
        EsBitmap? captured = null;
        EsRectInt? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; });
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定
        var virtualScreen = EsBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // キャンセル要求（Esc キー相当）を発行する
        // → MarqueeCore.RequestCancel → CancelRequested → OverlayWindow.OnAreaCancelRequested → CompleteCancelled
        window.SelectionOverlay.RequestCancel();

        // キャンセルされた（null 結果）
        Assert.Null(captured);
        Assert.Null(capturedBounds);
        window.Close();
    }

    // ====================================================================
    // ルーペの座標変換（ポインタ位置と表示画像の一致）
    // ====================================================================

    [AvaloniaFact]
    public void SetVirtualScreen_背景とルーペのソースが破棄されず有効に保持される()
    {
        // 回帰テスト: SetVirtualScreen が using で即座に破棄した WriteableBitmap を Image.Source に
        // 設定すると、レイアウトパス（Image.MeasureOverride → Bitmap.get_Size）で ObjectDisposedException が
        // 発生し、領域選択開始時にアプリがハングする。 背景（VirtualScreenImage）とルーペの拡大元画像は
        // 本ウィンドウが所有し、有効なまま保持されることを検証する。
        var window = CreateWindow(CaptureType.AreaSelect);
        window.Show();

        var bitmap = EsBitmap.Create(100, 100);
        window.SetVirtualScreen(bitmap, default);

        // 固定背景画像（VirtualScreenImage）の Source が破棄済みでないこと（Size 参照で例外なし）
        Assert.NotNull(window.VirtualScreenImageControl.Source);
        Assert.True(window.VirtualScreenImageControl.Source is EsBitmapAvaloniaImage img
            && img.Bitmap is { } bmp && bmp.Size.Width > 0);
        window.Close();
    }

    [AvaloniaFact]
    public void SetVirtualScreen_Close後に背景とルーペのソースが解放される()
    {
        var window = CreateWindow(CaptureType.AreaSelect);
        window.Show();

        var bitmap = EsBitmap.Create(100, 100);
        window.SetVirtualScreen(bitmap, default);
        Assert.NotNull(window.VirtualScreenImageControl.Source);

        // Close で表示用 WriteableBitmap と VirtualScreenImage.Source の両方が解放される（破棄済み Bitmap 残留防止）
        window.Close();

        Assert.Null(window.VirtualScreenImageControl.Source);
        Assert.True(bitmap.IsDisposed);
    }

    [AvaloniaFact]
    public void SetVirtualScreen_Close後に届いた画像はその場で破棄される()
    {
        // 回帰テスト: 背景撮影の完了がキャンセル（Close）より遅れた場合の破棄漏れを防ぐ
        // （差し替え時の旧値の破棄・クローズ後の破棄はコアの保持クラスが単一所有する）。
        var window = CreateWindow(CaptureType.AreaSelect);
        window.Show();
        window.Close();

        var late = EsBitmap.Create(100, 100);
        window.SetVirtualScreen(late, default);

        Assert.True(late.IsDisposed);
        Assert.Null(window.VirtualScreenImageControl.Source);
    }

    /// <summary>バックグラウンド（Task.Run + Dispatcher.Post）の非同期完了をタイムアウト付きで待つ。</summary>
    /// <param name="isCompleted">完了条件を判定する述語（true になるまで待機）。</param>
    /// <remarks>
    /// 確定時の切り出しは <c>Task.Run</c>（別スレッド）→ <c>Dispatcher.Post</c>（UI スレッド）で完了するため、<br/>
    /// 固定回数の Dispatcher ポストでは完了が保証されない。 完了条件をポーリングして確実に待つ。<br/>
    /// 5 秒でタイムアウトし、テストを失敗させる。
    /// </remarks>
    private static async Task WaitForCompletionAsync(Func<bool> isCompleted)
    {
        var timeout = TimeSpan.FromSeconds(5);
        var sw = Stopwatch.StartNew();
        while (!isCompleted())
        {
            if (sw.Elapsed > timeout)
            {
                Assert.Fail("非同期処理がタイムアウトしました。");
            }
            await Dispatcher.UIThread.InvokeAsync(() => { });
            await Task.Delay(10);
        }
    }

    // ====================================================================
    // ハイライト（ScreenSelect / WindowSelect の検出ハイライト）
    // ====================================================================

    [AvaloniaFact]
    public void 検出_画面_未検出時はハイライトをクリアする()
    {
        var window = CreateWindow(CaptureType.ScreenSelect);

        // 未検出（MockElementFinder は常に default）→ ハイライト無効
        InvokeApplyHighlight(window, new EsPointInt(100, 100));

        Assert.Null(window.SelectionOverlay.HighlightSourceRect);
    }

    [AvaloniaFact]
    public void 検出_画面_検出時はハイライトを反映する()
    {
        var finder = new MockElementFinder
        {
            ScreenResult = new ElementFinderResults(
                new EsRectInt(200, 100, 1920, 1080),
                IsDetected: true,
                Title: "Screen 1"),
        };
        var window = CreateWindow(CaptureType.ScreenSelect, elementFinder: finder);
        window.Position = new PixelPoint(100, 50);

        // 検出（画面物理 (200,100)-(2120,1180)）→ 対象（ウィンドウローカル物理）へ変換して反映
        InvokeApplyHighlight(window, new EsPointInt(500, 500));

        Assert.NotNull(window.SelectionOverlay.HighlightSourceRect);
        Assert.Equal(new Rect(100, 50, 1920, 1080), window.SelectionOverlay.HighlightSourceRect);
    }

    [AvaloniaFact]
    public void 検出_画面_サイズラベル換算はオーバーレイのスケーリングを維持する()
    {
        var finder = new MockElementFinder
        {
            ScreenResult = new ElementFinderResults(
                new EsRectInt(200, 100, 1920, 1080),
                IsDetected: true,
                Title: "Screen 1"),
        };
        // scaling = 2.0（オーバーレイの表示スケール。 DisplayScaleScaling も _scaling で初期化される）
        var window = CreateWindow(CaptureType.ScreenSelect, scaling: 2.0, elementFinder: finder);

        // マルチモニタでカーソル位置のスクリーンのスケーリングが異なっても、
        // サイズラベルの物理換算は対象（画面物理 − Position）と往復一致するよう _scaling を維持する
        InvokeApplyHighlight(window, new EsPointInt(500, 500));

        Assert.Equal(2.0, window.SelectionOverlay.DisplayScaleScaling);
        window.Close();
    }

    [AvaloniaFact]
    public void 検出_画面_未検出時もサイズラベル換算を維持する()
    {
        // 未検出（MockElementFinder は常に default）
        var window = CreateWindow(CaptureType.ScreenSelect, scaling: 2.0);

        InvokeApplyHighlight(window, new EsPointInt(100, 100));

        // 未検出時も _scaling（2.0）のまま維持される
        Assert.Equal(2.0, window.SelectionOverlay.DisplayScaleScaling);
        window.Close();
    }

    [AvaloniaFact]
    public void 検出_ウィンドウ_検出時はハイライトを反映する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new EsRectInt(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, elementFinder: finder);
        window.Position = new PixelPoint(100, 50);

        // 検出（画面物理 (300,200)-(1100,800)）→ 対象へ変換して反映
        InvokeApplyHighlight(window, new EsPointInt(500, 500));

        Assert.NotNull(window.SelectionOverlay.HighlightSourceRect);
        Assert.Equal(new Rect(200, 150, 800, 600), window.SelectionOverlay.HighlightSourceRect);
        Assert.Equal("Sample Window", window.SelectionOverlay.InfoTextProvider?.Invoke());
    }

    [AvaloniaFact]
    public void 検出_ウィンドウ_サイズラベル換算はオーバーレイのスケーリングを維持する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new EsRectInt(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, scaling: 2.0, elementFinder: finder);

        // ウィンドウのハイライトでもサイズラベル換算は _scaling を維持する（ScreenSelect と同一）
        InvokeApplyHighlight(window, new EsPointInt(500, 500));

        Assert.Equal(2.0, window.SelectionOverlay.DisplayScaleScaling);
        window.Close();
    }

    [AvaloniaFact]
    public async Task クリック確定_左押下で固定した矩形で解放時に確定する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new EsRectInt(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect, scaling: 1.0,
            onComplete: (_, bounds) => completed.Add(bounds), elementFinder: finder);
        window.Show();

        // 押下（ハイライト固定）→ 解放（確定）
        InvokeOnClickPressed(window, CreatePointerPressed(window, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
        InvokeOnClickReleased(window, CreatePointerReleased(window));

        // 確定は Task.Run + Dispatcher.Post で非同期に完了するため、完了を待つ
        await WaitForCompletionAsync(() => completed.Count > 0);

        // 確定が 1 回実行される（画面物理座標の矩形）
        Assert.Single(completed);
        Assert.NotNull(completed[0]);
        window.Close();
    }

    [AvaloniaFact]
    public async Task クリック確定_右ボタンの押下では固定されない()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new EsRectInt(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect,
            onComplete: (_, bounds) => completed.Add(bounds), elementFinder: finder);
        window.Show();

        // 右ボタン押下 → 固定されず、解放しても確定しない
        InvokeOnClickPressed(window, CreatePointerPressed(window, RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed));
        InvokeOnClickReleased(window, CreatePointerReleased(window));

        await Task.Delay(20);
        Assert.DoesNotContain(completed, bounds => bounds is not null);
        window.Close();
    }

    [AvaloniaFact]
    public async Task クリック確定_退化した検出結果は固定されない()
    {
        // 未検出（矩形が空）→ 固定しない → 解放しても切り出しを行わない（0 サイズの画像を作らない）
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect,
            onComplete: (_, bounds) => completed.Add(bounds));
        window.Show();

        InvokeOnClickPressed(window, CreatePointerPressed(window, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
        InvokeOnClickReleased(window, CreatePointerReleased(window));

        await Task.Delay(20);
        Assert.Empty(completed);
        window.Close();
    }

    [AvaloniaFact]
    public void OnClickMoved_ハイライトを更新して再描画を要求する()
    {
        var finder = new MockElementFinder
        {
            ScreenResult = new ElementFinderResults(
                new EsRectInt(200, 100, 1920, 1080),
                IsDetected: true,
                Title: "Screen 1"),
        };
        var window = CreateWindow(CaptureType.ScreenSelect, elementFinder: finder);
        window.Show();
        var pointer = new Pointer(1, PointerType.Mouse, true);

        // マウス移動 → ハイライト更新（検出関数の呼び分けはコアが単一所有する）
        var e = new PointerEventArgs(
            InputElement.PointerMovedEvent, window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None);
        InvokeOnClickMoved(window, e);

        Assert.NotNull(window.SelectionOverlay.HighlightSourceRect);
        window.Close();
    }

    [AvaloniaFact]
    public async Task OnClickReleased_固定矩形があれば確定する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new EsRectInt(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect, scaling: 1.0,
            onComplete: (_, bounds) => completed.Add(bounds), elementFinder: finder);
        window.Show();

        // 押下（ハイライト固定）→ 解放（確定）
        InvokeOnClickPressed(window, CreatePointerPressed(window, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
        InvokeOnClickReleased(window, CreatePointerReleased(window));

        // 確定は Task.Run + Dispatcher.Post で非同期に完了するため、完了を待つ
        await WaitForCompletionAsync(() => completed.Any(bounds => bounds is not null));

        // 固定した矩形（検出結果）で確定する
        Assert.Equal(new EsRectInt(300, 200, 800, 600), completed.First(bounds => bounds is not null));
        window.Close();
    }

    [AvaloniaFact]
    public void OnClickReleased_固定矩形がなければ確定しない()
    {
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect,
            onComplete: (_, bounds) => completed.Add(bounds));
        window.Show();

        // 押下なしで解放 → 確定しない（固定が無い）
        InvokeOnClickReleased(window, CreatePointerReleased(window));

        Assert.DoesNotContain(completed, bounds => bounds is not null);
        window.Close();
    }

    // ====================================================================
    // ウィンドウのキー操作（Enter = 確定 / Escape = キャンセル）
    // ====================================================================

    [AvaloniaFact]
    public void ハイライト型_Escapeでキャンセルされる()
    {
        // 回帰: 内包する選択オーバーレイ（Marquee）も TopLevel のキーを購読して処理済みにするため、
        // ウィンドウが受け取れず Escape でキャンセルできなかった（Tunnel + handledEventsToo で受ける）。
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect,
            onComplete: (_, bounds) => completed.Add(bounds));
        window.Show();

        window.RaiseEvent(new KeyEventArgs { Key = Key.Escape, RoutedEvent = InputElement.KeyDownEvent });

        Assert.Single(completed);
        Assert.Null(completed[0]);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ハイライト型_確定対象が無いEnterは何もしない()
    {
        // 何も検出していない状態の Enter はキャンセル扱いにしない（コアのキー解釈が単一所有する）
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect,
            onComplete: (_, bounds) => completed.Add(bounds));
        window.Show();

        window.RaiseEvent(new KeyEventArgs { Key = Key.Enter, RoutedEvent = InputElement.KeyDownEvent });

        await Task.Delay(20);
        Assert.DoesNotContain(completed, bounds => bounds is not null);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ハイライト型_確定対象があるEnterは確定する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new EsRectInt(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var completed = new List<EsRectInt?>();
        var window = CreateWindow(
            CaptureType.WindowSelect,
            onComplete: (_, bounds) => completed.Add(bounds), elementFinder: finder);
        window.Show();

        // ホバー（ハイライトを検出）→ Enter で確定
        InvokeApplyHighlight(window, new EsPointInt(500, 500));
        window.RaiseEvent(new KeyEventArgs { Key = Key.Enter, RoutedEvent = InputElement.KeyDownEvent });

        await WaitForCompletionAsync(() => completed.Any(bounds => bounds is not null));

        // ウィンドウの画面物理位置は既定（0,0）のため、検出矩形がそのままキャプチャ矩形になる（ウィンドウ選択は +1 しない）
        Assert.Equal(new EsRectInt(300, 200, 800, 600), completed.First(bounds => bounds is not null));
        window.Close();
    }

    [AvaloniaFact]
    public void 検出_ウィンドウ_未検出時はハイライトをクリアする()
    {
        var window = CreateWindow(CaptureType.WindowSelect);

        // 未検出 → ハイライト無効・タイトル空
        InvokeApplyHighlight(window, new EsPointInt(100, 100));

        Assert.Null(window.SelectionOverlay.HighlightSourceRect);
        Assert.Equal("", window.SelectionOverlay.InfoTextProvider?.Invoke());
    }

    // ====================================================================
    // リフレクションヘルパー（private メソッドの直接検証）
    // ====================================================================

    private static void InvokeApplyHighlight(OverlayWindow window, EsPointInt screenPoint)
    {
        // 検出関数の呼び分けはコアが単一所有するため、本ヘルパーは「検出→反映」の 1 経路を呼ぶ
        var applyMethod = typeof(OverlayWindow).GetMethod(
            "ApplyHighlight", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var finder = typeof(OverlayWindow)
            .GetField("_detector", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) as IElementFinder;
        var type = typeof(OverlayWindow)
            .GetField("_captureType", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) is CaptureType captureType
                ? captureType
                : CaptureType.AreaSelect;
        applyMethod.Invoke(window, [CaptureTypeTraits.FindUnderCursor(finder, type, screenPoint)]);
    }

    private static void InvokeOnClickPressed(OverlayWindow window, PointerPressedEventArgs e)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "OnClickPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [null, e]);
    }

    /// <summary>指定ウィンドウを対象とするポインタ押下イベントを生成する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="button">押下するボタン。</param>
    /// <param name="kind">押下の種類（左 / 右など）。</param>
    /// <returns>ポインタ押下イベント。</returns>
    private static PointerPressedEventArgs CreatePointerPressed(
        OverlayWindow window, RawInputModifiers button, PointerUpdateKind kind)
    {
        var pointer = new Pointer(1, PointerType.Mouse, true);
        return new PointerPressedEventArgs(
            window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(button, kind), KeyModifiers.None, 1);
    }

    /// <summary>指定ウィンドウを対象とするポインタ解放イベントを生成する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <returns>ポインタ解放イベント。</returns>
    private static PointerReleasedEventArgs CreatePointerReleased(OverlayWindow window)
    {
        var pointer = new Pointer(1, PointerType.Mouse, true);
        return new PointerReleasedEventArgs(
            window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left);
    }

    private static void InvokeOnClickMoved(OverlayWindow window, PointerEventArgs e)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "OnClickMoved", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [null, e]);
    }

    private static void InvokeOnClickReleased(OverlayWindow window, PointerReleasedEventArgs e)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "OnClickReleased", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [null, e]);
    }

    /// <summary>指定ウィンドウを対象とするポインタ移動イベントを生成する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <returns>ポインタ移動イベント。</returns>
    private static PointerEventArgs CreatePointerMoved(OverlayWindow window)
    {
        var pointer = new Pointer(1, PointerType.Mouse, true);
        return new PointerEventArgs(
            InputElement.PointerMovedEvent, window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None);
    }
}


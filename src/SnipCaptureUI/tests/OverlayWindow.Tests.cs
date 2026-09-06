using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Point = Avalonia.Point;
using Rect = Estable.RectD;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.FadeMaskDragRect;
using Estable;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Avalonia.Headless.XUnit;
using PbBitmap = Estable.Media.EsBitmap;
using BindingFlags = System.Reflection.BindingFlags;

namespace Estable.AvaloniaUI.SnipCaptureUI.Tests;

/// <summary><see cref="OverlayWindow"/> の単体テスト（回帰検証: 描画反映・背景フィット・座標変換）。</summary>
/// <remarks>
/// 過去に発生した実機バグの回帰を検出するためのテストを含む:<br/>
/// 1. 暗転・枠・テキストが表示されない → レンダラー状態が FadeMaskControl へ反映され、再描画が要求されるか<br/>
/// 2. 背景画像が拡大表示される → VirtualScreenImage がウィンドウ（DIP）にフィットするか<br/>
/// 3. 選択矩形と取得画像がずれる → ドメイン座標 → 画面物理座標の変換が正しいか
/// </remarks>
[Collection("AvaloniaHeadless")]
public class OverlayWindowTests
{
    /// <summary>キャプチャサービスのモック。</summary>
    private sealed class MockCaptureService : ICaptureService
    {
        /// <summary>CaptureArea の呼び出し回数（確定時の再キャプチャが廃止されたことの検証用）。</summary>
        public int CaptureAreaCallCount { get; private set; }

        public PbBitmap? CaptureArea(PixelRect bounds)
        {
            CaptureAreaCallCount++;
            return null;
        }

        public PbBitmap? CaptureVirtualScreen(out PixelPoint origin)
        {
            origin = default;
            return null;
        }
    }

    /// <summary>ウィンドウサービスのモック。</summary>
    private sealed class MockWindowService : IWindowService
    {
        public void HideFromTaskSwitcher(IPlatformHandle handle) { }
        public bool SetCaptureExclusion(IPlatformHandle handle, bool exclude) => true;
        public void EvacuateWindow(IPlatformHandle handle) { }
    }

    /// <summary>要素検出のモック（常に未検出）。</summary>
    private sealed class MockElementFinder : IElementFinder
    {
        /// <summary>スクリーン検出の結果（既定は未検出）。</summary>
        public ElementFinderResults ScreenResult { get; set; }

        /// <summary>ウィンドウ検出の結果（既定は未検出）。</summary>
        public ElementFinderResults WindowResult { get; set; }

        public ElementFinderResults FindWindowUnderCursor(PixelPoint screenPoint) => WindowResult;
        public ElementFinderResults FindScreenUnderCursor(PixelPoint screenPoint) => ScreenResult;
        public IReadOnlyList<ElementFinderResults> ListScreens() => [];
        public IReadOnlyList<ElementFinderResults> ListWindows() => [];
    }

    /// <summary>テスト用の OverlayWindow を生成する。</summary>
    private static OverlayWindow CreateWindow(
        CaptureType captureType = CaptureType.AreaSelect,
        double scaling = 1.0,
        Action<PbBitmap?, PixelRect?>? onComplete = null,
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
    public void UpdateOverlay_選択矩形を選択状態へ反映する()
    {
        var window = CreateWindow(CaptureType.AreaSelect, scaling: 1.0);

        // 選択矩形（ドメイン = 仮想スクリーンピクセル）を更新 → 選択状態（暗転マスク・ルーペ）へ反映される
        window.UpdateOverlay(new RectD(100, 200, 400, 300));
        Assert.True(window.SelectionOverlay.HasSelection);
        Assert.Equal(new RectD(100, 200, 400, 300), window.SelectionOverlay.SelectionRect);

        // サイズ 0 の矩形（未選択）→ 選択状態はクリアされる
        window.UpdateOverlay(new RectD(0, 0, 0, 0));
        Assert.False(window.SelectionOverlay.HasSelection);
    }

    [AvaloniaFact]
    public void UpdateOverlay_選択矩形をドメインのまま選択状態へ反映する()
    {
        var window = CreateWindow(scaling: 2.0);

        window.UpdateOverlay(new RectD(100, 200, 400, 300));

        // 選択矩形はドメイン（仮想スクリーンピクセル）のまま設定される。
        // DIP 変換（÷ scaling）+ 閉区間（終点 +1）は描画時に座標変換ポート（DomainScale）が行う。
        Assert.Equal(new RectD(100, 200, 400, 300), window.SelectionOverlay.SelectionRect);
        Assert.True(window.SelectionOverlay.HasSelection);
    }

    [AvaloniaFact]
    public void UpdateOverlay_サイズ0の矩形は未選択として反映する()
    {
        var window = CreateWindow();

        window.UpdateOverlay(new Rect(0, 0, 0, 0));

        Assert.False(window.SelectionOverlay.HasSelection);
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
    // 高速化: 確定時の切り出し（仮想スクリーンから。CaptureArea を呼ばない）
    // ====================================================================

    [AvaloniaFact]
    public async Task CompleteSelection_仮想スクリーンから切り出しCaptureAreaを呼ばない()
    {
        var captureService = new MockCaptureService();
        PbBitmap? captured = null;
        PixelRect? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; },
            captureService: captureService);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = PbBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 確定: 選択矩形 (2,2)〜(5,5)（半開区間幅 3）→ 閉区間 (2,2,4,4)
        // 切り出しはバックグラウンド（Task.Run + Dispatcher.Post）で実行されるため、完了をポーリングで待つ
        window.CompleteSelection(new Avalonia.Rect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => capturedBounds is not null);

        // CaptureArea は呼ばれない（仮想スクリーンからの切り出しのみ）
        Assert.Equal(0, captureService.CaptureAreaCallCount);

        // 切り出し結果は閉区間の矩形（SourceBounds は画面物理座標）
        Assert.NotNull(capturedBounds);
        Assert.Equal(new PixelRect(2, 2, 4, 4), capturedBounds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CompleteSelection_仮想スクリーン未設定ならCaptureAreaでフォールバックする()
    {
        var captureService = new MockCaptureService();
        PbBitmap? captured = null;
        PixelRect? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; },
            captureService: captureService);
        window.Show();

        // 仮想スクリーンを設定しない（オーバーレイを先に表示した直後 = キャプチャ未完了を模擬）
        // この状態で確定すると、CaptureArea でフォールバックする（バックグラウンド実行）
        window.CompleteSelection(new Avalonia.Rect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => capturedBounds is not null);

        // CaptureArea が呼ばれる（フォールバック）
        Assert.Equal(1, captureService.CaptureAreaCallCount);

        // SourceBounds は閉区間の矩形（画面物理座標）
        Assert.NotNull(capturedBounds);
        Assert.Equal(new PixelRect(2, 2, 4, 4), capturedBounds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task UpdateOverlay_事前切り出しが実行される()
    {
        var window = CreateWindow(CaptureType.AreaSelect, scaling: 1.0);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = PbBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 枠更新（ドラッグ後の矩形）→ 事前切り出しが実行される（Task.Run + Post のため完了をポーリングで待つ）
        window.UpdateOverlay(new Rect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        Assert.Equal(1, window.PreExtractCount);
        window.Close();
    }

    [AvaloniaFact]
    public async Task UpdateOverlay_同じ矩形なら事前切り出しをスキップする()
    {
        var window = CreateWindow(CaptureType.AreaSelect, scaling: 1.0);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = PbBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 同じ矩形で 2 回枠更新 → 事前切り出しは 1 回だけ（2 回目はスキップ）
        window.UpdateOverlay(new Rect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        window.UpdateOverlay(new Rect(2, 2, 3, 3));

        Assert.Equal(1, window.PreExtractCount);
        window.Close();
    }

    [AvaloniaFact]
    public async Task UpdateOverlay_矩形が変われば事前切り出しが再実行される()
    {
        var window = CreateWindow(CaptureType.AreaSelect, scaling: 1.0);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = PbBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 異なる矩形で 2 回枠更新 → 事前切り出しは 2 回
        window.UpdateOverlay(new Rect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        window.UpdateOverlay(new Rect(5, 5, 2, 2));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 2);

        Assert.Equal(2, window.PreExtractCount);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CompleteSelection_事前切り出しキャッシュを再利用する()
    {
        var captureService = new MockCaptureService();
        PbBitmap? captured = null;
        PixelRect? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; },
            captureService: captureService);
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = PbBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 枠更新（事前切り出し）→ 完了をポーリングで待つ
        window.UpdateOverlay(new Rect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => window.PreExtractCount >= 1);
        Assert.Equal(1, window.PreExtractCount);

        // 確定: 同じ矩形 → 事前切り出しキャッシュを再利用（追加切り出しなし）
        window.CompleteSelection(new Avalonia.Rect(2, 2, 3, 3));
        await WaitForCompletionAsync(() => captured is not null);
        Assert.Equal(1, window.PreExtractCount); // 確定時は切り出しスキップ

        // キャッシュが渡される（null でない）
        Assert.NotNull(captured);
        Assert.NotNull(capturedBounds);
        Assert.Equal(new PixelRect(2, 2, 4, 4), capturedBounds);
        window.Close();
    }

    // ====================================================================
    // AreaSelect の Enter/Esc/ダブルクリックによる確定・キャンセル
    // ====================================================================

    [AvaloniaFact]
    public async Task AreaSelect_確定要求でキャプチャが確定する()
    {
        PbBitmap? captured = null;
        PixelRect? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; });
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定（原点 = (0,0)）
        var virtualScreen = PbBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // 矩形を設定してから確定要求（Enter キー相当）を発行する
        // → SetDragRectSelection → ConfirmRequested → OverlayWindow.OnAreaConfirmRequested → CompleteSelection
        window.SelectionOverlay.SetDragRectSelection(new Rect(2, 2, 3, 3));
        window.SelectionOverlay.RequestConfirm();

        // 切り出しはバックグラウンド（Task.Run + Dispatcher.Post）で実行されるため、完了をポーリングで待つ
        await WaitForCompletionAsync(() => capturedBounds is not null);

        // 確定された（SourceBounds は閉区間 (2,2,4,4) = 画面物理座標）
        Assert.NotNull(capturedBounds);
        Assert.Equal(new PixelRect(2, 2, 4, 4), capturedBounds);
        window.Close();
    }

    [AvaloniaFact]
    public void AreaSelect_キャンセル要求でキャンセルされる()
    {
        PbBitmap? captured = null;
        PixelRect? capturedBounds = null;
        var window = CreateWindow(
            captureType: CaptureType.AreaSelect,
            onComplete: (bmp, bounds) => { captured = bmp; capturedBounds = bounds; });
        window.Show();

        // 仮想スクリーン全体（10×10 のダミー）を設定
        var virtualScreen = PbBitmap.Create(10, 10);
        window.SetVirtualScreen(virtualScreen, default);

        // キャンセル要求（Esc キー相当）を発行する
        // → DragRect.RequestCancel → CancelRequested → OverlayWindow.OnAreaCancelRequested → CompleteCancelled
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
    public void UpdateLoupe_ルーペ中心がドメイン座標と一致する()
    {
        var window = CreateWindow(scaling: 1.0);
        window.Show(); // _loupeControl.Parent の接続に必要

        // 仮想スクリーン Bitmap（3840×1080 = デュアルモニタ相当）を設定
        var bitmap = PbBitmap.Create(3840, 1080);
        window.SetVirtualScreen(bitmap, new PixelPoint(-1920, 0));

        // ドメイン（仮想スクリーンピクセル = ウィンドウローカル DIP × scaling）でルーペを更新
        window.UpdateLoupe(new PointD(960, 540));

        // ドメインは既に仮想スクリーン Bitmap 上の座標と一致する（原点を再減算しない）
        Assert.Equal(new PointD(960, 540), window.SelectionOverlay.LoupeCenterPoint);
        window.Close();
    }

    [AvaloniaFact]
    public void UpdateLoupe_仮想スクリーン原点が非ゼロでもルーペ中心はドメイン座標のままである()
    {
        var window = CreateWindow(scaling: 2.0);
        window.Show(); // _loupeControl.Parent の接続に必要

        // マルチモニタ（プライマリが右側・仮想スクリーン原点が (-1920, 0)）のケース
        var bitmap = PbBitmap.Create(3840, 1080);
        window.SetVirtualScreen(bitmap, new PixelPoint(-1920, 0));

        // ドメイン（= ローカル DIP × scaling）でルーペを更新
        window.UpdateLoupe(new PointD(2880, 540));

        // 仮想スクリーン原点を減算すると (4800, 540) にずれる（範囲外）ため、ドメイン座標のままであるべき
        Assert.Equal(new PointD(2880, 540), window.SelectionOverlay.LoupeCenterPoint);
        window.Close();
    }

    [AvaloniaFact]
    public void SetVirtualScreen_背景とルーペのソースが破棄されず有効に保持される()
    {
        // 回帰テスト: SetVirtualScreen が using で即座に破棄した WriteableBitmap を Image.Source に
        // 設定すると、レイアウトパス（Image.MeasureOverride → Bitmap.get_Size）で ObjectDisposedException が
        // 発生し、領域選択開始時にアプリがハングする。背景（VirtualScreenImage）とルーペの拡大元画像は
        // 本ウィンドウが所有し、有効なまま保持されることを検証する。
        var window = CreateWindow(CaptureType.AreaSelect);
        window.Show();

        var bitmap = PbBitmap.Create(100, 100);
        window.SetVirtualScreen(bitmap, default);

        // 固定背景画像（VirtualScreenImage）の Source が破棄済みでないこと（Size 参照で例外なし）
        Assert.NotNull(window.VirtualScreenImageControl.Source);
        Assert.True(window.VirtualScreenImageControl.Source is PbBitmapAvaloniaImage img
            && img.Bitmap is { } bmp && bmp.Size.Width > 0);
        window.Close();
    }

    [AvaloniaFact]
    public void SetVirtualScreen_Close後に背景とルーペのソースが解放される()
    {
        var window = CreateWindow(CaptureType.AreaSelect);
        window.Show();

        var bitmap = PbBitmap.Create(100, 100);
        window.SetVirtualScreen(bitmap, default);
        Assert.NotNull(window.VirtualScreenImageControl.Source);

        // Close で表示用 WriteableBitmap と VirtualScreenImage.Source の両方が解放される（破棄済み Bitmap 残留防止）
        window.Close();

        Assert.Null(window.VirtualScreenImageControl.Source);
    }

    /// <summary>バックグラウンド（Task.Run + Dispatcher.Post）の非同期完了をタイムアウト付きで待つ。</summary>
    /// <param name="isCompleted">完了条件を判定する述語（true になるまで待機）。</param>
    /// <remarks>
    /// 確定時の切り出しは <c>Task.Run</c>（別スレッド）→ <c>Dispatcher.Post</c>（UI スレッド）で完了するため、<br/>
    /// 固定回数の Dispatcher ポストでは完了が保証されない。完了条件をポーリングして確実に待つ。<br/>
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
    public void ToWindowPixel_画面物理座標からウィンドウローカル物理ピクセルへ変換する()
    {
        var window = CreateWindow(CaptureType.ScreenSelect);
        window.Position = new PixelPoint(100, 50);

        // Position を減算した矩形（scaling では割らない・ドメイン座標）
        var result = InvokeToWindowPixel(window, new PixelRect(200, 150, 800, 600));

        Assert.Equal(new Avalonia.Rect(100, 100, 800, 600), result);
    }

    [AvaloniaFact]
    public void UpdateSelectScreenHighlight_未検出時はハイライトをクリアする()
    {
        var window = CreateWindow(CaptureType.ScreenSelect);

        // 未検出（MockElementFinder は常に default）→ ハイライト無効・ScreenIndex -1
        InvokeUpdateSelectScreenHighlight(window, new PixelPoint(100, 100));

        Assert.False(window.SelectionOverlay.HasHighlight);
        Assert.Null(window.SelectionOverlay.LogicalHighlightRect);
        Assert.Equal(-1, window.SelectionOverlay.ScreenIndex);
    }

    [AvaloniaFact]
    public void UpdateSelectScreenHighlight_検出時はハイライトを反映する()
    {
        var finder = new MockElementFinder
        {
            ScreenResult = new ElementFinderResults(
                new PixelRect(200, 100, 1920, 1080),
                IsDetected: true,
                Title: "Screen 1"),
        };
        var window = CreateWindow(CaptureType.ScreenSelect, elementFinder: finder);
        window.Position = new PixelPoint(100, 50);

        // 検出（画面物理 (200,100)-(2120,1180)）→ ドメイン（ウィンドウローカル物理）へ変換して反映
        InvokeUpdateSelectScreenHighlight(window, new PixelPoint(500, 500));

        Assert.True(window.SelectionOverlay.HasHighlight);
        Assert.Equal(new Rect(100, 50, 1920, 1080), window.SelectionOverlay.LogicalHighlightRect);
        Assert.Equal(1, window.SelectionOverlay.ScreenIndex);
    }

    [AvaloniaFact]
    public void UpdateSelectScreenHighlight_サイズラベル換算はオーバーレイのスケーリングを維持する()
    {
        var finder = new MockElementFinder
        {
            ScreenResult = new ElementFinderResults(
                new PixelRect(200, 100, 1920, 1080),
                IsDetected: true,
                Title: "Screen 1"),
        };
        // scaling = 2.0（オーバーレイの表示スケール。DisplayScaleScaling も _scaling で初期化される）
        var window = CreateWindow(CaptureType.ScreenSelect, scaling: 2.0, elementFinder: finder);

        // マルチモニタでカーソル位置のスクリーンのスケーリングが異なっても、
        // サイズラベルの物理換算はドメイン（画面物理 − Position）と往復一致するよう _scaling を維持する
        InvokeUpdateSelectScreenHighlight(window, new PixelPoint(500, 500));

        Assert.Equal(2.0, window.SelectionOverlay.DisplayScaleScaling);
        window.Close();
    }

    [AvaloniaFact]
    public void UpdateSelectScreenHighlight_未検出時もサイズラベル換算を維持する()
    {
        // 未検出（MockElementFinder は常に default）
        var window = CreateWindow(CaptureType.ScreenSelect, scaling: 2.0);

        InvokeUpdateSelectScreenHighlight(window, new PixelPoint(100, 100));

        // 未検出時も _scaling（2.0）のまま維持される
        Assert.Equal(2.0, window.SelectionOverlay.DisplayScaleScaling);
        window.Close();
    }

    [AvaloniaFact]
    public void UpdateWindowSelectHighlight_検出時はハイライトを反映する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new PixelRect(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, elementFinder: finder);
        window.Position = new PixelPoint(100, 50);

        // 検出（画面物理 (300,200)-(1100,800)）→ ドメインへ変換して反映
        InvokeUpdateWindowSelectHighlight(window, new PixelPoint(500, 500));

        Assert.True(window.SelectionOverlay.HasHighlight);
        Assert.Equal(new Rect(200, 150, 800, 600), window.SelectionOverlay.LogicalHighlightRect);
        Assert.Equal("Sample Window", window.SelectionOverlay.WindowTitle);
    }

    [AvaloniaFact]
    public void UpdateWindowSelectHighlight_サイズラベル換算はオーバーレイのスケーリングを維持する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new PixelRect(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, scaling: 2.0, elementFinder: finder);

        // ウィンドウのハイライトでもサイズラベル換算は _scaling を維持する（ScreenSelect と同一）
        InvokeUpdateWindowSelectHighlight(window, new PixelPoint(500, 500));

        Assert.Equal(2.0, window.SelectionOverlay.DisplayScaleScaling);
        window.Close();
    }

    [AvaloniaFact]
    public void ToWindowDip_画面物理座標からウィンドウローカルDIPへ変換する()
    {
        var window = CreateWindow(CaptureType.ScreenSelect, scaling: 2.0);
        window.Position = new PixelPoint(100, 50);

        // (画面物理 − Position) / scaling で変換
        var result = InvokeToWindowDip(window, new PixelRect(300, 250, 800, 600));

        Assert.Equal(new Avalonia.Rect(100, 100, 400, 300), result);
    }

    [AvaloniaFact]
    public void OnClickPressed_左ボタン押下でハイライトが固定される()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new PixelRect(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, scaling: 1.0, elementFinder: finder);
        window.Show();
        var pointer = new Pointer(1, PointerType.Mouse, true);

        // 左ボタン押下（クリック確定フロー開始）
        var e = new PointerPressedEventArgs(
            window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, 1);
        InvokeOnClickPressed(window, e);

        // 固定矩形（_clickFixedRect）が設定される（リフレクションで確認）
        var field = typeof(OverlayWindow).GetField("_clickFixedRect", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.NotNull(field.GetValue(window));
        window.Close();
    }

    [AvaloniaFact]
    public void OnClickPressed_左ボタン以外は無視される()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new PixelRect(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var window = CreateWindow(CaptureType.WindowSelect, elementFinder: finder);
        window.Show();
        var pointer = new Pointer(1, PointerType.Mouse, true);

        // 右ボタン押下 → 無視（_clickFixedRect は設定されない）
        var e = new PointerPressedEventArgs(
            window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
            KeyModifiers.None, 1);
        InvokeOnClickPressed(window, e);

        var field = typeof(OverlayWindow).GetField("_clickFixedRect", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(field.GetValue(window));
        window.Close();
    }

    [AvaloniaFact]
    public void OnClickMoved_ハイライトを更新して再描画を要求する()
    {
        var finder = new MockElementFinder
        {
            ScreenResult = new ElementFinderResults(
                new PixelRect(200, 100, 1920, 1080),
                IsDetected: true,
                Title: "Screen 1"),
        };
        var window = CreateWindow(CaptureType.ScreenSelect, elementFinder: finder);
        window.Show();
        var pointer = new Pointer(1, PointerType.Mouse, true);

        // マウス移動 → ハイライト更新（ScreenSelect は UpdateSelectScreenHighlight）
        var e = new PointerEventArgs(
            InputElement.PointerMovedEvent, window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None);
        InvokeOnClickMoved(window, e);

        Assert.True(window.SelectionOverlay.HasHighlight);
        window.Close();
    }

    [AvaloniaFact]
    public async Task OnClickReleased_固定矩形があれば確定する()
    {
        var finder = new MockElementFinder
        {
            WindowResult = new ElementFinderResults(
                new PixelRect(300, 200, 800, 600),
                IsDetected: true,
                Title: "Sample Window"),
        };
        var completed = new List<PixelRect?>();
        var window = CreateWindow(
            CaptureType.WindowSelect, scaling: 1.0,
            onComplete: (_, bounds) => completed.Add(bounds), elementFinder: finder);
        window.Show();
        var pointer = new Pointer(1, PointerType.Mouse, true);

        // 押下（ハイライト固定）→ 解放（確定）
        var pressed = new PointerPressedEventArgs(
            window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, 1);
        InvokeOnClickPressed(window, pressed);

        // 固定矩形（_clickFixedRect）が設定されているか確認
        var clickFixed = typeof(OverlayWindow).GetField("_clickFixedRect", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
        Assert.NotNull(clickFixed);

        var released = new PointerReleasedEventArgs(
            window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left);
        InvokeOnClickReleased(window, released);

        // 確定は Task.Run + Dispatcher.Post で非同期に完了するため、完了を待つ
        await WaitForCompletionAsync(() => completed.Count > 0);

        // 確定が 1 回実行される（画面物理座標の矩形）
        Assert.Single(completed);
        Assert.NotNull(completed[0]);
        window.Close();
    }

    [AvaloniaFact]
    public void OnClickReleased_固定矩形がなければ確定しない()
    {
        var completed = new List<PixelRect?>();
        var window = CreateWindow(
            CaptureType.WindowSelect,
            onComplete: (_, bounds) => completed.Add(bounds));
        window.Show();
        var pointer = new Pointer(1, PointerType.Mouse, true);

        // 押下なしで解放 → 確定しない（_clickFixedRect は null）
        var released = new PointerReleasedEventArgs(
            window, pointer, window, new Point(10, 10), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left);
        InvokeOnClickReleased(window, released);

        Assert.Empty(completed);
        window.Close();
    }

    [AvaloniaFact]
    public void UpdateWindowSelectHighlight_未検出時はハイライトをクリアする()
    {
        var window = CreateWindow(CaptureType.WindowSelect);

        // 未検出 → ハイライト無効・タイトル空
        InvokeUpdateWindowSelectHighlight(window, new PixelPoint(100, 100));

        Assert.False(window.SelectionOverlay.HasHighlight);
        Assert.Null(window.SelectionOverlay.LogicalHighlightRect);
        Assert.Equal("", window.SelectionOverlay.WindowTitle);
    }

    // ====================================================================
    // リフレクションヘルパー（private メソッドの直接検証）
    // ====================================================================

    private static Avalonia.Rect InvokeToWindowPixel(OverlayWindow window, PixelRect bounds)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "ToWindowPixel", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Avalonia.Rect)method.Invoke(window, [bounds])!;
    }

    private static Avalonia.Rect InvokeToWindowDip(OverlayWindow window, PixelRect bounds)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "ToWindowDip", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Avalonia.Rect)method.Invoke(window, [bounds])!;
    }

    private static void InvokeOnClickPressed(OverlayWindow window, PointerPressedEventArgs e)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "OnClickPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [null, e]);
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

    private static void InvokeUpdateSelectScreenHighlight(OverlayWindow window, PixelPoint screenPoint)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "UpdateSelectScreenHighlight", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [screenPoint]);
    }

    private static void InvokeUpdateWindowSelectHighlight(OverlayWindow window, PixelPoint screenPoint)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "UpdateWindowSelectHighlight", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [screenPoint]);
    }
}


using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Rect = Avalonia.Rect;
using Estable.Platform;
using Estable.Platform.Coordinate;
using Estable.Platform.Imaging;
using Estable.AvaloniaUI.CaptureSession.Logging;
using Estable.AvaloniaUI.FadeMaskDragRect;
using Estable.FadeMaskDragRect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Estable.CaptureSession;

namespace Estable.AvaloniaUI.CaptureSession;

/// <summary>画面キャプチャ用の切り取りオーバーレイウィンドウ（UI 部品）。</summary>
/// <remarks>
/// 全画面表示の透明ウィンドウとして動作し、マウス操作による領域選択やウィンドウ選択を実現する。<br/>
/// 3 種類のキャプチャモード（全画面 / ウィンドウ選択 / 範囲選択）に対応する。<br/>
/// <br/>
/// 状態は持たない（選択矩形・ハイライトは複合コントロール <see cref="FadeMaskDragRectControl"/> が保持）。<br/>
/// - AreaSelect: 複合コントロール（DragRect + FadeMask + Loupe）でドラッグ選択し、ルーペで拡大表示する。<br/>
/// - ScreenSelect / WindowSelect: ウィンドウの Pointer イベントから <see cref="IElementFinder"/> で検出し、<br/>
///   複合コントロールのハイライト状態（LogicalHighlightRect / HasHighlight 等）に設定する。<br/>
/// 確定フロー（非表示 → DWM 待機 → キャプチャ）は本クラスが直接行う（<see cref="ICaptureService"/> に委譲）。
/// </remarks>
internal sealed partial class OverlayWindow : Window
{
    /// <summary>キャプチャ種別。</summary>
    private readonly CaptureType _captureType;

    /// <summary>キャプチャ完了コールバック（確定・キャンセル・エラー時に呼ばれる）。</summary>
    private readonly Action<EsBitmap?, PixelRect?> _onComplete;

    /// <summary>DPI スケーリング係数（プライマリスクリーン基準）。</summary>
    private readonly double _scaling;

    /// <summary>ネイティブウィンドウ操作（Alt+Tab 非表示・ハング時退避）。プラットフォーム抽象。</summary>
    private readonly IWindowService _windowService;

    /// <summary>画面キャプチャ（プラットフォーム抽象）。</summary>
    private readonly ICaptureService _captureService;

    /// <summary>ウィンドウ/スクリーン検出（プラットフォーム抽象）。</summary>
    private readonly IElementFinder _detector;

    /// <summary>内部ロガー。</summary>
    private readonly ILogger _log;

    /// <summary>完了済みかどうか（volatile: バックグラウンドスレッドからも参照）。</summary>
    private volatile bool _hasCompleted;

    /// <summary>クリック確定型の押下時に固定したハイライト矩形。リリース時に確定する。</summary>
    private Rect? _clickFixedRect;

    /// <summary>仮想スクリーン全体の EsBitmap（全モード共通のキャプチャソース・切り出し元）。</summary>
    private EsBitmap? _virtualScreenBitmap;

    /// <summary>仮想スクリーンの原点（画面物理座標）。Bitmap 上の座標 = 画面物理座標 − 原点。</summary>
    private PixelPoint _virtualScreenOrigin;

    /// <summary>事前切り出し済みの矩形（画面物理座標・閉区間）。キャッシュのキー。</summary>
    private PixelRect? _cachedClipRect;

    /// <summary>事前切り出し済みの Bitmap（確定時に再利用。枠の変更がない場合は切り出しをスキップ）。</summary>
    private EsBitmap? _cachedClipBitmap;

    /// <summary>事前切り出しの実行回数（テスト検証用）。</summary>
    private int _preExtractCount;

    /// <summary>事前切り出しの実行回数（テスト検証用アクセサ）。</summary>
    internal int PreExtractCount => _preExtractCount;

    /// <summary>OverlayWindow を初期化する。</summary>
    /// <param name="windowService">ネイティブウィンドウ操作（Alt+Tab 非表示・ハング時退避）。</param>
    /// <param name="captureService">画面キャプチャ（プラットフォーム抽象）。</param>
    /// <param name="detector">ウィンドウ/スクリーン検出（プラットフォーム抽象）。</param>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="settings">キャプチャ設定（全メンバー non-null）。</param>
    /// <param name="scaling">DPI スケーリング係数。</param>
    /// <param name="onComplete">キャプチャ完了コールバック。</param>
    /// <param name="logger">内部ロガー。null の場合は出力なし。</param>
    public OverlayWindow(
        IWindowService windowService,
        ICaptureService captureService,
        IElementFinder detector,
        CaptureType captureType,
        CaptureSettings settings,
        double scaling,
        Action<EsBitmap?, PixelRect?> onComplete,
        ILogger? logger = null)
    {
        InitializeComponent();
        _captureType = captureType;
        _onComplete = onComplete;
        _log = logger ?? NullLogger.Instance;
        _windowService = windowService;
        _captureService = captureService;
        _detector = detector;
        _scaling = scaling > 0 ? scaling : 1.0;

        var s = settings.ToNonNull();

        // カーソルは AreaSelect のみ十字。WindowSelect / ScreenSelect はデフォルト（矢印）
        if (captureType == CaptureType.AreaSelect)
            Cursor = new Cursor(StandardCursorType.Cross);

        SetWindowSizeToVirtualScreen();
        ConfigureSelectionControls(s);
        SubscribeToEvents();
    }

    /// <summary>ウィンドウを仮想スクリーン全体（全モニタのバウンディングボックス）に合わせて配置・サイズ変更する。</summary>
    private void SetWindowSizeToVirtualScreen()
    {
        var screens = Screens.All;
        if (screens.Count == 0) return;

        var bounds = screens[0].Bounds;
        foreach (var screen in screens)
            bounds = bounds.Union(screen.Bounds);
        Position = bounds.Position;
        Width = bounds.Width / _scaling;
        Height = bounds.Height / _scaling;
    }

    /// <summary>選択コントロール（FadeMaskDragRectControl）を設定する。</summary>
    /// <param name="s">キャプチャ設定（全メンバー non-null）。</param>
    /// <remarks>
    /// 複合コントロール（DragRect + FadeMask + Loupe の配線を内包）へモード・外観・座標変換を設定する。<br/>
    /// - モード: キャプチャ種別に応じて描画モード・DragRect / Loupe の有効・無効・全面暗転を一括切替。<br/>
    /// - 外観: 設定変換（<see cref="FadeMaskDragRectSettingsExtensions.ToFadeMaskDragRectSettings"/>）で一括適用。<br/>
    /// - 座標変換: DomainScale（ドメイン = 仮想スクリーンピクセル = ウィンドウローカル DIP × scaling）。
    /// </remarks>
    private void ConfigureSelectionControls(CaptureSettings s)
    {
        // モード（描画モード・DragRect / Loupe の有効・無効・全面暗転を一括切替）
        // - AreaSelect: DragRect + FadeMask + Loupe 有効（全面暗転 + 選択矩形 + ルーペ）
        // - ScreenSelect / WindowSelect: FadeMask のみ有効（ハイライト表示。クリック確定フローは本クラスが担う）
        // 内包コントロール（ルーペ / ドラッグ）の詳細ログ（Trace）をホストのログ基盤へ転送する（不具合調査用）。
        SelectionOverlay.Log = _log;
        SelectionOverlay.Mode = _captureType switch
        {
            CaptureType.AreaSelect => FadeMaskDragRectMode.AreaSelect,
            CaptureType.ScreenSelect => FadeMaskDragRectMode.ScreenSelect,
            _ => FadeMaskDragRectMode.WindowSelect,
        };
        // 外観設定（DragRect / FadeMask / Loupe を一括適用。設定変換は拡張メソッドに一元化）
        SelectionOverlay.AppearanceSettings =
            s.ToFadeMaskDragRectSettings(positionOffset: 20.0);
        // DPI スケーリング・ルーペ描画スケールは AppearanceSettings の対象外（コードから動的設定）。
        // 画面キャプチャはドメイン = 仮想スクリーンピクセルのため、ウィンドウのスケーリング（_scaling）を設定する。
        SelectionOverlay.Scaling = _scaling;
        SelectionOverlay.DisplayScaleScaling = _scaling;
        SelectionOverlay.LoupeRenderScaling = s.LoupeRenderScaling!.Value;
        // サイズラベル: 選択中の矩形（ドメイン = 画面物理 − Position）と、そのモニタの実スケーリングから
        // 描画時に直接生成する（物理ピクセルはスケーリングに依存しない。DIP 換算の往復誤差を排除）。
        // ハイライト（ScreenSelect / WindowSelect）・選択（AreaSelect）の順で優先し、未選択は設定値に委ねる。
        SelectionOverlay.SizeLabelOverrideProvider = () =>
        {
            if (SelectionOverlay.LogicalHighlightRect is { } highlight)
            {
                return FormatDomainSizeLabel(highlight, GetMonitorScalingForRect(highlight));
            }
            if (SelectionOverlay.HasSelection)
            {
                var selection = SelectionOverlay.SelectionRect;
                return FormatDomainSizeLabel(selection, GetMonitorScalingForRect(selection));
            }
            return s.SizeLabelOverride;
        };
        // 選択矩形・ハイライトはドメイン座標（仮想スクリーンピクセル）で設定し、描画時に DIP へ変換する（DragRect と同方式）
        // DomainScale: Forward = DIP→ドメイン（× scaling）/ Inverse = ドメイン→DIP（÷ scaling）
        // DragRect / FadeMask / Loupe の 3 コントロールへ同一ポートを一括分配する。
        SelectionOverlay.SetCoordinateTransform(CoordTransform.DomainScale(() => _scaling));
    }

    /// <summary>選択状態をオーバーレイ描画へ反映する（AreaSelect 用）。</summary>
    /// <param name="selectionRect">選択矩形（ドメイン = 仮想スクリーンピクセル）。</param>
    /// <remarks>単体テストから呼び出し可能（internal）。</remarks>
    internal void UpdateOverlay(Rect selectionRect)
    {
        // ドメイン（仮想スクリーンピクセル）の選択矩形を複合コントロール（FadeMask + ルーペ）へ反映する。
        // DIP 変換（÷ scaling）+ 閉区間（終点 +1）は描画時に座標変換ポート（DomainScale）が行う。
        SelectionOverlay.SetSelection(selectionRect);
        // ドラッグ後の枠（選択矩形）を事前切り出しする（確定時の高速化。同じ矩形ならスキップ）。
        // ドメイン（仮想スクリーンピクセル）はウィンドウ Position 基準のため、画面物理座標へは Position を加算する。
        if (selectionRect.Width > 0 && selectionRect.Height > 0)
        {
            PreExtractClip(DomainToScreenRect(selectionRect, Position));
        }
    }

    /// <summary>選択状態をクリアして全面暗転（穴あけなし）に戻す（新規選択開始・キャンセル時）。</summary>
    /// <remarks>
    /// 矩形外クリックで新規選択が開始されると DragRect の選択矩形はサイズ 0 になり非表示になるが、<br/>
    /// ドラッグ開始（SelectionChanged）までは FadeMask に前の矩形が残る。<br/>
    /// ここで選択状態をクリアして全面暗転に戻す（押下直後の FadeMask 残留を防止）。<br/>
    /// 複合コントロールの <see cref="FadeMaskDragRectControl.ClearSelection"/> へ委譲する。<br/>
    /// ShowFullOverlay=true のため、選択なしの場合は全面暗転が表示される。
    /// </remarks>
    private void ClearSelectionOverlay() => SelectionOverlay.ClearSelection();

    /// <summary>選択オーバーレイ（SelectionOverlay）の再描画を要求する。</summary>
    /// <remarks>
    /// 複合コントロールの描画状態（ハイライト・選択等）を変更した後に呼び出す。<br/>
    /// ウィンドウ自身の <c>InvalidateVisual</c> では子コントロール（SelectionOverlay）の再描画は
    /// トリガーされないため、コントロール自身に要求する必要がある。<br/>
    /// 単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal void RequestOverlayRender() => SelectionOverlay.InvalidateOverlay();

    /// <summary>AreaSelect のドメイン矩形（仮想スクリーンローカル物理ピクセル）を画面物理座標へ変換する。</summary>
    /// <param name="domainRect">ドメイン矩形（= ウィンドウローカル DIP × scaling）。</param>
    /// <param name="windowPos">ウィンドウの画面物理位置（= 仮想スクリーン原点）。</param>
    /// <returns>画面物理座標の矩形。</returns>
    /// <remarks>
    /// ドメインは既にスケーリング済み（DIP × scaling）のため、<paramref name="windowPos"/> の加算のみで変換する。<br/>
    /// キャプチャ（<see cref="ICaptureService.CaptureArea"/>）の引数と一致させるために使用する（座標ずれの回帰防止）。
    /// </remarks>
    internal static Rect DomainToScreenRect(Rect domainRect, PixelPoint windowPos)
        => new(
            domainRect.X + windowPos.X, domainRect.Y + windowPos.Y,
            domainRect.Width, domainRect.Height);

    /// <summary>半開区間の矩形（<c>幅 = 終点 − 始点</c>）を閉区間の PixelRect（始点・終点のピクセルを含む）へ変換する。</summary>
    /// <param name="rect">半開区間の矩形（画面物理座標）。</param>
    /// <returns>閉区間の PixelRect（幅・高さが +1 された矩形）。</returns>
    /// <remarks>
    /// DragRect の矩形は半開区間のため、そのままキャプチャすると終点のピクセルが欠ける。<br/>
    /// 始点・終点の両ピクセルを切出し対象に含める（閉区間キャプチャ）ために <c>Width / Height を +1</c> する。<br/>
    /// 単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal static PixelRect ToClosedIntervalPixelRect(Rect rect)
        => new(
            (int)rect.X, (int)rect.Y,
            (int)rect.Width + 1, (int)rect.Height + 1);

    /// <summary>キャプチャ種別に応じたキャプチャ矩形（画面物理座標・PixelRect）へ変換する。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="screenRect">選択矩形（画面物理座標）。</param>
    /// <returns>キャプチャ矩形。</returns>
    /// <remarks>
    /// AreaSelect: ドラッグ選択の半開区間を閉区間化（+1）して終点ピクセルまで含める（<see cref="ToClosedIntervalPixelRect"/>）。<br/>
    /// ScreenSelect / WindowSelect: 画面・ウィンドウの矩形は最初から全ピクセルを含むため、<br/>
    /// +1 すると画面外（ウィンドウ外）に 1px はみ出す。そのため +1 せずそのまま使用する。<br/>
    /// 純粋関数のため静的メソッドとする（ヘッドレス不要で単体テスト可能）。
    /// </remarks>
    internal static PixelRect ToCapturePixelRect(CaptureType captureType, Rect screenRect)
        => captureType == CaptureType.AreaSelect
            ? ToClosedIntervalPixelRect(screenRect)
            : new PixelRect((int)screenRect.X, (int)screenRect.Y, (int)screenRect.Width, (int)screenRect.Height);

    /// <summary>ルーペを更新する（AreaSelect 用）。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン = 仮想スクリーンピクセル）。</param>
    /// <remarks>
    /// 表示位置・サイズは LoupeControl が座標変換ポート（SetCoordinateTransform）から<br/>
    /// 自己計算する（カーソル右下 + オフセット + クランプ）。単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal void UpdateLoupe(Point domainPos)
    {
        // 仮想スクリーン Bitmap が未取得の間はルーペを表示しない（Source 未設定の市松模様を防ぐ）。
        // 表示開始は SetVirtualScreen（Source 設定後）が行う（ルーペの IsVisible = true は SetLoupePosition が担う）。
        if (_virtualScreenBitmap is null) return;

        // ルーペの表示開始と中心更新は複合コントロールへ委譲する（Parent 未接続は無視）。
        // ソース: ドメイン（仮想スクリーンピクセル = ウィンドウローカル DIP × scaling）は
        // ウィンドウ Position = 仮想スクリーン原点のため、既に仮想スクリーン Bitmap 上の座標と一致する。
        // 仮想スクリーン原点（_virtualScreenOrigin）を更に引くとマルチモニタ（原点が (0,0) でない）でずれるため引かない。
        // ルーペの拡大元画像（Source）は SetVirtualScreen で一度だけ設定済み（EsBitmapAvaloniaImage）のため、
        // ここでは中心位置の更新のみ行う（MouseMove 毎の全画面コピー生成と破棄済み Bitmap 残留を防ぐ）。
        SelectionOverlay.SetLoupePosition(domainPos);
    }

    // ====================================================================
    // テスト用アクセサ（回帰テストからの検証用）
    // ====================================================================

    /// <summary>固定背景画像（VirtualScreenImage）（テスト用アクセサ）。</summary>
    internal Image VirtualScreenImageControl => VirtualScreenImage;

    /// <summary>ウィンドウのイベント購読を設定する。</summary>
    private void SubscribeToEvents()
    {
        if (_captureType == CaptureType.AreaSelect)
        {
            // AreaSelect: DragRect（複合コントロール内）の確定・キャンセル要求を購読する。
            // Enter/Esc キー（OnTopLevelKeyDown）とダブルクリック（OnPointerPressed）は
            // ConfirmRequested / CancelRequested を発火するため、ここで接続して確定・キャンセルする。
            SelectionOverlay.ConfirmRequested += OnAreaConfirmRequested;
            SelectionOverlay.CancelRequested += OnAreaCancelRequested;
        }
        else
        {
            // ScreenSelect / WindowSelect: クリック確定フロー（PointerMoved / Pressed / Released）
            PointerMoved += OnClickMoved;
            PointerPressed += OnClickPressed;
            PointerReleased += OnClickReleased;
            // Enter / Esc キーによる確定・キャンセル（キーボード操作）
            KeyDown += OnClickKeyDown;
        }
        Opened += OnWindowOpened;
        Deactivated += OnOverlayWindowDeactivated;
        Closed += OnOverlayWindowClosed;
    }

    /// <summary>ScreenSelect / WindowSelect のキー操作（Enter = 確定 / Esc = キャンセル）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// - Enter: 現在のカーソル位置で検出される要素（スクリーン / ウィンドウ）を確定する。<br/>
    /// - Esc: キャプチャをキャンセルする。<br/>
    /// ハイライト矩形（LogicalHighlightRect）が未設定の場合は Enter を無視する（確定対象なし）。
    /// </remarks>
    private void OnClickKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CompleteCancelled();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            // 現在のカーソル位置で検出される要素を確定する（ハイライト矩形を画面物理座標へ変換）
            if (SelectionOverlay.LogicalHighlightRect is { } highlightLogical)
            {
                var bounds = new Rect(
                    highlightLogical.X + Position.X,
                    highlightLogical.Y + Position.Y,
                    highlightLogical.Width,
                    highlightLogical.Height);
                CompleteSelection(bounds);
                e.Handled = true;
            }
        }
    }

    /// <summary>AreaSelect の確定要求（Enter キー・ダブルクリック等）を受けてキャプチャを確定する。</summary>
    /// <param name="sender">イベント送信元（FadeMaskDragRectControl）。</param>
    /// <param name="e">選択矩形イベント（Rect はドメイン = 仮想スクリーンピクセル）。</param>
    /// <remarks>
    /// ドメイン（仮想スクリーンピクセル）の選択矩形を画面物理座標へ変換（Position 加算）して確定する。<br/>
    /// キャプチャは <see cref="CompleteSelection"/> が仮想スクリーン Bitmap から切り出す。
    /// </remarks>
    private void OnAreaConfirmRequested(object? sender, FadeMaskDragRectSelectionEventArgs e)
    {
        _log.LogOverlayConfirm(e.SelectionRect.ToString());
        var screenRect = DomainToScreenRect(e.SelectionRect, Position);
        CompleteSelection(screenRect);
    }

    /// <summary>AreaSelect のキャンセル要求（Esc キー・ダブルクリック外等）を受けてキャプチャをキャンセルする。</summary>
    /// <param name="sender">イベント送信元（FadeMaskDragRectControl）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnAreaCancelRequested(object? sender, EventArgs e) => CompleteCancelled();
}


using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;

/// <summary>画面キャプチャ用の切り取りオーバーレイウィンドウ（UI 部品）。</summary>
/// <remarks>
/// 全画面表示の透明ウィンドウとして動作し、マウス操作による領域選択やウィンドウ選択を実現する。<br/>
/// 3 種類のキャプチャモード（全画面 / ウィンドウ選択 / 範囲選択）に対応する。<br/>
/// <br/>
/// 状態は持たない（選択矩形・ハイライトは複合コントロール <see cref="SelectionOverlayControl"/> が保持）。<br/>
/// - AreaSelect: 複合コントロール（Marquee + Loupe）でドラッグ選択し、ルーペで拡大表示する。<br/>
/// - ScreenSelect / WindowSelect: ウィンドウの Pointer イベントから <see cref="IElementFinder"/> で検出し、<br/>
///   複合コントロールのハイライト状態（HighlightSourceRect / HasHighlight 等）に設定する。<br/>
/// 確定フロー（非表示 → DWM 待機 → キャプチャ）は本クラスが直接行う（<see cref="ICaptureService"/> に委譲）。
/// </remarks>
internal sealed partial class CaptureOverlayWindow : Window
{
    /// <summary>キャプチャ種別。</summary>
    private readonly CaptureType _captureType;

    /// <summary>キャプチャ完了コールバック（確定・キャンセル・エラー時に呼ばれる）。 切り出し矩形は画面物理座標。</summary>
    private readonly Action<EsBitmap?, EsRectInt?> _onComplete;

    /// <summary>DPI スケーリング係数（プライマリスクリーン基準）。</summary>
    private readonly double _scaling;

    /// <summary>ネイティブウィンドウ操作（Alt+Tab 非表示・ハング時退避）。 プラットフォーム抽象。</summary>
    private readonly IWindowService _windowService;

    /// <summary>画面キャプチャ（プラットフォーム抽象）。</summary>
    private readonly ICaptureService _captureService;

    /// <summary>ウィンドウ/スクリーン検出（プラットフォーム抽象）。</summary>
    private readonly IElementFinder _detector;

    /// <summary>内部ロガー。</summary>
    private readonly ILogger _log;

    /// <summary>セッションの状態（再入禁止・完了は 1 回だけ）。</summary>
    /// <remarks>
    /// 「確定とキャンセルの競合」「非アクティブ化とキー入力の重なり」で二重完了にしないための規則は<br/>
    /// コアの <see cref="CaptureSessionState"/> が単一所有する（UI 側でフラグを手書きしない）。<br/>
    /// バックグラウンドの完了通知からも触れるため、排他はコアが行う。
    /// </remarks>
    private readonly CaptureSessionState _state = new();

    /// <summary>クリック確定型の押下時に固定したハイライト矩形（固定なしの解放では確定しない）。 コアの状態が単一所有する。</summary>
    private readonly CaptureClickSelection _clickSelection = new();

    /// <summary>仮想スクリーン全体の画像（全モード共通のキャプチャソース・切り出し元）を取得する。</summary>
    /// <value>撮影した画像（読み取りの面。 保持するコアのクラスが所有する）。 未取得（背景撮影中・取得失敗）の場合は null。</value>
    private EsReadOnlyBitmap? VirtualScreenBitmap => _captureImage.Image;

    /// <summary>事前切り出し済みのキャッシュ（同一判定・所有権の受け渡し・旧値の破棄をコアが単一所有する）。</summary>
    private readonly CaptureClipCache _clipCache = new();

    /// <summary>モニタごとの矩形と実表示倍率（構築時に 1 回だけ解決する）。</summary>
    private IReadOnlyList<(EsRectInt Bounds, double Scale)> _monitors = [];

    /// <summary>ウィンドウが覆う仮想スクリーンの範囲（画面物理ピクセル）。 画像未取得時の切り出し範囲の判定に使う。</summary>
    private EsRectInt _screenBounds;

    /// <summary>撮影した画像の保持（差し替え時の旧値の破棄・クローズ後の破棄・未取得時の範囲をコアが単一所有する）。</summary>
    private readonly CaptureImageHolder _captureImage;

    /// <summary>表示境界の画像（<see cref="VirtualScreenImage"/>・ルーペの Source が共有する）。 本ウィンドウが破棄する。</summary>
    /// <remarks>
    /// <c>IImage</c>（Avalonia の表示用画像）は非所有の契約のため、設定しただけでは破棄されない。 <see cref="SetVirtualScreen"/> の差し替え時と
    /// クローズ時に本ウィンドウが <see cref="EsBitmapAvaloniaAdapter.Dispose"/> を呼ぶ（元画像は <see cref="_captureImage"/> が所有する）。
    /// </remarks>
    private EsBitmapAvaloniaAdapter? _displayImage;

    /// <summary>カーソル下の要素のハイライト状態（対象座標への変換・表示名・ログをコアが単一所有する）。</summary>
    private readonly CaptureHighlightSession _highlight;

    /// <summary>事前切り出しの実行回数（テスト検証用アクセサ）。</summary>
    internal int PreExtractCount => _clipCache.StoreCount;

    /// <summary>CaptureOverlayWindow を初期化する。</summary>
    /// <param name="windowService">ネイティブウィンドウ操作（Alt+Tab 非表示・ハング時退避）。</param>
    /// <param name="captureService">画面キャプチャ（プラットフォーム抽象）。</param>
    /// <param name="detector">ウィンドウ/スクリーン検出（プラットフォーム抽象）。</param>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="settings">キャプチャ設定（全メンバー non-null）。</param>
    /// <param name="scaling">DPI スケーリング係数。</param>
    /// <param name="onComplete">キャプチャ完了コールバック（切り出し矩形は画面物理座標）。</param>
    /// <param name="logger">内部ロガー。 null の場合は出力なし。</param>
    /// <remarks>オーバーレイの生成が 1 回の撮影セッションの開始となる（コアの状態へ開始を記録する）。</remarks>
    public CaptureOverlayWindow(
        IWindowService windowService,
        ICaptureService captureService,
        IElementFinder detector,
        CaptureType captureType,
        CaptureSettings settings,
        double scaling,
        Action<EsBitmap?, EsRectInt?> onComplete,
        ILogger? logger = null)
    {
        InitializeComponent();
        // オーバーレイの生成 = セッション開始（以降の完了は 1 回だけ受け付ける）
        _state.TryBegin();
        _captureType = captureType;
        _onComplete = onComplete;
        _log = logger ?? NullLogger.Instance;
        _windowService = windowService;
        _captureService = captureService;
        _detector = detector;
        // スケーリング係数の正規化（0 以下は基準値）はコアが単一所有する
        _scaling = DpiScaler.NormalizeScaling(scaling);

        var s = settings.ToNonNull();

        // 十字カーソルはドラッグで矩形を作る種別（ハイライト型以外 = 範囲選択）で使う
        // （選択スタイルの判定はコアが単一所有するため、UI は導出値を保持しない）
        if (!CaptureTypeTraits.IsHighlighted(_captureType))
            Cursor = new Cursor(StandardCursorType.Cross);

        _highlight = new CaptureHighlightSession(_log);
        SetWindowSizeToVirtualScreen();
        _captureImage = new CaptureImageHolder(_screenBounds);
        ResolveMonitors();
        ConfigureSelectionControls(s);
        SubscribeToEvents();
    }

    /// <summary>モニタごとの矩形と実表示倍率を解決する（構成はセッション中に変わらないため 1 回だけ行う）。</summary>
    /// <remarks>
    /// サイズラベルの表示倍率は描画のたびに評価されるため、ここで解決した一覧を再利用する<br/>
    /// （毎回 <c>Screens.All</c> を列挙して配列を作ると、ドラッグ中の再描画ごとに割り当てが発生する）。<br/>
    /// 判定規則（矩形の中心を含むモニタ）と倍率未確定の除外はコアの <see cref="MonitorScalingMath"/> が単一所有する。
    /// </remarks>
    private void ResolveMonitors()
        => _monitors = MonitorScalingMath.Collect(
            Screens.All.Select(screen => (screen.Bounds.ToEsRectInt(), screen.Scaling)));

    /// <summary>ウィンドウを仮想スクリーン全体（全モニタの包含矩形）に合わせて配置・サイズ変更する。</summary>
    /// <remarks>
    /// 撮影対象の範囲（全モニタの包含矩形）と「モニタが無ければ撮影しない」という規則は<br/>
    /// コアの <see cref="CaptureVirtualScreen.ResolveBounds"/> が単一所有する（UI は配置するだけ）。<br/>
    /// 配置に使った範囲は切り出し範囲の判定（画像未取得時のクランプ）にも使うため保持する。
    /// </remarks>
    private void SetWindowSizeToVirtualScreen()
    {
        // 包含矩形の計算と空の判定はコアが単一所有する（空の場合は撮影する領域が無い）
        if (CaptureVirtualScreen.ResolveBounds(
                Screens.All.Select(screen => screen.Bounds.ToEsRectInt())) is not { } bounds)
        {
            return;
        }

        // 物理ピクセル → DIP の換算は Platform 層の純粋関数（係数の正規化を含む）に委譲する
        Position = bounds.TopLeft.ToAvaloniaPixelPoint();
        Width = DpiScaler.ToDip(bounds.Width, _scaling);
        Height = DpiScaler.ToDip(bounds.Height, _scaling);
        _screenBounds = bounds;
    }

    /// <summary>選択コントロール（SelectionOverlayControl）を設定する。</summary>
    /// <param name="s">キャプチャ設定（全メンバー non-null）。</param>
    /// <remarks>
    /// 複合コントロール（Marquee + Loupe の配線を内包）へモード・外観・座標変換を設定する。<br/>
    /// - モード: キャプチャ種別に応じて描画モード・Marquee / Loupe の有効・無効・全面暗転を一括切替。<br/>
    /// - 設定: 設定変換（<c>SelectionOverlayOptionsMapper.ToSelectionOverlayOptions</c>）で 1 値へ変換して一括適用。<br/>
    /// - 座標変換: ClientToSource（対象 = ウィンドウローカル物理ピクセル = ローカル DIP × scaling。 画面物理へは Position 加算）。
    /// </remarks>
    private void ConfigureSelectionControls(CaptureSettings s)
    {
        // モード（描画モード・Marquee / Loupe の有効・無効・全面暗転を一括切替）
        // - AreaSelect: Marquee + Loupe 有効（全面暗転 + 選択矩形 + ルーペ）
        // - ScreenSelect / WindowSelect: Marquee のみ有効（ハイライト表示。 クリック確定フローは本クラスが担う）
        // 内包コントロール（ルーペ / ドラッグ）の詳細ログ（Trace）をホストのログ基盤へ転送する（不具合調査用）。
        SelectionOverlay.Log = _log;
        // モード写像（キャプチャ種別 → 動作モード）はキャプチャコアが単一所有する
        SelectionOverlay.Mode = CaptureLabelTexts.ToSelectionMode(_captureType);
        // 設定を複合コントロールの設定値（1 値）へ変換して一括適用する（変換はコアのマッパーが単一所有する）。
        // ルーペの市松模様の下地色は塗らない（Avalonia 版は背後に固定背景画像を置くため透明で透かす）
        SelectionOverlay.Options = s.ToSelectionOverlayOptions();
        // 描画に使う色（暗転・枠線・ハンドル・ラベル・ルーペ）は設定から写像する
        // （設定を反映しないと、切り出し（Crop）とキャプチャで見た目が食い違う）
        SelectionOverlay.MarqueeAppearance = CapturePaintMapper.ToMarqueeAppearance(s);
        SelectionOverlay.LoupeAppearance = CapturePaintMapper.ToLoupeAppearance(s);
        // DPI スケーリング・ルーペ描画スケールは設定（Options）の対象外（環境値。 コードから動的設定）。
        // 対象はウィンドウローカル物理ピクセル（= ローカル DIP × スケーリング）のため、ウィンドウのスケーリング（_scaling）を設定する。
        SelectionOverlay.ClientToSourceScaling = _scaling;
        SelectionOverlay.DisplayScaleScaling = _scaling;
        SelectionOverlay.LoupeClientToPixelScaling = s.LoupeClientToPixelScaling!.Value;
        // サイズラベル: 文言（書式）はコアが単一所有し、本ホストは矩形を含むモニタの実スケール（マルチモニタ補正）だけを提供する。
        // 対象はウィンドウローカル物理ピクセルのため、コアは DIP × スケーリングで物理ピクセルへ換算する（DIP 換算の往復誤差を排除）。
        SelectionOverlay.SizeLabelDisplayScalingProvider = GetMonitorScalingForRect;
        // 操作ラベル・情報ラベルの文言はホスト（キャプチャ文脈）が与える（汎用コアは文言を持たない）。
        // モード別の文言と情報ラベルの表示可否は CaptureLabelTexts が単一所有し、
        // 情報ラベルに出すウィンドウタイトルはハイライト状態（コアが単一所有する）を参照する。
        SelectionOverlay.OperationTextProvider = () => CaptureLabelTexts.GetOperationText(SelectionOverlay.Mode);
        SelectionOverlay.InfoTextProvider = () => CaptureLabelTexts.GetInfoText(SelectionOverlay.Mode, _highlight.Title);
        // 選択矩形・ハイライトは対象座標（ウィンドウローカル物理ピクセル）で設定し、描画時に DIP へ変換する（Marquee と同方式）
        // ClientToSource: ToSource = DIP→対象（× scaling）/ ToDip = 対象→DIP（÷ scaling）
        // Marquee / Loupe の 2 コントロールへ同一ポートを一括分配する。
        SelectionOverlay.SetCoordTransform(CoordTransform.ClientToSource(() => _scaling));
    }

    /// <summary>選択オーバーレイ（SelectionOverlay）の再描画を要求する。</summary>
    /// <remarks>
    /// 複合コントロールの描画状態（ハイライト・選択等）を変更した後に呼び出す。<br/>
    /// ウィンドウ自身の <c>InvalidateVisual</c> では子コントロール（SelectionOverlay）の再描画は
    /// トリガーされないため、コントロール自身に要求する必要がある。<br/>
    /// 単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal void RequestOverlayRender() => SelectionOverlay.RequestRedraw();

    /// <summary>切り出し元画像の占有範囲（画面物理ピクセル）を取得する。</summary>
    /// <value>仮想スクリーン画像があればその範囲、未取得（背景撮影中）ならウィンドウが覆う仮想スクリーンの範囲。</value>
    /// <remarks>
    /// 切り出し矩形の決定（閉区間化・画像内へのクランプ）はコア（<see cref="CropClipMath.ResolveCaptureRect(CaptureType, EsRectInt, EsRectInt)"/>）が単一所有する。<br/>
    /// 未取得時のフォールバック（どちらの取得経路を使うか）もコアの保持クラスが単一所有する。
    /// </remarks>
    private EsRectInt CaptureImageBounds => _captureImage.Bounds;

    // ====================================================================
    // テスト用アクセサ（回帰テストからの検証用）
    // ====================================================================

    /// <summary>固定背景画像（VirtualScreenImage）（テスト用アクセサ）。</summary>
    internal Image VirtualScreenImageControl => VirtualScreenImage;

    /// <summary>ウィンドウのイベント購読を設定する。</summary>
    private void SubscribeToEvents()
    {
        if (CaptureTypeTraits.IsHighlighted(_captureType))
        {
            // ScreenSelect / WindowSelect: クリック確定フロー（PointerMoved / Pressed / Released）
            PointerMoved += OnClickMoved;
            PointerPressed += OnClickPressed;
            PointerReleased += OnClickReleased;
            // Enter = 確定 / Esc = キャンセル（キーボード操作）。
            // 内包する選択オーバーレイ（Marquee）も TopLevel のキーを購読し、Enter / Escape を
            // 処理済みにする（選択を持たないハイライト型でも常に処理済みにされるため）。
            // 本ウィンドウが必ず受け取れるよう、Tunnel + handledEventsToo で購読する。
            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        else
        {
            // AreaSelect: Marquee（複合コントロール内）の確定・キャンセル要求を購読する。
            // Enter/Esc キー（OnTopLevelKeyDown）とダブルクリック（OnPointerPressed）は
            // ConfirmRequested / CancelRequested を発火するため、ここで接続して確定・キャンセルする。
            SelectionOverlay.ConfirmRequested += OnAreaConfirmRequested;
            SelectionOverlay.CancelRequested += OnAreaCancelRequested;
        }
        Opened += OnWindowOpened;
        Deactivated += OnOverlayWindowDeactivated;
        Closed += OnOverlayWindowClosed;
    }

    /// <summary>ScreenSelect / WindowSelect のキー操作（Enter = 確定 / Esc = キャンセル）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// どのキーがどの操作かの解釈はコアの <see cref="CaptureKeyActions"/> が単一所有する<br/>
    /// （確定対象が無い場合の Enter は何もしない。 キャンセル扱いにしない）。<br/>
    /// 確定対象の判定もコア（<see cref="CaptureTypeTraits.ResolveRect"/>）へ委譲する。<br/>
    /// 確定はハイライト矩形（対象座標）を画面物理座標へ戻して行う。
    /// </remarks>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var target = ResolveTargetRect();
        switch (CaptureKeyActions.Resolve(e.Key.ToEsKey(), target is not null))
        {
            case CaptureKeyAction.Confirm when target is { } rect:
                CompleteSelection(CropClipMath.ToScreenRect(rect, Position.ToEsPointInt()));
                e.Handled = true;
                break;
            case CaptureKeyAction.Cancel:
                CompleteCancelled();
                e.Handled = true;
                break;
        }
    }

    /// <summary>AreaSelect の確定要求（Enter キー・ダブルクリック等）を受けてキャプチャを確定する。</summary>
    /// <param name="sender">イベント送信元（SelectionOverlayControl）。</param>
    /// <param name="e">選択矩形イベント（Rect は対象 = ウィンドウローカル物理ピクセル）。</param>
    /// <remarks>
    /// 対象（ウィンドウローカル物理ピクセル）の選択矩形を画面物理座標へ変換（Position 加算）して確定する。<br/>
    /// 確定要求の記録と切り出しは <see cref="CompleteSelection(EsRect)"/> が担う。
    /// </remarks>
    private void OnAreaConfirmRequested(object? sender, SelectionConfirmedEventArgs e)
        // 対象（ウィンドウローカル物理ピクセル）→ 画面物理座標の変換はコアが単一所有する
        => CompleteSelection(CropClipMath.ToScreenRect(e.SelectionSourceRect.ToEsRect(), Position.ToEsPointInt()));

    /// <summary>AreaSelect のキャンセル要求（Esc キー・ダブルクリック外等）を受けてキャプチャをキャンセルする。</summary>
    /// <param name="sender">イベント送信元（SelectionOverlayControl）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnAreaCancelRequested(object? sender, EventArgs e) => CompleteCancelled();
}




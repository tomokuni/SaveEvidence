using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Feature.Loupe.Presenter.Setting;
using EsUtil.Feature.Marquee.Presenter.Setting;
using EsUtil.Feature.Loupe.AvaloniaUI.LoupeControl;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI;

/// <summary>
/// 画像表示（拡大・パン・Fit）と領域選択（ドラッグ・暗転・ルーペ）を統合した高レベル複合コントロール。
/// </summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（<see cref="ZoomViewControl"/> と <see cref="FadeMaskDragRectControl"/> を内包し、
/// ホストへ統一インターフェースを提供）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="ZoomViewControl"/>: 画像ビューポート（拡大・パン・Fit・原寸）。<br/>
/// - <see cref="FadeMaskDragRectControl"/>: 領域選択オーバーレイ（ドラッグ選択・暗転マスク・ルーペ）。<br/>
/// - 矩形オーバーレイ: ホストから渡された <see cref="OverlayRect"/> をビューポート座標へ変換して表示する。<br/>
/// <br/>
/// <b>責務分離</b>:<br/>
/// - 画像表示（ズーム・パン）は <see cref="ZoomViewControl"/> が担う（ズーム率管理は本コントロール）。<br/>
/// - 領域選択（ドラッグ・暗転・ルーペ）は <see cref="FadeMaskDragRectControl"/> が担う。<br/>
/// - 外部提供矩形（<see cref="Rects"/>）の描画は本コントロールが担う（View / ルーペの両方へ描画）。<br/>
/// <br/>
/// <b>ライフサイクル</b>:<br/>
/// - 画像（<see cref="Source"/>）はホストが設定する。<br/>
/// - ズーム操作（<see cref="ZoomIn"/> / <see cref="ZoomOut"/> / Fit）は本コントロールが提供する。<br/>
/// - 選択状態の外部設定（<see cref="SetSelection"/> / <see cref="ClearSelection"/>）は <see cref="FadeMaskDragRectControl"/> へ委譲する。
/// </remarks>
public sealed partial class CropViewControl : Panel
{
    /// <summary>ズーム・パンビューポート（自己完結型複合コントロール。 拡大・パン・Fit）。</summary>
    private readonly ZoomViewControl _zoomView = new() { ZIndex = 0 };

    /// <summary>領域選択オーバーレイ（ドラッグ選択・暗転マスク・ルーペ）。</summary>
    /// <remarks>画像がない間は <see cref="Source"/> に応じてヒットテストを切るとともに非表示にする（画像なしでの選択矩形作成と全面暗転を防止）。</remarks>
    private readonly FadeMaskDragRectControl _selectionOverlay = new() { ZIndex = 10, IsHitTestVisible = false, IsVisible = false };

    /// <summary>内部ロガー（ズームビューポートへ伝搬）。 null の場合は <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger"/>。</summary>
    /// <remarks><see cref="ZoomViewControl.Log"/> へ委譲する（ズーム操作・座標変換の詳細ログをホストのログ基盤へ転送）。</remarks>
    public ILogger Log
    {
        get => _zoomView.Log;
        set => _zoomView.Log = value;
    }

    /// <summary>外部提供矩形オーバーレイ（ビューポート座標で描画）。</summary>
    private readonly Canvas _rectOverlay = new() { ZIndex = 20, IsHitTestVisible = false };

    /// <summary>
    /// <see cref="CropViewControl"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <remarks>
    /// 子コントロール（<see cref="ZoomViewControl"/> / <see cref="FadeMaskDragRectControl"/> / 矩形オーバーレイ）を生成して配置し、<br/>
    /// ビューポート変化（パン・ズーム）を配線する。<br/>
    /// ズーム要求（Ctrl+ホイール）・ズーム率の遷移は <see cref="ZoomViewControl"/> が自己完結する。<br/>
    /// 領域選択オーバーレイ上のホイールは本コントロールがビューポートへ転送する（Crop 中もパン・ズームを有効にする）。
    /// </remarks>
    public CropViewControl()
    {
        _zoomView.HorizontalAlignment = HorizontalAlignment.Stretch;
        _zoomView.VerticalAlignment = VerticalAlignment.Stretch;
        Children.Add(_zoomView);
        Children.Add(_selectionOverlay);
        Children.Add(_rectOverlay);

        // ビューポート変化（パン・ズーム）を領域選択オーバーレイと矩形オーバーレイへ中継する
        _zoomView.ViewportChanged += (_, _) =>
        {
            _selectionOverlay.RefreshOverlay();
            UpdateRectOverlay();
        };

        // 領域選択オーバーレイ上のホイールをビューポートへ転送する。
        // オーバーレイは Crop 中に画像の上を覆って入力を占有するため、ホイールはオーバーレイだけが受け取る
        // （ビューポートは兄弟要素のため、オーバーレイ側で処理済みにしたイベントは届かない）。
        // 回転量から移動量への換算とズーム率の決定はビューポート（ZoomViewControl）が単一所有する。
        _selectionOverlay.PointerWheelChanged += OnSelectionOverlayWheelChanged;

        // ルーペ描画完了時に、外部提供矩形をルーペ Bitmap へ描画する
        _selectionOverlay.LoupeRendered += OnLoupeRendered;

        // 既定は Crop（プレビュー内の切出し）。 ホストが SelectionMode で変更できる
        _selectionOverlay.Mode = FadeMaskDragRectMode.Crop;
    }

    /// <summary>領域選択オーバーレイのホイールをビューポート（ズーム・パン）へ転送する。</summary>
    /// <param name="sender">イベント送信元（領域選択オーバーレイ）。</param>
    /// <param name="e">ホイールイベント。</param>
    /// <remarks>
    /// Ctrl 併用はズーム（発生位置をズーム中心にする）、それ以外はパンとする。<br/>
    /// 解釈（どちらを選ぶか）と回転量 → 移動量の換算はビューポート（<c>ZoomViewControl</c>）が単一所有する<br/>
    /// （オーバーレイは入力を受けるだけであり、ビューの状態を変更しない）。
    /// </remarks>
    private void OnSelectionOverlayWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            _zoomView.ZoomByWheel(e.Delta.Y > 0, ToViewportPosition(e.GetPosition(_selectionOverlay).ToEsPoint()));
        }
        else
        {
            _zoomView.PanByWheel(
                new EsVector(e.Delta.X, e.Delta.Y).ToAvaloniaVector(), (e.KeyModifiers & KeyModifiers.Shift) != 0);
        }

        e.Handled = true;
    }

    /// <summary>領域選択オーバーレイのローカル座標をビューポート（ズームビュー）のローカル座標へ変換する。</summary>
    /// <param name="overlayPosition">オーバーレイのローカル座標（DIP）。</param>
    /// <returns>ビューポートのローカル座標（DIP）。 同一ビジュアルツリーに無い場合は null（ズーム中心はビューポート中心になる）。</returns>
    /// <remarks>
    /// 両者は通常同一領域を占める（重ね合わせ）が、レイアウトの差に依存しないようビジュアルツリーの変換に委譲する。
    /// </remarks>
    private Point? ToViewportPosition(EsPoint overlayPosition)
        => _selectionOverlay.TranslatePoint(overlayPosition.ToAvaloniaPoint(), _zoomView);

    // ====================================================================
    // 公開 API: 選択モード
    // ====================================================================

    /// <summary>選択モード（既定は <see cref="FadeMaskDragRectMode.Crop"/>）。</summary>
    /// <remarks>
    /// <see cref="FadeMaskDragRectControl.Mode"/> へ委譲する（内包コントロールの有効・無効と描画モードを一括切替）。
    /// </remarks>
    public FadeMaskDragRectMode SelectionMode
    {
        get => _selectionOverlay.Mode;
        set => _selectionOverlay.Mode = value;
    }

    /// <summary>領域選択オーバーレイの設定（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線）。<see cref="FadeMaskDragRectControl.Options"/> へ委譲する。</summary>
    /// <value>設定の単一値。 既定は <see cref="FadeMaskDragRectOptions.Default"/>。</value>
    public FadeMaskDragRectOptions SelectionOverlayOptions
    {
        get => _selectionOverlay.Options;
        set => _selectionOverlay.Options = value;
    }

    /// <summary>DPI スケーリング係数（<see cref="FadeMaskDragRectControl.ClientToSourceScaling"/> へ委譲）。</summary>
    /// <remarks>ユーザー設定ではなく、セッション（Crop は 1.0）がコードから動的設定する。</remarks>
    public double ClientToSourceScaling
    {
        get => _selectionOverlay.ClientToSourceScaling;
        set => _selectionOverlay.ClientToSourceScaling = value;
    }

    /// <summary>ルーペの DIP → 物理ピクセル倍率（0.1〜8.0）。<see cref="FadeMaskDragRectControl.LoupeClientToPixelScaling"/> へ委譲する。</summary>
    /// <remarks>ユーザー設定ではなく、ウィンドウの DPI スケーリングから自動取得する。</remarks>
    public double LoupeClientToPixelScaling
    {
        get => _selectionOverlay.LoupeClientToPixelScaling;
        set => _selectionOverlay.LoupeClientToPixelScaling = value;
    }

    /// <summary>領域選択オーバーレイの選択矩形コントロールが使う色。<see cref="FadeMaskDragRectControl.MarqueeAppearance"/> へ委譲する。</summary>
    /// <value>外観の単一値。 既定は <see cref="MarqueeAppearance.Default"/>。</value>
    /// <remarks>色は描画にだけ必要な設定のため、コアの設定ではない（セッションが設定する）。</remarks>
    public MarqueeAppearance SelectionOverlayMarqueeAppearance
    {
        get => _selectionOverlay.MarqueeAppearance;
        set => _selectionOverlay.MarqueeAppearance = value;
    }

    /// <summary>領域選択オーバーレイのルーペが使う色。<see cref="FadeMaskDragRectControl.LoupeAppearance"/> へ委譲する。</summary>
    /// <value>色の単一値。 既定は <see cref="LoupeAppearance.Default"/>。</value>
    /// <remarks>色は描画にだけ必要な設定のため、コアの設定ではない（セッションが設定する）。</remarks>
    public LoupeAppearance SelectionOverlayLoupeAppearance
    {
        get => _selectionOverlay.LoupeAppearance;
        set => _selectionOverlay.LoupeAppearance = value;
    }

    /// <summary>表示する画像を設定する（ZoomViewControl とルーペへ伝搬する）。</summary>
    /// <remarks>
    /// 画像変更時は Fit 表示にリセットし、ズーム状態を適用する。<br/>
    /// オーバーレイ（領域選択）の可視性は本プロパティでは制御しない。<br/>
    /// 領域選択オーバーレイの可視性は <see cref="SetSelectionOverlayVisible"/>（Crop セッションが開始・終了時に制御）に一本化する。<br/>
    /// 画像が <c>null</c> に変わるときは既存の選択をクリアする（画像なしで選択矩形が残るのを防ぐ）。
    /// </remarks>
    public IImage? Source
    {
        get => _zoomView.Source;
        set
        {
            _zoomView.Source = value;
            UpdateLoupeSource();
            // 画像が null に変わるときは選択状態をクリアする。
            // （オーバーレイの表示・ヒットテストは SetSelectionOverlayVisible が専任。 ここでは触らない）
            if (value is null)
            {
                ClearSelection();
            }
        }
    }

    /// <summary>ルーペの拡大元画像を <see cref="FadeMaskDragRectControl"/> へ設定する。</summary>
    /// <remarks>真のソースはズームビューポートが持つため、本コントロールはフィールドへ複製せず参照する。</remarks>
    private void UpdateLoupeSource() => _selectionOverlay.SetLoupeSource(_zoomView.Source);

    // ====================================================================
    // 公開 API: 外部提供矩形
    // ====================================================================

    /// <summary>オーバーレイとして表示する矩形の一覧（対象座標）。</summary>
    /// <remarks>
    /// 設定すると View（ビューポート）とルーペ画像の両方へ描画される。<br/>
    /// 矩形情報（<see cref="OverlayRect"/>）には破線種類・前景色・背景色・線幅が含まれる。
    /// </remarks>
    public IReadOnlyList<OverlayRect> Rects
    {
        get => _rects;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _rects = value;
            UpdateRectOverlay();
        }
    }

    private IReadOnlyList<OverlayRect> _rects = [];

    // ====================================================================
    // 公開 API: ズーム
    // ====================================================================

    /// <summary>現在のズーム率（%）。 0 = Fit（自動）。 変更時は <see cref="ZoomViewControl"/> が表示を適用する。</summary>
    /// <remarks>
    /// ズーム率は <see cref="ZoomViewControl"/> が単一所有するため、getter / setter は <see cref="ZoomViewControl.ZoomPercent"/> へ委譲する。<br/>
    /// （表示・通知は <see cref="ZoomViewControl"/> が行う）。<c>double</c> でスライダ等の連続値も設定できる。
    /// </remarks>
    public double ZoomPercent
    {
        get => _zoomView.ZoomPercent;
        set => _zoomView.ZoomPercent = value;
    }

    /// <summary>ズームインする（⊖⊕ボタン相当。 段階遷移）。</summary>
    public void ZoomIn() => _zoomView.ZoomIn();

    /// <summary>ズームアウトする（⊖⊕ボタン相当。 段階遷移）。</summary>
    public void ZoomOut() => _zoomView.ZoomOut();

    /// <summary>Fit 表示（ウィンドウに合わせて画像を表示）に戻す。</summary>
    /// <remarks><see cref="ZoomViewControl.ZoomFit"/> へ委譲する（ズーム率の単一所有）。<see cref="ZoomIn"/> / <see cref="ZoomOut"/> と対になるズーム操作。</remarks>
    public void ZoomFit() => _zoomView.ZoomFit();

    /// <summary>ズーム率が変更されたときに発火するイベント（<see cref="ZoomViewControl.ZoomChanged"/> へ委譲）。</summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged
    {
        add => _zoomView.ZoomChanged += value;
        remove => _zoomView.ZoomChanged -= value;
    }

    /// <summary>ビューポート（パン・ズーム・表示状態）が変化したときに発火するイベント（<see cref="ZoomViewControl.ViewportChanged"/> へ委譲）。</summary>
    public event EventHandler? ViewportChanged
    {
        add => _zoomView.ViewportChanged += value;
        remove => _zoomView.ViewportChanged -= value;
    }

    /// <summary>現在のズーム状態でズームインが可能かどうか（<see cref="ZoomViewControl.CanZoomIn"/> へ委譲）。</summary>
    public bool CanZoomIn => _zoomView.CanZoomIn;

    /// <summary>現在のズーム状態でズームアウトが可能かどうか（<see cref="ZoomViewControl.CanZoomOut"/> へ委譲）。</summary>
    public bool CanZoomOut => _zoomView.CanZoomOut;

    /// <summary>画像の中央寄せ / 左上寄せ（<see cref="ZoomViewControl.CenterAlign"/> へ委譲）。</summary>
    public bool CenterAlign
    {
        get => _zoomView.CenterAlign;
        set => _zoomView.CenterAlign = value;
    }

    /// <summary>画像の表示矩形（ビューポート座標）を取得する（<see cref="ZoomViewControl.ImageRectViewport"/> へ委譲）。</summary>
    public Rect ImageRectViewport() => _zoomView.ImageRectViewport();

    /// <summary>ズームビューの外観（下地・市松模様・スクロールバーの色）を取得または設定する（<see cref="ZoomViewControl.Appearance"/> へ委譲）。</summary>
    /// <value>外観の単一値。 既定は <see cref="ZoomViewAppearance.Default"/>。</value>
    /// <remarks>
    /// 市松模様は画像の透明部分の背景であり、描く範囲（画像の表示矩形の内側）はコアが単一所有する<br/>
    /// （画像の外＝ペーストボードは下地色の単色になる）。 設定は <c>PreviewChecker*</c> をホストが写像する。
    /// </remarks>
    public ZoomViewAppearance Appearance
    {
        get => _zoomView.Appearance;
        set => _zoomView.Appearance = value;
    }

    // ====================================================================
    // 公開 API: 選択状態（FadeMaskDragRectControl へ委譲）
    // ====================================================================

    /// <summary>領域選択オーバーレイ（テスト用アクセサ。 画像なし時のドラッグ無効化の検証用）。</summary>
    internal FadeMaskDragRectControl SelectionOverlay => _selectionOverlay;

    /// <summary>矩形オーバーレイの子要素（テスト用アクセサ。 コア確定値の写像を検証する）。</summary>
    /// <value>矩形 1 件につき 1 つの Panel（破線の場合は背景の下層と前景の上層を含む）。</value>
    internal IReadOnlyList<Control> RectOverlayChildren => [.. _rectOverlay.Children];

    /// <summary>現在の選択状態（選択矩形の有無）。</summary>
    public bool HasSelection => _selectionOverlay.HasSelection;

    /// <summary>現在の選択矩形（対象座標）。 未選択時は 0x0 の空矩形。</summary>
    public Rect SelectionSourceRect => _selectionOverlay.SelectionSourceRect;

    /// <summary>選択状態を外部から設定する（暗転マスクとルーペへ同期）。</summary>
    /// <param name="rect">選択矩形（対象座標）。 サイズ 0 以下は選択なし。</param>
    public void SetSelection(Rect rect) => _selectionOverlay.SetSelection(rect);

    /// <summary>選択状態をクリアする（全面暗転に戻す）。</summary>
    public void ClearSelection() => _selectionOverlay.ClearSelection();

    /// <summary>領域選択オーバーレイの可視性とヒットテストを設定する（Crop セッションの開始・終了時に制御）。</summary>
    /// <param name="visible">true でオーバーレイを可視化しヒットテストを有効化する。 false で非表示かつヒットテストを無効化する。</param>
    /// <remarks>
    /// オーバーレイはプレビュー表示時（キャプチャ完了・画像読込）は非表示で、Crop セッション中のみ表示する。<br/>
    /// Crop モード（<see cref="SelectionMode"/>）では全面暗転を描画するため、プレビュー表示時に勝手に表示すると<br/>
    /// 「Crop を開始した状態」のような暗転が残る。<see cref="Source"/> 設定では自動表示せず、本メソッドで明示的に制御する。
    /// </remarks>
    public void SetSelectionOverlayVisible(bool visible)
    {
        _selectionOverlay.IsVisible = visible;
        _selectionOverlay.IsHitTestVisible = visible;
        if (!visible)
        {
            ClearSelection();
        }
    }

    /// <summary>座標変換ポートを設定する（内包コントロールへ分配）。</summary>
    /// <param name="port">座標変換ポート。 null で等価変換。</param>
    public void SetCoordTransform(ICoordTransform? port) => _selectionOverlay.SetCoordTransform(port);

    /// <summary>内包 Marquee へフォーカスを設定する（矢印キー nudge の受付開始）。</summary>
    /// <remarks><see cref="FadeMaskDragRectControl.FocusMarquee"/> へ委譲する。</remarks>
    public void FocusMarquee() => _selectionOverlay.FocusMarquee();

    /// <summary>ルーペの拡大元画像を設定する（<see cref="FadeMaskDragRectControl.SetLoupeSource"/> へ委譲）。</summary>
    /// <param name="source">ルーペの拡大元画像。 null でルーペをクリア。</param>
    public void SetLoupeSource(IImage? source) => _selectionOverlay.SetLoupeSource(source);

    /// <summary>ルーペのソースをクリアする（<see cref="FadeMaskDragRectControl.ClearLoupe"/> へ委譲）。</summary>
    public void ClearLoupe() => _selectionOverlay.ClearLoupe();

    /// <summary>サイズラベルを描画時に評価するプロバイダ（<see cref="FadeMaskDragRectControl.SizeLabelOverrideProvider"/> へ委譲）。</summary>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _selectionOverlay.SizeLabelOverrideProvider;
        set => _selectionOverlay.SizeLabelOverrideProvider = value;
    }

    /// <summary>操作ラベル（2 行目）の文言を描画時に評価するプロバイダ（<see cref="FadeMaskDragRectControl.OperationTextProvider"/> へ委譲）。</summary>
    public Func<string?>? OperationTextProvider
    {
        get => _selectionOverlay.OperationTextProvider;
        set => _selectionOverlay.OperationTextProvider = value;
    }

    /// <summary>情報ラベル（3 行目）の文言を描画時に評価するプロバイダ（<see cref="FadeMaskDragRectControl.InfoTextProvider"/> へ委譲）。</summary>
    public Func<string?>? InfoTextProvider
    {
        get => _selectionOverlay.InfoTextProvider;
        set => _selectionOverlay.InfoTextProvider = value;
    }

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化を内包オーバーレイへ通知する。</summary>
    /// <remarks><see cref="FadeMaskDragRectControl.RefreshOverlay"/> へ委譲する（暗転マスク・選択枠・ルーペの追従）。</remarks>
    public void NotifyViewportChanged() => _selectionOverlay.RefreshOverlay();

    /// <summary>選択確定要求を <see cref="FadeMaskDragRectControl"/> へ委譲する（Enter キー相当）。</summary>
    public void RequestConfirm() => _selectionOverlay.RequestConfirm();

    /// <summary>選択キャンセル要求を <see cref="FadeMaskDragRectControl"/> へ委譲する（Escape キー相当）。</summary>
    public void RequestCancel() => _selectionOverlay.RequestCancel();

    /// <summary>選択確定要求イベント（<see cref="FadeMaskDragRectControl.ConfirmRequested"/> へ委譲）。</summary>
    public event EventHandler<FadeMaskDragRectSelectionEventArgs>? ConfirmRequested
    {
        add => _selectionOverlay.ConfirmRequested += value;
        remove => _selectionOverlay.ConfirmRequested -= value;
    }

    /// <summary>選択キャンセル要求イベント（<see cref="FadeMaskDragRectControl.CancelRequested"/> へ委譲）。</summary>
    public event EventHandler? CancelRequested
    {
        add => _selectionOverlay.CancelRequested += value;
        remove => _selectionOverlay.CancelRequested -= value;
    }

    // ====================================================================
    // 矩形オーバーレイ（外部提供矩形の描画）
    // ====================================================================

    /// <summary>矩形オーバーレイを再描画する。</summary>
    /// <remarks>
    /// <see cref="Rects"/> の各矩形の左上・右下を <see cref="SourceToClient"/>（画像表示矩形の原点基準）でビューポート座標へ変換し、<br/>
    /// コア（<see cref="OverlayRectLayout.Resolve"/>）が<b>確定した描画入力</b>を図形要素へ写す。<br/>
    /// クランプ・太さ補正・破線の判定はコアが単一所有する（本メソッドは式と判断を持たない）。
    /// </remarks>
    private void UpdateRectOverlay()
    {
        _rectOverlay.Children.Clear();
        if (Source is null || Rects.Count == 0)
        {
            return;
        }

        foreach (var rect in Rects)
        {
            var topLeft = SourceToClient(new Point(rect.Bounds.X, rect.Bounds.Y));
            var bottomRight = SourceToClient(new Point(rect.Bounds.Right, rect.Bounds.Bottom));

            var frame = OverlayRectLayout.Resolve(rect, topLeft.ToEsPoint(), bottomRight.ToEsPoint());
            var rectElement = CreateRectElement(frame);

            Canvas.SetLeft(rectElement, frame.Bounds.X);
            Canvas.SetTop(rectElement, frame.Bounds.Y);
            _rectOverlay.Children.Add(rectElement);
        }
    }

    /// <summary>ビューポート座標を対象座標（元画像ピクセル）へ変換する。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <returns>対象座標。</returns>
    /// <remarks>
    /// 画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="ZoomViewControl.ClientToDisplayedSource"/> に委譲する（座標変換の単一所有）。
    /// </remarks>
    public Point ClientToSource(Point viewportPos)
        => _zoomView.ClientToDisplayedSource(viewportPos);

    /// <summary>対象座標（元画像ピクセル）をビューポート座標へ変換する。</summary>
    /// <param name="contentPos">対象座標。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// 画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="ZoomViewControl.DisplayedSourceToClient"/> に委譲する（座標変換の単一所有）。
    /// </remarks>
    public Point SourceToClient(Point contentPos)
        => _zoomView.DisplayedSourceToClient(contentPos);

    // ====================================================================
    // 矩形オーバーレイ作成（破線スタイル・色・幅）
    // ====================================================================

    /// <summary>コアが確定した描画入力からオーバーレイ矩形の表示要素を作成する。</summary>
    /// <param name="frame">描画入力（コアが確定した配置・太さ・破線の判定と長さ・色）。</param>
    /// <returns>作成した Panel（破線の場合は背景色の下層 + 前景色の上層を重ねた要素）。</returns>
    /// <remarks>
    /// 破線（<see cref="OverlayLineStyle.Dashed"/> / <see cref="OverlayLineStyle.Dotted"/>）の場合は
    /// 前景色と背景色をダッシュごとに交互に塗る（市松風）。 Avalonia の <c>StrokeDashArray</c> は
    /// 単色しか表現できないため、前景色で破線を描き、背景色を下層の別矩形で表現する。<br/>
    /// 幅・高さ・太さ・ダッシュ長・描画可否はコア（<see cref="OverlayRectFrame"/>）が確定した値を使う（本メソッドは式を持たない）。
    /// </remarks>
    private static Panel CreateRectElement(OverlayRectFrame frame)
    {
        var bounds = frame.Bounds;

        // 背景色を下層に、前景色（破線）を上層に重ねる（破線の市松風）。 実線は前景色のみ。
        var backgroundElement = new Rectangle
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Fill = frame.DrawsBackground ? new SolidColorBrush(frame.Background.ToAvaloniaColor()) : null,
        };

        var foregroundElement = new Rectangle
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Stroke = new SolidColorBrush(frame.Foreground.ToAvaloniaColor()),
            StrokeThickness = frame.Thickness,
            StrokeDashArray = frame.IsDashed ? new AvaloniaList<double>([frame.DashLength, frame.GapLength]) : null,
        };

        // 2 要素を重ねて返す（背景 → 前景）。 Canvas の子として両方を追加するため、親要素としてグルーピングする。
        var group = new Panel
        {
            Width = bounds.Width,
            Height = bounds.Height,
        };
        group.Children.Add(backgroundElement);
        group.Children.Add(foregroundElement);
        return group;
    }

    // ====================================================================
    // ルーペへの矩形描画（Rendered イベント経由）
    // ====================================================================

    /// <summary>ルーペの描画完了時に、外部提供矩形を描画済み Bitmap へ描く。</summary>
    /// <param name="sender">イベント送信元（FadeMaskDragRectControl）。</param>
    /// <param name="e">描画済み Bitmap と表示情報。</param>
    /// <remarks>
    /// 外部提供矩形（<see cref="Rects"/>）を <c>(対象座標 − CaptureTopLeft) × Scale</c> で<br/>
    /// ルーペ画像ピクセル座標へ変換し、4 辺を破線（前景色 / 背景色・市松風）で描画する。<br/>
    /// 描画範囲の判定は <see cref="OverlayRectRasterizer"/> が行うため、矩形が空でも分岐を持たない。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (e.CaptureTopLeft is not { } topLeft)
        {
            return;
        }

        foreach (var rect in Rects)
        {
            OverlayRectRasterizer.Draw(e.Bitmap, rect, topLeft.ToEsPoint(), e.Scale);
        }
    }
}

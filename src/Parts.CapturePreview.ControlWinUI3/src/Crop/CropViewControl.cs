using EsUtil.Feature.Loupe;
using EsUtil.Feature.Marquee;
using EsUtil.Ui.WinUI3.Marquee;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView;
using EsUtil.Ui.WinUI3.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop;

/// <summary>画像表示（拡大・パン・Fit）と外部提供矩形のオーバーレイを統合した高レベル複合コントロール（WinUI 3 版）。</summary>
/// <remarks>
/// 他の UI 版と同じ UI 非依存コア（<c>Parts.CapturePreview.Core</c>）を使う。<br/>
/// <b>適用パターン</b>: Facade（ズームビューと矩形オーバーレイを内包し、ホストへ統一インターフェースを提供）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="ZoomViewControl"/>: 画像ビューポート（拡大・パン・Fit）。<br/>
/// - <see cref="RectOverlayCanvas"/>: ホストから渡された <see cref="Rects"/> をビューポート座標へ変換して描画する透明オーバーレイ。<br/>
/// - <see cref="SelectionOverlayControl"/>: 領域選択の複合コントロール（暗転マスク + 選択矩形 + ルーペ）。 切り出し中のみ表示する。<br/>
/// - 強調オーバーレイ: ホストから渡された <see cref="HighlightSourceRect"/>（対象座標）を強調表示する（描画のみ・入力なし）。
/// </remarks>
public sealed partial class CropViewControl : Grid
{
    /// <summary>ホイール 1 ノッチあたりの回転量（WinUI 3 は 120 単位で報告する）。</summary>
    /// <remarks>コアの回転量の単位（1 ノッチ = 1.0）へ換算するために使う。</remarks>
    private const double WheelDeltaPerNotch = 120.0;

    /// <summary>ズーム・パンビューポート（拡大・パン・Fit）。</summary>
    private readonly ZoomViewControl _zoomView = new();

    /// <summary>外部提供矩形のオーバーレイ（透明。 入力は下のズームビューへ通す）。</summary>
    private readonly RectOverlayCanvas _rectOverlay;

    /// <summary>領域選択の複合コントロール（暗転マスク + 選択矩形 + ルーペ）。</summary>
    /// <remarks>
    /// 切り出し中のみ表示する（初期状態は非表示・入力なし）。 表示の切替は <see cref="SetSelectionOverlayVisible"/> が担う。<br/>
    /// 暗転マスク・選択矩形・ルーペの状態は本コントロールが保持する。
    /// </remarks>
    private readonly SelectionOverlayControl _selectionOverlay = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };

    /// <summary>強調オーバーレイ（ホストが与える強調矩形を描く装飾専用の選択矩形コントロール）。</summary>
    /// <remarks>入力は受けず（<c>IsHitTestVisible = false</c>）描画のみを担う（ズーム・ドラッグは下のレイヤが受ける）。</remarks>
    private readonly MarqueeControl _highlight = new() { IsHitTestVisible = false };

    /// <summary>外部提供矩形（対象座標）。</summary>
    private IReadOnlyList<OverlayRect> _rects = [];

    /// <summary>強調する矩形（対象座標。 未設定は null）。</summary>
    private EsRect? _highlightSourceRect;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>CropView コントロールを初期化する。</summary>
    /// <remarks>
    /// ズームビュー・矩形オーバーレイ・領域選択オーバーレイ・強調オーバーレイを重ねて配置し、<br/>
    /// ビューポートの変化をオーバーレイへ中継する。
    /// </remarks>
    public CropViewControl()
    {
        _rectOverlay = new RectOverlayCanvas(_zoomView);
        _highlight.Appearance = HighlightDefaultAppearance;

        // パン・ズーム・リサイズで各オーバーレイを追従させる
        _zoomView.ViewportChanged += OnZoomViewportChanged;

        // 領域選択オーバーレイ上のホイールをビューポートへ転送する
        // （オーバーレイは切り出し中に画像の上を覆って入力を占有するため、ホイールはオーバーレイだけが受け取る）
        _selectionOverlay.PointerWheelChanged += OnSelectionOverlayWheel;

        // 既定は切り出し（プレビュー内の切出し）。 ホストが SelectionMode で変更できる
        _selectionOverlay.Mode = SelectionOverlayMode.Crop;

        // 後から追加した子が上へ描画される（ズームビューの上にオーバーレイを重ねる）
        Children.Add(_zoomView);
        Children.Add(_selectionOverlay);
        Children.Add(_rectOverlay);
        Children.Add(_highlight);
    }

    /// <summary>表示する画像（ズーム・パンの対象）を取得または設定する。</summary>
    /// <value>表示する画像。 null の場合は何も表示しない。 設定すると Fit 表示へリセットする。</value>
    public EsReadOnlyBitmap? Source
    {
        get => _zoomView.Source;
        set
        {
            _zoomView.Source = value;
            // ルーペの拡大元を表示中の画像へ合わせる（画像差し替えに追従する）
            _selectionOverlay.SetLoupeSource(value);
            // 画像の表示矩形が変わるため、強調矩形の写像も更新する
            UpdateHighlight();
        }
    }

    /// <summary>画像の中央寄せ / 左上寄せを取得または設定する。</summary>
    /// <value>true の場合は中央寄せ、false の場合は左上寄せ。</value>
    public bool CenterAlign
    {
        get => _zoomView.CenterAlign;
        set => _zoomView.CenterAlign = value;
    }

    /// <summary>画像の下地（背景色）の外観を取得または設定する（内包するズームビューの外観の写し）。</summary>
    /// <value>下地色（下地・スクロールバーをコアが単一所有する）。</value>
    /// <remarks>外観の既定値・描画可否はコアの <see cref="ZoomViewAppearance"/> が単一所有する。</remarks>
    public ZoomViewAppearance Appearance
    {
        get => _zoomView.Appearance;
        set => _zoomView.Appearance = value;
    }

    /// <summary>外部提供矩形（対象座標）を取得または設定する。</summary>
    /// <value>ビューポートへ描画する矩形。 空の場合は何も描画しない。</value>
    /// <remarks>設定すると矩形オーバーレイを再描画する。</remarks>
    public IReadOnlyList<OverlayRect> Rects
    {
        get => _rects;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _rects = value;
            _rectOverlay.SetRects(value);
        }
    }

    /// <summary>強調の既定の外観（内側の塗りと枠線だけを描く）。</summary>
    /// <value>ラベル・ハンドル・始点マーカー・暗転・外枠を描かない外観（色はホストが与える）。</value>
    /// <remarks>既定値は共有層（<see cref="HighlightAppearanceDefaults.Default"/>）が単一所有する。</remarks>
    public static MarqueeAppearance HighlightDefaultAppearance => HighlightAppearanceDefaults.Default;

    /// <summary>強調する矩形（対象座標）を取得または設定する。</summary>
    /// <value>強調する矩形。 未設定（null）の場合は強調しない。</value>
    /// <remarks>
    /// 本コントロールが対象座標 → ビューポート座標の写像を担う（ホストは座標変換を持たない）。<br/>
    /// 描画は <see cref="HighlightAppearance"/> に従う（既定は内側の塗りと枠線だけ）。<br/>
    /// <b>画像（<see cref="Source"/>）が未設定の間は描画しない</b>（写像の基準が無いため）。
    /// </remarks>
    public EsRect? HighlightSourceRect
    {
        get => _highlightSourceRect;
        set
        {
            _highlightSourceRect = value;
            UpdateHighlight();
        }
    }

    /// <summary>強調の外観（色・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="HighlightDefaultAppearance"/>。</value>
    public MarqueeAppearance HighlightAppearance
    {
        get => _highlight.Appearance;
        set => _highlight.Appearance = value;
    }

    /// <summary>内包するズームビューポートを取得する（座標変換・Fit 状態の利用に使う）。</summary>
    /// <value>ズーム・パンを担うコントロール。</value>
    public ZoomViewControl ZoomView => _zoomView;

    /// <summary>ズーム操作ログの出力先を取得または設定する（内包するズームビューへ伝搬する）。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしとなる（null の吸収はコアが単一所有する）。</value>
    public ILogger Log
    {
        get => _zoomView.Log;
        set => _zoomView.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[CapturePreview]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本アセンブリのログ定義（<see cref="CapturePreviewLogMessage"/>）の接頭辞を設定する。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            CapturePreviewLogMessage.SetPrefix(value);
        }
    }

    /// <summary>現在のズーム率（%）を取得または設定する。</summary>
    /// <value>Fit 中は実効倍率、固定倍率中はズーム率。 0 より大きい値のみ設定できる。</value>
    public double ZoomPercent
    {
        get => _zoomView.ZoomPercent;
        set => _zoomView.ZoomPercent = value;
    }

    /// <summary>ズームイン可能かどうかを取得する。</summary>
    /// <value>ズームイン可能なら true。</value>
    public bool CanZoomIn => _zoomView.CanZoomIn;

    /// <summary>ズームアウト可能かどうかを取得する。</summary>
    /// <value>ズームアウト可能なら true。</value>
    public bool CanZoomOut => _zoomView.CanZoomOut;

    /// <summary>ズーム率が変更されたときに発火する（内包するズームビューから中継する）。</summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged
    {
        add => _zoomView.ZoomChanged += value;
        remove => _zoomView.ZoomChanged -= value;
    }

    /// <summary>ビューポート（パン・ズーム・表示サイズ）が変化したときに発火する（内包するズームビューから中継する）。</summary>
    public event EventHandler? ViewportChanged
    {
        add => _zoomView.ViewportChanged += value;
        remove => _zoomView.ViewportChanged -= value;
    }

    /// <summary>画像の表示矩形（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>画像の表示矩形。 画像未設定時は空の矩形。</returns>
    public EsRect ImageRectViewport()
    {
        try
        {
            return _zoomView.ImageRectViewport();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ImageRectViewport));
            throw;
        }
    }

    /// <summary>ズームインする（段階遷移）。</summary>
    public void ZoomIn()
    {
        try
        {
            _zoomView.ZoomIn();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomIn));
            throw;
        }
    }

    /// <summary>ズームアウトする（段階遷移）。</summary>
    public void ZoomOut()
    {
        try
        {
            _zoomView.ZoomOut();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomOut));
            throw;
        }
    }

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    public void ZoomFit()
    {
        try
        {
            _zoomView.ZoomFit();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomFit));
            throw;
        }
    }

    /// <summary>ビューポート座標を対象座標（元画像ピクセル）へ変換する。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>対象座標（元画像ピクセル）。</returns>
    /// <remarks>画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。</remarks>
    public EsPoint ClientToSource(EsPoint viewportPos)
    {
        try
        {
            return _zoomView.ClientToDisplayedSource(viewportPos);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ClientToSource));
            throw;
        }
    }

    /// <summary>対象座標（元画像ピクセル）をビューポート座標へ変換する。</summary>
    /// <param name="contentPos">対象座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。</remarks>
    public EsPoint SourceToClient(EsPoint contentPos)
    {
        try
        {
            return _zoomView.DisplayedSourceToClient(contentPos);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SourceToClient));
            throw;
        }
    }

    /// <summary>ホイールの回転量でビューポートをパンする（ホストが他のコントロールで受けたホイールの転送用）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。 1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <remarks>回転量から移動量への換算とクランプはズームビューポートが単一所有する。</remarks>
    public void PanByWheel(EsVector delta, bool shift)
    {
        try
        {
            _zoomView.PanByWheel(delta, shift);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(PanByWheel));
            throw;
        }
    }

    /// <summary>Ctrl+ホイールのズームを適用する（ホストが他のコントロールで受けたホイールの転送用）。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="viewportPosition">ズーム中心とする位置（ビューポート座標・DIP）。 null の場合はビューポート中心。</param>
    /// <remarks>ズーム率の遷移と表示の適用はズームビューポートが単一所有する。</remarks>
    public void ZoomByWheel(bool zoomIn, EsPoint? viewportPosition = null)
        => _zoomView.ZoomByWheel(zoomIn, viewportPosition);

    /// <summary>コントロールのローカル座標をこのコントロール自身のローカル座標へ変換する（同一コントロールならそのまま）。</summary>
    /// <param name="element">変換元の要素。</param>
    /// <param name="position">変換元のローカル座標（DIP）。</param>
    /// <returns>このコントロールのローカル座標（DIP）。</returns>
    /// <remarks>ホストが重ねたオーバーレイ上のホイール位置をズーム中心として渡すために使用する。</remarks>
    public EsPoint TranslatePositionFrom(UIElement element, EsPoint position)
        => element.TransformToVisual(this).TransformPoint(position.ToWinUIPoint()).ToEsPoint();

    /// <summary>領域選択オーバーレイの動作モードを取得または設定する。</summary>
    /// <value>動作モード。 既定は <see cref="SelectionOverlayMode.Crop"/>。</value>
    public SelectionOverlayMode SelectionMode
    {
        get => _selectionOverlay.Mode;
        set => _selectionOverlay.Mode = value;
    }

    /// <summary>領域選択オーバーレイの設定（単一値）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定は <see cref="SelectionOverlayOptions.Default"/>。</value>
    public SelectionOverlayOptions SelectionOverlayOptions
    {
        get => _selectionOverlay.Options;
        set => _selectionOverlay.Options = value;
    }

    /// <summary>ビューポートDIP → 対象座標の倍率を取得または設定する。</summary>
    /// <value>倍率（1.0 = 等倍）。</value>
    public double ClientToSourceScaling
    {
        get => _selectionOverlay.ClientToSourceScaling;
        set => _selectionOverlay.ClientToSourceScaling = value;
    }

    /// <summary>ルーペ画像の描画倍率を取得または設定する。</summary>
    /// <value>描画倍率（1.0 = 等倍）。</value>
    public double LoupeClientToPixelScaling
    {
        get => _selectionOverlay.LoupeClientToPixelScaling;
        set => _selectionOverlay.LoupeClientToPixelScaling = value;
    }

    /// <summary>領域選択オーバーレイの選択矩形の外観を取得または設定する。</summary>
    /// <value>外観の単一値（色・枠線の太さ・表示可否）。</value>
    public MarqueeAppearance SelectionOverlayMarqueeAppearance
    {
        get => _selectionOverlay.MarqueeAppearance;
        set => _selectionOverlay.MarqueeAppearance = value;
    }

    /// <summary>領域選択オーバーレイのルーペの外観を取得または設定する。</summary>
    /// <value>外観の単一値（色・枠線の太さ・表示可否）。</value>
    public LoupeAppearance SelectionOverlayLoupeAppearance
    {
        get => _selectionOverlay.LoupeAppearance;
        set => _selectionOverlay.LoupeAppearance = value;
    }

    /// <summary>領域選択オーバーレイ（テスト用アクセサ）。</summary>
    internal SelectionOverlayControl SelectionOverlay => _selectionOverlay;

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択矩形がある場合は true。</value>
    public bool HasSelection => _selectionOverlay.HasSelection;

    /// <summary>選択矩形（対象座標 = 元画像ピクセル）を取得する。</summary>
    /// <value>選択矩形。 未選択の場合は空の矩形。</value>
    public EsRect SelectionSourceRect => _selectionOverlay.SelectionSourceRect;

    /// <summary>選択矩形を設定する（対象座標 = 元画像ピクセル）。</summary>
    /// <param name="rect">設定する矩形（対象座標）。</param>
    public void SetSelection(EsRect rect)
    {
        try
        {
            _selectionOverlay.SetSelection(rect);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetSelection));
            throw;
        }
    }

    /// <summary>選択矩形を消す。</summary>
    public void ClearSelection()
    {
        try
        {
            _selectionOverlay.ClearSelection();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ClearSelection));
            throw;
        }
    }

    /// <summary>領域選択オーバーレイの表示と入力の可否を切り替える。</summary>
    /// <param name="visible">表示する場合は true。</param>
    /// <remarks>
    /// 切り出し中のみ表示する（非表示中は入力も受けない。 暗転マスク・選択枠・ルーペが一括で切り替わる）。<br/>
    /// 非表示へ戻すときは選択を破棄する（暗転マスク・選択枠がプレビューへ残留するのを防ぐ）。<br/>
    /// 切り替えのたびに描画を要求する（非表示の間に描画済みのキャンバスは表示後に再描画されないため）。
    /// </remarks>
    public void SetSelectionOverlayVisible(bool visible)
    {
        // WinUI 3 の要素は可視性を列挙型（Visibility）で表すため、真偽を写す
        _selectionOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _selectionOverlay.IsHitTestVisible = visible;

        if (!visible)
        {
            ClearSelection();
        }

        _selectionOverlay.RequestRedraw();
    }

    /// <summary>領域選択オーバーレイへ座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は恒等変換。</param>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _selectionOverlay.SetCoordTransform(port);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetCoordTransform));
            throw;
        }
    }

    /// <summary>選択矩形コントロールへフォーカスを移す（キー操作の受け口にする）。</summary>
    public void FocusMarquee()
    {
        try
        {
            _selectionOverlay.FocusMarquee();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(FocusMarquee));
            throw;
        }
    }

    /// <summary>ルーペの拡大元画像を設定する。</summary>
    /// <param name="source">拡大元画像。</param>
    public void SetLoupeSource(EsReadOnlyBitmap? source) => _selectionOverlay.SetLoupeSource(source);

    /// <summary>ルーペを非表示にして拡大元画像を解放する。</summary>
    public void ClearLoupe() => _selectionOverlay.ClearLoupe();

    /// <summary>サイズラベルの文言プロバイダを取得または設定する（文言はホストが与える）。</summary>
    /// <value>サイズラベルの文言を返す関数。 null の場合は自動生成を使う。</value>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _selectionOverlay.SizeLabelOverrideProvider;
        set => _selectionOverlay.SizeLabelOverrideProvider = value;
    }

    /// <summary>操作ラベルの文言プロバイダを取得または設定する（文言はホストが与える）。</summary>
    /// <value>操作ラベルの文言を返す関数。</value>
    public Func<string?>? OperationTextProvider
    {
        get => _selectionOverlay.OperationTextProvider;
        set => _selectionOverlay.OperationTextProvider = value;
    }

    /// <summary>情報ラベルの文言プロバイダを取得または設定する（文言はホストが与える）。</summary>
    /// <value>情報ラベルの文言を返す関数。</value>
    public Func<string?>? InfoTextProvider
    {
        get => _selectionOverlay.InfoTextProvider;
        set => _selectionOverlay.InfoTextProvider = value;
    }

    /// <summary>ビューポートの変化を領域選択オーバーレイへ通知する。</summary>
    /// <remarks>暗転マスク・選択枠・ルーペがビューポート変化に追従する。</remarks>
    public void NotifyViewportChanged()
    {
        try
        {
            _selectionOverlay.NotifyViewportChanged();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(NotifyViewportChanged));
            throw;
        }
    }

    /// <summary>選択の確定を要求する（キー操作と同じ経路）。</summary>
    public void RequestConfirm()
    {
        try
        {
            _selectionOverlay.RequestConfirm();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(RequestConfirm));
            throw;
        }
    }

    /// <summary>選択のキャンセルを要求する（キー操作と同じ経路）。</summary>
    public void RequestCancel()
    {
        try
        {
            _selectionOverlay.RequestCancel();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(RequestCancel));
            throw;
        }
    }

    /// <summary>選択の確定が要求されたときに発火する（内包する領域選択オーバーレイから中継する）。</summary>
    public event EventHandler<SelectionConfirmedEventArgs>? ConfirmRequested
    {
        add => _selectionOverlay.ConfirmRequested += value;
        remove => _selectionOverlay.ConfirmRequested -= value;
    }

    /// <summary>選択のキャンセルが要求されたときに発火する（内包する領域選択オーバーレイから中継する）。</summary>
    public event EventHandler? CancelRequested
    {
        add => _selectionOverlay.CancelRequested += value;
        remove => _selectionOverlay.CancelRequested -= value;
    }

    /// <summary>強調オーバーレイ（テスト用アクセサ。 強調矩形の写像と外観の反映を検証する）。</summary>
    internal MarqueeControl HighlightMarquee => _highlight;

    /// <summary>ビューポートの変化を各オーバーレイへ中継する。</summary>
    /// <param name="sender">イベントの送信元（内包するズームビュー）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnZoomViewportChanged(object? sender, EventArgs e)
    {
        _rectOverlay.Refresh();
        _selectionOverlay.NotifyViewportChanged();
        UpdateHighlight();
    }

    /// <summary>領域選択オーバーレイのホイールをビューポート（ズーム・パン）へ転送する。</summary>
    /// <param name="sender">イベントの送信元（領域選択オーバーレイ）。</param>
    /// <param name="e">ポインターの情報（ホイールの回転量を含む）。</param>
    /// <remarks>
    /// 解釈（どちらを選ぶか）はコアの <see cref="ZoomWheelIntent"/>、回転量 → 移動量の換算はビューポートが単一所有する。
    /// </remarks>
    private void OnSelectionOverlayWheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_selectionOverlay);
        var delta = point.Properties.MouseWheelDelta / WheelDeltaPerNotch;
        var modifiers = e.KeyModifiers;

        var intent = ZoomWheelIntent.Resolve(
            (modifiers & VirtualKeyModifiers.Control) != 0,
            (modifiers & VirtualKeyModifiers.Shift) != 0,
            delta);

        var local = point.Position.ToEsPoint();
        if (intent.IsZoom)
        {
            _zoomView.ZoomByWheel(intent.ZoomIn, TranslatePositionFrom(_selectionOverlay, local));
        }
        else if (point.Properties.IsHorizontalMouseWheel)
        {
            _zoomView.PanByWheel(new EsVector(delta, 0), intent.ScrollHorizontal);
        }
        else
        {
            _zoomView.PanByWheel(new EsVector(0, delta), intent.ScrollHorizontal);
        }

        e.Handled = true;
    }

    /// <summary>強調矩形を内包する強調オーバーレイへ反映する。</summary>
    /// <remarks>
    /// 対象座標 → ビューポート座標の写像は <see cref="SourceToClient"/> が単一所有する（Fit の中央寄せ補正を含む）。<br/>
    /// 負サイズのクランプはコア（<see cref="OverlayRectLayout.ViewportRectFromCorners"/>）が単一所有する。<br/>
    /// 写像の結果は強調コントロールの <c>HighlightSourceRect</c>（等価変換）へ渡すため、座標系は一貫する。
    /// </remarks>
    private void UpdateHighlight()
    {
        if (_highlightSourceRect is not { } rect || Source is null)
        {
            _highlight.HighlightSourceRect = null;
            return;
        }

        var topLeft = SourceToClient(new EsPoint(rect.X, rect.Y));
        var bottomRight = SourceToClient(new EsPoint(rect.Right, rect.Bottom));
        _highlight.HighlightSourceRect = OverlayRectLayout.ViewportRectFromCorners(topLeft, bottomRight);
    }
}

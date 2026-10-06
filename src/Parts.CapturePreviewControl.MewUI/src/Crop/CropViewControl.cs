using EsUtil.Platform.Core;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Marquee.MewUI;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.MewUI;
using Microsoft.Extensions.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;

/// <summary>画像表示（拡大・パン・Fit）と外部提供矩形のオーバーレイを統合した高レベル複合コントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>CropViewControl</c> と同じ UI 非依存コア（<c>CropViewControl.Core</c>）を使う。<br/>
/// <b>適用パターン</b>: Facade（ズームビューと矩形オーバーレイを内包し、ホストへ統一インターフェースを提供）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="ZoomViewControl"/>: 画像ビューポート（拡大・パン・Fit）。<br/>
/// - <see cref="RectOverlayCanvas"/>: ホストから渡された <see cref="Rects"/> をビューポート座標へ変換して描画する透明オーバーレイ。<br/>
/// - 強調オーバーレイ: ホストから渡された <see cref="HighlightSourceRect"/>（対象座標）を強調表示する（描画のみ・入力なし）。<br/>
/// <br/>
/// <b>領域選択オーバーレイ</b>: 本コントロールは含まない。 ホストが <c>FadeMaskDragRectControl.MewUI</c> を重ねて構成する<br/>
/// （ドラッグ選択・暗転マスク・ルーペ。 `App.MewUI` の切り出しはこの構成）。
/// </remarks>
public sealed class CropViewControl : Grid
{
    /// <summary>ズーム・パンビューポート（拡大・パン・Fit）。</summary>
    private readonly ZoomViewControl _zoomView = new();

    /// <summary>外部提供矩形のオーバーレイ（透明。 入力は下のズームビューへ通す）。</summary>
    private readonly RectOverlayCanvas _rectOverlay;

    /// <summary>強調オーバーレイ（ホストが与える強調矩形を描く装飾専用の選択矩形コントロール）。</summary>
    /// <remarks>入力は受けず（<c>IsHitTestVisible = false</c>）描画のみを担う（ズーム・ドラッグは下のレイヤが受ける）。</remarks>
    private readonly MarqueeControl _highlight = new() { IsHitTestVisible = false };

    /// <summary>外部提供矩形（対象座標）。</summary>
    private IReadOnlyList<OverlayRect> _rects = [];

    /// <summary>CropView コントロールを初期化する。</summary>
    /// <remarks>
    /// ズームビューと矩形オーバーレイと強調オーバーレイを重ねて配置し、ビューポートの変化をオーバーレイへ中継する。
    /// </remarks>
    public CropViewControl()
    {
        _rectOverlay = new RectOverlayCanvas(_zoomView);
        _highlight.Appearance = HighlightDefaultAppearance;

        // パン・ズーム・リサイズで矩形オーバーレイと強調オーバーレイを追従させる
        _zoomView.ViewportChanged += (_, _) =>
        {
            _rectOverlay.InvalidateVisual();
            UpdateHighlight();
        };

        // 後から追加した子が上へ描画される（ズームビューの上にオーバーレイを重ねる）
        Add(_zoomView);
        Add(_rectOverlay);
        Add(_highlight);
    }

    /// <summary>表示する画像（ズーム・パンの対象）を取得または設定する。</summary>
    /// <value>表示する画像。 null の場合は何も表示しない。 設定すると Fit 表示へリセットする。</value>
    public EsBitmap? Source
    {
        get => _zoomView.Source;
        set
        {
            _zoomView.Source = value;
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

    /// <summary>画像の下地（市松模様・背景色）の外観を取得または設定する（内包するズームビューの外観の写し）。</summary>
    /// <value>市松模様の大きさ・2 色と下地色（下地・市松・スクロールバーをコアが単一所有する）。</value>
    /// <remarks>
    /// 外観の既定値・描画可否（市松なし・背景が透明）はコアの <see cref="ZoomViewAppearance"/> が単一所有する。<br/>
    /// Avalonia 版は市松模様をホストが <c>Brush</c> で供給する（自 UI の描画機構に合わせた非対称）。
    /// </remarks>
    public ZoomViewAppearance Appearance
    {
        get => _zoomView.Appearance;
        set => _zoomView.Appearance = value;
    }

    /// <summary>外部提供矩形（対象座標）を取得または設定する。</summary>
    /// <value>ビューポートとルーペへ描画する矩形。 空の場合は何も描画しない。</value>
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

    // ====================================================================
    // 公開 API: 強調（ハイライト）
    // ====================================================================

    /// <summary>強調の既定の外観（内側の塗りと枠線だけを描く）。</summary>
    /// <value>ラベル・ハンドル・始点マーカー・暗転・外枠を描かない外観（色はホストが与える）。</value>
    /// <remarks>
    /// 強調は「対象矩形を目立たせる」用途であり、選択の対話（ハンドル・始点マーカー）や<br/>
    /// 全面暗転は意味を持たないため、内側の装飾だけを既定とする。<br/>
    /// 色（<see cref="MarqueeAppearance.InnerFillColor"/> / <see cref="MarqueeAppearance.InnerBorderColor"/>）は<br/>
    /// ホストの用途で決まるため、既定は透明のまま（ホストが <see cref="HighlightAppearance"/> で与える）。
    /// </remarks>
    public static readonly MarqueeAppearance HighlightDefaultAppearance = MarqueeAppearance.Default with
    {
        ShowHandles = false,
        ShowMarker = false,
        ShowSizeLabel = false,
        ShowCoordLabel = false,
        ShowOverlay = false,
        ShowOuterBorder = false,
        ShowInnerFill = true,
        ShowInnerBorder = true,
    };

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

    private EsRect? _highlightSourceRect;

    /// <summary>強調の外観（色・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="HighlightDefaultAppearance"/>。</value>
    public MarqueeAppearance HighlightAppearance
    {
        get => _highlight.Appearance;
        set => _highlight.Appearance = value;
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
    /// <value>接頭辞（例: <c>[CapturePreviewControl]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本アセンブリのログ定義（<see cref="CapturePreviewLogMessage"/>）の接頭辞を設定する。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            CapturePreviewLogMessage.Prefix =
                string.IsNullOrWhiteSpace(value) ? CapturePreviewLogMessage.DefaultPrefix : value;
        }
    }

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

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
    public EsPoint TranslatePositionFrom(Element element, EsPoint position)
        => element.TranslatePoint(new Point(position.X, position.Y), this).ToEsPoint();

    /// <summary>強調オーバーレイ（テスト用アクセサ。 強調矩形の写像と外観の反映を検証する）。</summary>
    internal MarqueeControl HighlightMarquee => _highlight;
}

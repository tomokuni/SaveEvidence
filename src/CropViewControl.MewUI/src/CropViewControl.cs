using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.SaveEvidence.CropView;
using EsUtil.SaveEvidence.ZoomView.MewUI;
using EsUtil.SaveEvidence.ZoomView;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.MewUI;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.CropView.MewUI;

/// <summary>画像表示（拡大・パン・Fit）と外部提供矩形のオーバーレイを統合した高レベル複合コントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>CropViewControl</c> と同じ UI 非依存コア（<c>CropViewControl.Core</c>）を使う。<br/>
/// <b>適用パターン</b>: Facade（ズームビューと矩形オーバーレイを内包し、ホストへ統一インターフェースを提供）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="ZoomViewControl"/>: 画像ビューポート（拡大・パン・Fit）。<br/>
/// - <see cref="RectOverlayCanvas"/>: ホストから渡された <see cref="Rects"/> をビューポート座標へ変換して描画する透明オーバーレイ。<br/>
/// <br/>
/// <b>領域選択オーバーレイ</b>: 本コントロールは含まない。ホストが <c>FadeMaskDragRectControl.MewUI</c> を重ねて構成する<br/>
/// （ドラッグ選択・暗転マスク・ルーペ。`app.MewUI` の切り出しはこの構成）。
/// </remarks>
public sealed class CropViewControl : Grid
{
    /// <summary>ズーム・パンビューポート（拡大・パン・Fit）。</summary>
    private readonly ZoomViewControl _zoomView = new();

    /// <summary>外部提供矩形のオーバーレイ（透明。入力は下のズームビューへ通す）。</summary>
    private readonly RectOverlayCanvas _rectOverlay;

    /// <summary>外部提供矩形（ドメイン座標）。</summary>
    private IReadOnlyList<OverlayRect> _rects = [];

    /// <summary>CropView コントロールを初期化する。</summary>
    /// <remarks>
    /// ズームビューと矩形オーバーレイを重ねて配置し、ビューポートの変化をオーバーレイへ中継する。
    /// </remarks>
    public CropViewControl()
    {
        _rectOverlay = new RectOverlayCanvas(_zoomView);

        // パン・ズーム・リサイズで矩形オーバーレイを追従させる
        _zoomView.ViewportChanged += (_, _) => _rectOverlay.InvalidateVisual();

        // 後から追加した子が上へ描画される（ズームビューの上にオーバーレイを重ねる）
        Add(_zoomView);
        Add(_rectOverlay);
    }

    /// <summary>表示する画像（ズーム・パンの対象）を取得または設定する。</summary>
    /// <value>表示する画像。null の場合は何も表示しない。設定すると Fit 表示へリセットする。</value>
    public EsBitmap? Source
    {
        get => _zoomView.Source;
        set => _zoomView.Source = value;
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

    /// <summary>外部提供矩形（ドメイン座標）を取得または設定する。</summary>
    /// <value>ビューポートとルーペへ描画する矩形。空の場合は何も描画しない。</value>
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

    /// <summary>内包するズームビューポートを取得する（座標変換・Fit 状態の利用に使う）。</summary>
    /// <value>ズーム・パンを担うコントロール。</value>
    public ZoomViewControl ZoomView => _zoomView;

    /// <summary>ズーム操作ログの出力先を取得または設定する（内包するズームビューへ伝搬する）。</summary>
    /// <value>ロガー。null を設定した場合は出力なしとなる（null の吸収はコアが単一所有する）。</value>
    public ILogger Log
    {
        get => _zoomView.Log;
        set => _zoomView.Log = value;
    }

    /// <summary>現在のズーム率（%）を取得または設定する。</summary>
    /// <value>Fit 中は実効倍率、固定倍率中はズーム率。0 より大きい値のみ設定できる。</value>
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
    /// <returns>画像の表示矩形。画像未設定時は空の矩形。</returns>
    public EsRect ImageRectViewport() => _zoomView.ImageRectViewport();

    /// <summary>ズームインする（段階遷移）。</summary>
    public void ZoomIn() => _zoomView.ZoomIn();

    /// <summary>ズームアウトする（段階遷移）。</summary>
    public void ZoomOut() => _zoomView.ZoomOut();

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    public void ZoomFit() => _zoomView.ZoomFit();

    /// <summary>ビューポート座標をドメイン座標（元画像ピクセル）へ変換する。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>ドメイン座標（元画像ピクセル）。</returns>
    /// <remarks>画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。</remarks>
    public EsPoint ViewportToContent(EsPoint viewportPos) => _zoomView.ViewportToContentImage(viewportPos);

    /// <summary>ドメイン座標（元画像ピクセル）をビューポート座標へ変換する。</summary>
    /// <param name="contentPos">ドメイン座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。</remarks>
    public EsPoint ContentToViewport(EsPoint contentPos) => _zoomView.ContentToViewportImage(contentPos);

    /// <summary>ホイールの回転量でビューポートをパンする（ホストが他のコントロールで受けたホイールの転送用）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <remarks>回転量から移動量への換算とクランプはズームビューポートが単一所有する。</remarks>
    public void PanByWheel(EsVector delta, bool shift) => _zoomView.PanByWheel(delta, shift);

    /// <summary>Ctrl+ホイールのズームを適用する（ホストが他のコントロールで受けたホイールの転送用）。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="viewportPosition">ズーム中心とする位置（ビューポート座標・DIP）。null の場合はビューポート中心。</param>
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
}

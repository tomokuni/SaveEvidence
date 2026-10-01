using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.SaveEvidence.Parts.ZoomView.AvaloniaUI;

/// <summary>ズーム・パン可能な画像表示コントロール（Avalonia 実装。 自己完結型の複合コントロール）。</summary>
/// <remarks>
/// <b>UI 層の責務は 3 つだけ</b>である（判断・値・計算はコアの <see cref="ZoomViewSession"/> が単一所有する）。<br/>
/// 1. <b>入力ブリッジ</b>: ポインター・ホイールのイベントをコア型へ変換してセッションへ渡す（判定と効果は内部ビューポートが担う）。<br/>
/// 2. <b>描画</b>: 内部ビューポートがセッションの値を <see cref="Image"/> と標準 <c>ScrollBar</c> へ写す。<br/>
/// 3. <b>ホスト結線</b>: 内部ビューポートの生成と公開 API への委譲・通知の転送。<br/>
/// <br/>
/// <b>公開 API</b> はセッションへの委譲であり、内部ビューポート（<see cref="ZoomableView"/>）へホストが到達する必要はない。<br/>
/// ズーム率の遷移・Fit 表示・可否・パンの換算・座標変換はコアが確定する。<br/>
/// 本コントロールは特定のコントロールに依存せず、<see cref="ZoomChanged"/> で実効ズーム率・Fit 状態・可否を通知する<br/>
/// （表示文字列の生成はホストの責務）。
/// </remarks>
public sealed class ZoomViewControl : Panel
{
    /// <summary>パン・ズーム表示を担う内部ビューポート（本コントロールが生成・保持する）。</summary>
    private readonly ZoomableView _zoomableView;

    /// <summary>画像の透明部分の背景（市松模様）を描く内部ビュー（画像より先に描くため最初の子にする）。</summary>
    private readonly CheckerboardView _checkerboard;

    /// <summary>既定の設定でズームビューコントロールを初期化する。</summary>
    /// <remarks>ズーム遷移の設定は <see cref="ZoomConfig.Default"/> を使用する。 カスタマイズは <see cref="ZoomViewControl(ZoomConfig)"/> を使用する。</remarks>
    public ZoomViewControl() : this(ZoomConfig.Default) { }

    /// <summary>指定の設定でズームビューコントロールを初期化する。</summary>
    /// <param name="config">ズーム遷移の設定（ズーム範囲・倍率・レベル集合）。</param>
    /// <remarks>
    /// 内部ビューポートを子として追加し、セッションの通知（ズーム状態・ビューポートの変化）を購読して自 UI の公開イベントへ転送する。<br/>
    /// ホストは内部ビューポートの配線を行う必要がない。
    /// </remarks>
    public ZoomViewControl(ZoomConfig config)
    {
        _zoomableView = new ZoomableView();
        _zoomableView.Session.ApplyConfig(config);
        _checkerboard = new CheckerboardView(_zoomableView.Session);
        Children.Add(_checkerboard);
        Children.Add(_zoomableView);
        _zoomableView.Session.Notified += OnSessionNotified;
    }

    /// <summary>内部ロガー（横断的関心事）を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしとして扱う。</value>
    /// <remarks>null の吸収はコア（<see cref="EsUtil.Platform.Core.LoggerFallback.OrNullLogger"/>）が単一所有する（呼び出し側で null 判定しない）。</remarks>
    public ILogger Log
    {
        get => Session.Log;
        set => Session.Log = value;
    }

    /// <summary>ズーム率が変更されたときに発火する（実効ズーム率・Fit 状態・ズーム可否を通知する）。</summary>
    /// <remarks>
    /// ホスト側は本イベントを購読し、スライダ・⊖⊕ ボタンの Enable/Disable・表示テキスト等の任意の UI へ反映できる<br/>
    /// （本コントロールは特定コントロールに依存しない）。 通知値はコアが確定済みである。
    /// </remarks>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>ビューポート（パン・ズーム・表示状態）が変化したときに発火する（オーバーレイ追従用）。</summary>
    /// <remarks>
    /// パン・ズーム・スクロールバー・画像配置の変更後に発火し、<br/>
    /// 画像プレビュー上のオーバーレイ（Crop の暗転マスク・選択枠・ルーペ等）の追従に使用する。
    /// </remarks>
    public event EventHandler? ViewportChanged;

    /// <summary>表示する画像（ズーム対象）を取得または設定する。</summary>
    /// <value>ズーム対象の画像。 null の場合は何も表示しない。 設定すると Fit 表示へリセットする。</value>
    public IImage? Source
    {
        get => _zoomableView.Source;
        set => _zoomableView.Source = value;
    }

    /// <summary>外観（下地・市松模様・スクロールバーの色）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="ZoomViewAppearance.Default"/>。</value>
    /// <remarks>
    /// <b>市松模様は画像の透明部分の背景</b>であり、描く範囲（画像の表示矩形の内側）はコア（<see cref="ZoomViewAppearance.ComputeCheckerArea"/>）が単一所有する。<br/>
    /// 画像の外（ペーストボード）は下地色（<see cref="ZoomViewAppearance.BackgroundColor"/>）の単色になる。<br/>
    /// 変更すると再描画される（市松模様は本コントロールが描画する）。
    /// </remarks>
    public ZoomViewAppearance Appearance
    {
        get => Session.Appearance;
        set
        {
            _zoomableView.ApplyFeedback(Session.SetAppearance(value));
            _checkerboard.InvalidateVisual();
        }
    }

    /// <summary>画像の中央寄せ / 左上寄せを取得または設定する（画像がビューポートより小さい軸にのみ作用する）。</summary>
    /// <value>true の場合は中央寄せ、false の場合は左上寄せ。</value>
    public bool CenterAlign
    {
        get => Session.CenterAlign;
        set => _zoomableView.ApplyFeedback(Session.SetCenterAlign(value));
    }

    /// <summary>現在の実効ズーム率（%）を取得または設定する。</summary>
    /// <value>Fit 中は実効倍率（ビューポートと元画像サイズから算出）、固定倍率中はズーム率。 0 にはならない。</value>
    /// <remarks>
    /// <b>setter</b> は 0 より大きい値のみ受け付ける（Fit へは <see cref="ZoomFit"/> を使用する）。<br/>
    /// 設定値は <see cref="ZoomConfig"/> の範囲へクランプされ、ズーム中心を維持したまま倍率を変更する。
    /// </remarks>
    public double ZoomPercent
    {
        get => Session.ZoomPercent;
        set => _zoomableView.ApplyFeedback(Session.SetFixedZoom(value));
    }

    /// <summary>現在 Fit 表示中かどうかを取得する。</summary>
    /// <value>Fit 表示中なら true。</value>
    /// <remarks>
    /// 内部ズーム率が 0（Fit）かどうかを意味的に判定する。<br/>
    /// <see cref="ZoomPercent"/> は実効ズーム率のため 0 にならない（Fit 判定は本プロパティを使用する）。
    /// </remarks>
    public bool IsFit => Session.IsFit;

    /// <summary>現在のズーム状態でズームインが可能かどうかを取得する。</summary>
    /// <value>ズームイン可能なら true。 画像未設定時は false。</value>
    /// <remarks>イベント（<see cref="ZoomChanged"/>）を待たずに初期表示時のボタン状態を設定したい場合に参照する。</remarks>
    public bool CanZoomIn => Session.CanZoomIn;

    /// <summary>現在のズーム状態でズームアウトが可能かどうかを取得する。</summary>
    /// <value>ズームアウト可能なら true。 画像未設定時は false。</value>
    /// <remarks>イベント（<see cref="ZoomChanged"/>）を待たずに初期表示時のボタン状態を設定したい場合に参照する。</remarks>
    public bool CanZoomOut => Session.CanZoomOut;

    /// <summary>ズームインする（⊖⊕ ボタン。 設定されたレベル集合の次のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。 Fit は遷移に含めない。</remarks>
    public void ZoomIn() => _zoomableView.ApplyFeedback(Session.ZoomIn());

    /// <summary>ズームアウトする（⊖⊕ ボタン。 設定されたレベル集合の前のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomOut() => _zoomableView.ApplyFeedback(Session.ZoomOut());

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    /// <remarks>Fit へ移行する唯一の経路（<see cref="ZoomPercent"/> の setter は 0 を受け付けない）。</remarks>
    public void ZoomFit() => _zoomableView.ApplyFeedback(Session.ZoomFit());

    /// <summary>ホイールの回転量でパンする（回転量から移動量への換算を単一所有する）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。 1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <remarks>
    /// 本コントロール上でホイールが発生した場合と、ホストが他のコントロール（選択オーバーレイ等）で<br/>
    /// 受けたホイールを転送する場合の双方が本メソッドを使用する（換算則の二重所有を作らない）。
    /// </remarks>
    public void PanByWheel(Vector delta, bool shift)
        => _zoomableView.ApplyFeedback(Session.PanByWheel(new EsVector(delta.X, delta.Y), shift));

    /// <summary>Ctrl+ホイールのズームを適用する（ホイール操作の公開 API）。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="pointerPosition">ズーム中心とする位置（ビューポート座標）。 null の場合はビューポート中心。</param>
    /// <remarks>
    /// 本コントロール上でホイールが発生した場合と、ホストが他のコントロールで受けたホイールを転送する場合の双方が使用する。
    /// </remarks>
    public void ZoomByWheel(bool zoomIn, Point? pointerPosition = null)
        => _zoomableView.ApplyFeedback(Session.ZoomByWheel(zoomIn, pointerPosition?.ToEsPoint()));

    /// <summary>画像の表示矩形（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>画像の表示矩形。 画像未設定時は空。</returns>
    /// <remarks>ホストが重ねるオーバーレイの位置決めに使用する（値はコアが確定する）。</remarks>
    public Rect ImageRectViewport()
    {
        var rect = Session.ImageRect;
        return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public Point ViewportToContentImage(Point viewportPos)
        => Session.ViewportToContentImage(viewportPos.ToEsPoint()).ToAvaloniaPoint();

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public Point ContentToViewportImage(Point contentPos)
        => Session.ContentToViewportImage(contentPos.ToEsPoint()).ToAvaloniaPoint();

    /// <summary>内部ビューポートが所有するセッションを取得する。</summary>
    /// <value>ズームの状態・判断・計算を単一所有するセッション。</value>
    private ZoomViewSession Session => _zoomableView.Session;

    /// <summary>セッションの通知を自 UI の公開イベントへ転送する。</summary>
    /// <param name="sender">送信元（セッション）。</param>
    /// <param name="notice">通知（種別と通知時点の値）。</param>
    /// <remarks>判断を含まない種別 → イベントの対応表とする（通知値はコアが確定済み）。</remarks>
    private void OnSessionNotified(object? sender, ZoomNotice notice)
    {
        switch (notice.Kind)
        {
            case ZoomNoticeKind.ZoomChanged:
                ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(notice.State));
                break;
            case ZoomNoticeKind.ViewportChanged:
                // 市松模様は画像の表示矩形に追従するため、市松ビューも描き直す
                _checkerboard.InvalidateVisual();
                ViewportChanged?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
}

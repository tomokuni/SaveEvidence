using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズームビューの状態（ズーム率・オフセット・設定・表示条件）を保持し、入力の解釈と判断を単一所有する状態管理クラス。</summary>
/// <remarks>
/// UI 実装（Avalonia / MewUI）は「入力イベントをコア型へ変換して本クラスへ渡す」「返された効果を適用する」<br/>
/// 「通知を自 UI の公開イベントへ転送する」「状態を描画する」だけを担い、判断を持たない。<br/>
/// <br/>
/// <b>単一所有する判断</b>:<br/>
/// 1. Fit の表現（内部ズーム率 0 = 自動）と実効ズーム率の導出（Fit 中はクランプした実効倍率）。<br/>
/// 2. Fit 実効倍率の算出・キャッシュと無効化（コンテンツ・ビューポートの変更時のみ）。<br/>
/// 3. ズーム可否（<see cref="CanZoomIn"/> / <see cref="CanZoomOut"/>）と通知値 <see cref="ZoomState"/> の確定。<br/>
/// 4. ズーム遷移の適用（<see cref="ZoomTransition"/> の呼び出しとログ）と、ズーム中心を維持したオフセット再計算。<br/>
/// 5. パンの適用（grab 方式）・スクロール範囲へのクランプ・ホイール回転量からの換算。<br/>
/// 6. 画像変更時の Fit 復帰、ビューポート変更時のキャッシュ無効化。<br/>
/// 7. スクロールバーのドラッグ（サムのヒット判定・トラック押下のサム移動・サム位置からのオフセット逆算）。<br/>
/// <br/>
/// <b>UI に残すもの</b>: 入力イベントの受付（フレームワーク型 → コア型の変換）・効果の適用・描画先への描画・<br/>
/// DPI の取得・ポインタキャプチャ・レイアウト。<br/>
/// <br/>
/// <b>コア型のみに依存</b>: <see cref="EsPoint"/> / <see cref="EsVector"/> / <see cref="EsSize"/> / <see cref="EsRect"/> を使い、<br/>
/// UI フレームワークへ依存しない（UI 初期化なしの <c>[Fact]</c> で並列検証できる）。<br/>
/// <br/>
/// <b>ファイル分割</b>: 本ファイルは状態・読み取り・設定の反映を担い、ズーム遷移は <c>ZoomViewSession.Zoom.cs</c>、<br/>
/// パンと座標変換は <c>ZoomViewSession.Pan.cs</c>、スクロールバーは <c>ZoomViewSession.ScrollBar.cs</c> が担う。
/// </remarks>
/// <remarks>ズームビューの状態管理クラスを初期化する。</remarks>
/// <param name="config">ズーム遷移の設定。 null の場合は <see cref="ZoomOptions.Default"/>。</param>
public sealed partial class ZoomViewSession(ZoomOptions? config = null)
{
    /// <summary>ロガー（null の場合は出力なし）。</summary>
    private ILogger? _log;

    /// <summary>ズーム遷移の設定（不変値）。</summary>
    private ZoomOptions _config = config ?? ZoomOptions.Default;

    /// <summary>ズームビューの外観（下地・市松模様・スクロールバー）。</summary>
    private ZoomViewAppearance _appearance = ZoomViewAppearance.Default;

    /// <summary>内部ズーム率（%）。 0 = Fit、0 より大きい値は固定倍率。</summary>
    private double _zoomPercent;

    /// <summary>パン位置（コンテンツ座標。 ビューポート左上に対応する）。</summary>
    private EsVector _offset;

    /// <summary>画像が設定されているかどうか。</summary>
    private bool _hasSource;

    /// <summary>元画像のサイズ（コンテンツ座標）。</summary>
    private EsSize _sourceSize;

    /// <summary>ビューポートのサイズ（DIP）。</summary>
    private EsSize _viewportSize;

    /// <summary>画像がビューポートより小さい軸を中央寄せするかどうか。</summary>
    private bool _centerAlign = true;

    /// <summary>Fit 実効倍率（%）のキャッシュ。 null = 未計算。</summary>
    private double? _fitZoomCache;

    /// <summary>ズーム可否のキャッシュ。 null = 未計算。</summary>
    private ZoomAvailability? _availability;

    /// <summary>ズーム中心として使用するポインタ位置（ビューポート座標）。 null の場合はビューポート中心。</summary>
    private EsPoint? _pointerPosition;

    /// <summary>ドラッグ開始時に掴んだコンテンツ座標（grab 原点）。</summary>
    private EsPoint _grabSourcePos;

    /// <summary>ドラッグによるパン中かどうか。</summary>
    private bool _isPanning;

    /// <summary>ドラッグ中のスクロールバー軸（null = スクロールバーのドラッグなし）。</summary>
    private bool? _scrollBarDragging;

    /// <summary>スクロールバーのドラッグ開始時のポインタ位置（ビューポート座標）。</summary>
    private EsPoint _scrollBarDragStart;

    /// <summary>スクロールバーのドラッグ開始時のサム位置（トラック内の相対位置・DIP）。</summary>
    private double _scrollBarDragStartThumb;

    /// <summary>ズーム状態（実効ズーム率・Fit 状態・可否）が確定したときに送る通知の購読。</summary>
    private readonly CallbackList<ZoomNotice> _notified = new();

    /// <summary>ズームとビューポートの通知を購読する。</summary>
    /// <param name="notify">通知を受け取る処理。</param>
    /// <returns>購読を解除する <see cref="IDisposable"/>。</returns>
    /// <remarks>
    /// UI 層は自 UI の公開イベントへ転送するだけにする（判断を持たない）。<br/>
    /// <b><c>event</c> を使わない理由</b>: <see cref="ZoomNotice"/> は <c>record</c> であり
    /// <see cref="EventArgs"/> を継承できないため（CA1003 の規則を満たす形で公開する）。
    /// </remarks>
    public IDisposable Subscribe(Action<ZoomNotice> notify) => _notified.Add(notify);

    /// <summary>ロガーを取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしとして扱う。</value>
    /// <remarks>null の吸収（<c>?? NullLogger.Instance</c>）は呼び出し側で行う（規則は .github/ログ規約.md が所有する）。</remarks>
    public ILogger Log
    {
        get => _log ?? NullLogger.Instance;
        set => _log = value;
    }

    /// <summary>ズームビューの外観を取得する。</summary>
    /// <value>現在の外観（下地・市松模様・スクロールバー）。</value>
    public ZoomViewAppearance Appearance => _appearance;

    /// <summary>ビューポートのサイズ（DIP）を取得する。</summary>
    /// <value>ビューポートのサイズ。 未レイアウト時は 0。</value>
    public EsSize ViewportSize => _viewportSize;

    /// <summary>画像がビューポートより小さい軸を中央寄せするかどうかを取得する。</summary>
    /// <value>中央寄せの場合は true（既定）。</value>
    public bool CenterAlign => _centerAlign;

    /// <summary>内部ズーム率（%）を取得する。</summary>
    /// <value>0 = Fit、0 より大きい値は固定倍率。 Fit 判定には <see cref="IsFit"/> を使用する。</value>
    /// <remarks>遷移（<see cref="ZoomTransition"/>）は本値を期待する（実効ズーム率は Fit 中も 0 にならないため）。</remarks>
    public double InternalZoomPercent => _zoomPercent;

    /// <summary>Fit 表示中かどうかを取得する。</summary>
    /// <value>Fit 表示中（内部ズーム率が 0 = 自動）の場合は true。</value>
    public bool IsFit => _zoomPercent <= 0;

    /// <summary>内部ズーム率を倍率へ換算した値を取得する（Fit 中は 0）。</summary>
    /// <value>倍率（1.0 = 100%）。 Fit 中は 0（実効スケールには <see cref="EffectiveScale"/> を使用する）。</value>
    public double Scale => ZoomMath.ScaleFromPercent(_zoomPercent);

    /// <summary>Fit 表示時の実効倍率（%）をクランプなしで取得する。</summary>
    /// <value>実表示倍率（%）。 画像未設定・ビューポート未確定時は <see cref="ZoomMath.FallbackFitZoomPercent"/>。</value>
    /// <remarks>
    /// 現在の表示状態に依存しない（固定倍率表示中でも常に一定の Fit 実効倍率を返す）。<br/>
    /// ホットパス（ズーム可否・実効倍率の参照）で毎回計算しないようキャッシュし、<br/>
    /// コンテンツ（画像）とビューポートの変更時にのみ無効化する。
    /// </remarks>
    public double FitZoomPercent
    {
        get
        {
            if (_fitZoomCache is { } cached)
            {
                return cached;
            }

            var value = _hasSource && _viewportSize.Width > 0 && _viewportSize.Height > 0
                ? ZoomMath.FitZoomPercent(_sourceSize, _viewportSize)
                : ZoomMath.FallbackFitZoomPercent;
            _fitZoomCache = value;
            return value;
        }
    }

    /// <summary>現在の表示に使用する実効スケールを取得する。</summary>
    /// <value>倍率（1.0 = 100%）。 固定倍率中はズーム率 / 100、Fit 中は Fit スケール、画像なしは 1。</value>
    /// <remarks>ゼロ除算を避けるため、座標変換・パン量の計算には必ず本値を使用する（Fit 中は <see cref="Scale"/> が 0 のため）。</remarks>
    public double EffectiveScale => ZoomMath.ComputeEffectiveScale(_zoomPercent, _sourceSize, _viewportSize);

    /// <summary>現在の実効ズーム率（%）を取得する。</summary>
    /// <value>Fit 中はクランプした Fit 実効倍率、固定倍率中はズーム率。 0 にはならない。</value>
    public double ZoomPercent => ComputeEffectiveZoomPercent(_zoomPercent);

    /// <summary>現在のパン位置（コンテンツ座標）を取得する。</summary>
    /// <value>ビューポート左上に対応するコンテンツ座標。</value>
    public EsVector Offset => _offset;

    /// <summary>ズームイン可能かどうかを取得する。</summary>
    /// <value>ズームイン可能な場合は true。 画像未設定時は false。</value>
    public bool CanZoomIn => GetAvailability().CanZoomIn;

    /// <summary>ズームアウト可能かどうかを取得する。</summary>
    /// <value>ズームアウト可能な場合は true。 画像未設定時は false。</value>
    /// <remarks>参照のたびに可否を計算しないよう、状態変化時にキャッシュを無効化して遅延計算する。</remarks>
    public bool CanZoomOut => GetAvailability().CanZoomOut;

    /// <summary>通知に使う現在のズーム状態（実効ズーム率・Fit 状態・可否）を取得する。</summary>
    /// <value>4 値を同時に確定した状態。</value>
    public ZoomState CurrentState => new(ZoomPercent, IsFit, CanZoomIn, CanZoomOut);

    /// <summary>画像の表示サイズ（ビューポート座標・DIP）を取得する。</summary>
    /// <value>元画像サイズに実効スケールを掛けたサイズ。 画像未設定時は 0。</value>
    public EsSize DisplaySize
        => new(_sourceSize.Width * EffectiveScale, _sourceSize.Height * EffectiveScale);

    /// <summary>画像表示矩形の原点（ビューポート座標）を取得する。</summary>
    /// <value>画像の表示矩形の左上座標。 Fit 中央寄せ・パンの補正を含む。</value>
    /// <remarks>画像内座標への変換（<c>ClientToDisplayedSource</c>）の基準点を単一所有する。</remarks>
    public EsPoint ImageOrigin
        => ZoomMath.ComputeImageOrigin(DisplaySize, _viewportSize, _zoomPercent, _offset, Scale, _centerAlign);

    /// <summary>画像の表示矩形（ビューポート座標・DIP）を取得する。</summary>
    /// <value>画像の表示矩形。 画像未設定・ビューポート未確定時は空。</value>
    /// <remarks>オーバーレイ（切り出しの暗転マスク・選択枠・ルーペ等）が本矩形を参照して追従する。</remarks>
    public EsRect ImageRect
    {
        get
        {
            var display = DisplaySize;
            if (display.Width <= 0 || display.Height <= 0)
            {
                return default;
            }

            var origin = ImageOrigin;
            return new EsRect(origin.X, origin.Y, display.Width, display.Height);
        }
    }

    /// <summary>ドラッグによるパン中かどうかを取得する。</summary>
    /// <value>パン中の場合は true。</value>
    public bool IsPanning => _isPanning;

    /// <summary>スクロールバーをドラッグ中かどうかを取得する。</summary>
    /// <value>ドラッグ中の場合は true。</value>
    public bool IsDraggingScrollBar => _scrollBarDragging is not null;

    /// <summary>コンテンツ（画像）の設定を反映する。</summary>
    /// <param name="hasSource">画像が設定されているかどうか。</param>
    /// <param name="sourceSize">元画像のサイズ（コンテンツ座標）。</param>
    /// <returns>UI が適用する効果（再描画）。</returns>
    /// <remarks>画像の差し替えは座標系が変わるため、Fit 表示（ズーム率 0・オフセット 0）へリセットする。</remarks>
    public ZoomFeedback SetSource(bool hasSource, EsSize sourceSize)
    {
        _hasSource = hasSource;
        _sourceSize = sourceSize;
        _zoomPercent = 0;
        _offset = default;
        Invalidate();

        RaiseZoomChanged();
        RaiseViewportChanged();
        return new ZoomFeedback { ShouldRedraw = true };
    }

    /// <summary>ビューポートのサイズを反映する。</summary>
    /// <param name="viewportSize">ビューポートのサイズ（DIP）。</param>
    /// <returns>UI が適用する効果（再描画）。</returns>
    /// <remarks>
    /// Fit 実効倍率はビューポートに依存するため、常にキャッシュを無効化する<br/>
    /// （Fit 中でなくてもキャッシュを破棄する。 固定倍率中にリサイズしても古い Fit 値を使わない）。
    /// </remarks>
    public ZoomFeedback SetViewport(EsSize viewportSize)
    {
        if (_viewportSize == viewportSize)
        {
            return default;
        }

        _viewportSize = viewportSize;
        Invalidate();

        // Fit 中は実効倍率（＝通知する実効ズーム率）が変わるため、ズーム状態も通知する
        if (IsFit)
        {
            RaiseZoomChanged();
        }

        RaiseViewportChanged();
        return new ZoomFeedback { ShouldRedraw = true };
    }

    /// <summary>画像の中央寄せ／左上寄せを反映する。</summary>
    /// <param name="centerAlign">中央寄せする場合は true。</param>
    /// <returns>UI が適用する効果（再描画）。</returns>
    /// <remarks>同じ値の再設定では何もしない（不要な再通知を避ける）。</remarks>
    public ZoomFeedback SetCenterAlign(bool centerAlign)
    {
        if (_centerAlign == centerAlign)
        {
            return default;
        }

        _centerAlign = centerAlign;
        RaiseViewportChanged();
        return new ZoomFeedback { ShouldRedraw = true };
    }

    /// <summary>外観（下地・市松模様・スクロールバー）を反映する。</summary>
    /// <param name="appearance">新しい外観。</param>
    /// <returns>UI が適用する効果（再描画）。</returns>
    /// <remarks>外観は描画のみに作用する（ズーム状態は変化しないため通知しない）。</remarks>
    public ZoomFeedback SetAppearance(ZoomViewAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        if (_appearance == appearance)
        {
            return default;
        }

        _appearance = appearance;
        return new ZoomFeedback { ShouldRedraw = true };
    }

    /// <summary>ズーム遷移の設定を反映する。</summary>
    /// <param name="config">新しい設定（検証済み）。</param>
    /// <returns>UI が適用する効果（なし）。</returns>
    /// <remarks>ズーム範囲が変わるため、可否のキャッシュを破棄して次の参照時に再計算する。</remarks>
    public ZoomFeedback ApplyConfig(ZoomOptions config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _availability = null;
        return default;
    }

    /// <summary>実効ズーム率（%）を導出する（Fit 中はクランプした実効倍率・固定倍率中は内部ズーム率）。</summary>
    /// <param name="internalZoom">内部ズーム率（%）。 0 = Fit。</param>
    /// <returns>実効ズーム率（%）。 0 にはならない（Fit 中も実効倍率を返す）。</returns>
    private double ComputeEffectiveZoomPercent(double internalZoom)
        => internalZoom <= 0 ? _config.ClampZoom(FitZoomPercent) : internalZoom;

    /// <summary>ズーム可否を取得する（未計算なら遅延計算してキャッシュする）。</summary>
    /// <returns>ズーム可否。</returns>
    private ZoomAvailability GetAvailability()
        => _availability ??= BuildAvailability();

    /// <summary>ズーム可否を計算する。</summary>
    /// <returns>ズーム可否。 画像未設定時は何れも不可。</returns>
    private ZoomAvailability BuildAvailability()
    {
        var (canZoomIn, canZoomOut) = ZoomTransition.BuildAvailability(_zoomPercent, FitZoomPercent, _config, _hasSource);
        return new ZoomAvailability(canZoomIn, canZoomOut);
    }

    /// <summary>ビューポートの中心座標を取得する。</summary>
    /// <returns>ビューポート中心（DIP）。</returns>
    private EsPoint ViewportCenter() => new(_viewportSize.Width / 2, _viewportSize.Height / 2);

    /// <summary>コンテンツ・ビューポートに依存するキャッシュを破棄する。</summary>
    private void Invalidate()
    {
        _fitZoomCache = null;
        _availability = null;
    }

    /// <summary>ズーム状態の変化を通知する。</summary>
    private void RaiseZoomChanged()
        => _notified.Raise(new ZoomNotice(ZoomNoticeKind.ZoomChanged, CurrentState, ImageRect));

    /// <summary>ビューポートの変化を通知する。</summary>
    private void RaiseViewportChanged()
        => _notified.Raise(new ZoomNotice(ZoomNoticeKind.ViewportChanged, CurrentState, ImageRect));
}

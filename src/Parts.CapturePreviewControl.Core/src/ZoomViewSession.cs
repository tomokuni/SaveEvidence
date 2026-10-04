using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

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
/// 5. パンの適用（grab 方式）・スクロール範囲へのクランプ・ホイール回転量からの換算（<see cref="ScrollMath.WheelScrollUnit"/>）。<br/>
/// 6. 画像変更時の Fit 復帰、ビューポート変更時のキャッシュ無効化。<br/>
/// 7. スクロールバーのドラッグ（サムのヒット判定・トラック押下のサム移動・サム位置からのオフセット逆算）。<br/>
/// <br/>
/// <b>UI に残すもの</b>: 入力イベントの受付（フレームワーク型 → コア型の変換）・効果の適用・描画先への描画・<br/>
/// DPI の取得・ポインタキャプチャ・レイアウト。<br/>
/// <br/>
/// <b>コア型のみに依存</b>: <see cref="EsPoint"/> / <see cref="EsVector"/> / <see cref="EsSize"/> / <see cref="EsRect"/> を使い、<br/>
/// UI フレームワークへ依存しない（UI 初期化なしの <c>[Fact]</c> で並列検証できる）。
/// </remarks>
/// <remarks>ズームビューの状態管理クラスを初期化する。</remarks>
/// <param name="config">ズーム遷移の設定。 null の場合は <see cref="ZoomOptions.Default"/>。</param>
public sealed class ZoomViewSession(ZoomOptions? config = null)
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

    /// <summary>ズーム状態（実効ズーム率・Fit 状態・可否）が確定したときに送る通知。</summary>
    /// <remarks>UI 層は自 UI の公開イベントへ転送するだけにする（判断を持たない）。</remarks>
    public event EventHandler<ZoomNotice>? Notified;

    /// <summary>ロガーを取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしとして扱う。</value>
    /// <remarks>null の吸収は <see cref="LoggerFallback.OrNullLogger"/> が単一所有する（呼び出し側・UI 層で null 判定しない）。</remarks>
    public ILogger Log
    {
        get => _log.OrNullLogger();
        set => _log = value;
    }

    /// <summary>ズーム遷移の設定を取得する。</summary>
    /// <value>現在の設定（不変値）。</value>
    public ZoomOptions Config => _config;

    /// <summary>ズームビューの外観を取得する。</summary>
    /// <value>現在の外観（下地・市松模様・スクロールバー）。</value>
    public ZoomViewAppearance Appearance => _appearance;

    /// <summary>画像が設定されているかどうかを取得する。</summary>
    /// <value>画像が設定されている場合は true。</value>
    public bool HasSource => _hasSource;

    /// <summary>元画像のサイズ（コンテンツ座標）を取得する。</summary>
    /// <value>元画像のサイズ。 未設定時は 0。</value>
    public EsSize SourceSize => _sourceSize;

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
        return new ZoomFeedback { Redraw = true };
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
        return new ZoomFeedback { Redraw = true };
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
        return new ZoomFeedback { Redraw = true };
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
        return new ZoomFeedback { Redraw = true };
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

    /// <summary>ズーム率を設定する（ズーム中心を維持する）。</summary>
    /// <param name="zoomPercent">新しいズーム率（%）。 0 以下は Fit。</param>
    /// <param name="center">ズーム中心（ビューポート座標）。 null の場合はビューポート中心。</param>
    /// <returns>UI が適用する効果（再描画）。</returns>
    /// <remarks>
    /// ズーム中心のコンテンツ座標を不変に保つようオフセットを再計算する（<see cref="ZoomMath.ComputeOffsetAfterScaleChange"/>）。<br/>
    /// Fit へ移行する場合はオフセットをリセットする。<br/>
    /// Fit 中からの固定倍率への移行はオフセット 0 から開始する（内部スケールが 0 のため中心維持できない）。
    /// </remarks>
    public ZoomFeedback SetZoom(double zoomPercent, EsPoint? center = null)
    {
        var oldScale = Scale;
        _zoomPercent = zoomPercent;

        if (zoomPercent <= 0)
        {
            // Fit: オフセットをリセットする（Fit スケールで描画する。 ズーム中心は不要）
            _offset = default;
        }
        else if (oldScale > 0 && _hasSource)
        {
            var zoomCenter = center ?? ViewportCenter();
            _offset = ZoomMath.ComputeOffsetAfterScaleChange(
                _offset, zoomCenter, oldScale, ZoomMath.ScaleFromPercent(zoomPercent), _sourceSize, _viewportSize);
        }

        // 可否はズーム率に依存するため破棄して再計算する（Fit 実効倍率はコンテンツ・ビューポートにのみ依存するため保持する）
        _availability = null;

        RaiseZoomChanged();
        RaiseViewportChanged();
        return new ZoomFeedback { Redraw = true };
    }

    /// <summary>実効ズーム率（%）を固定倍率として設定する（UI のスライダ・拡大率メニュー用）。</summary>
    /// <param name="zoomPercent">設定するズーム率（%）。</param>
    /// <returns>UI が適用する効果（設定した場合は再描画、0 以下は何もしない）。</returns>
    /// <remarks>
    /// <b>0 以下は無視する</b>（0 以下は Fit の意図と区別できないため受け付けず、Fit へは <see cref="ZoomFit"/> を使用する）。<br/>
    /// 設定値は <see cref="ZoomOptions"/> の範囲へクランプし、ズーム中心を維持したまま倍率を変更する。<br/>
    /// 本規則（0 以下の拒否とクランプ）を両 UI に重複させないための入口である。
    /// </remarks>
    public ZoomFeedback SetFixedZoom(double zoomPercent)
    {
        if (zoomPercent <= 0)
        {
            return default;
        }

        return SetZoom(_config.ClampZoom(zoomPercent), _pointerPosition);
    }

    /// <summary>パン位置を設定する（コンテンツ座標）。</summary>
    /// <param name="offset">パン位置（コンテンツ座標）。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    /// <remarks>スクロール範囲へクランプされる。 クランプ結果が現在値と同じ場合は何もしない（不要な再描画・通知を避ける）。</remarks>
    public ZoomFeedback SetOffset(EsVector offset)
    {
        var clamped = ClampOffsetForCurrentState(offset);
        if (clamped == _offset)
        {
            return default;
        }

        _offset = clamped;
        RaiseViewportChanged();
        return new ZoomFeedback { Redraw = true };
    }

    /// <summary>パンする（コンテンツ座標の移動量）。</summary>
    /// <param name="delta">コンテンツ座標の移動量。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    public ZoomFeedback PanBy(EsVector delta)
        => SetOffset(new EsVector(_offset.X + delta.X, _offset.Y + delta.Y));

    /// <summary>ホイールの回転量でパンする（回転量から移動量への換算を単一所有する）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。 1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    /// <remarks>
    /// 移動量は DIP で計算し、実効倍率で除してコンテンツ座標へ変換する（ズーム中も画面上の移動量が一定になる）。<br/>
    /// 本コントロール上でホイールが発生した場合と、ホストが他のコントロール（選択オーバーレイ等）で受けた<br/>
    /// ホイールを転送する場合の双方が本メソッドを使用する（換算則の二重所有を作らない）。
    /// </remarks>
    public ZoomFeedback PanByWheel(EsVector delta, bool shift)
    {
        var dx = shift ? delta.Y * ScrollMath.WheelScrollUnit : delta.X * ScrollMath.WheelScrollUnit;
        var dy = shift ? 0 : delta.Y * ScrollMath.WheelScrollUnit;
        var scale = EffectiveScale;
        return PanBy(new EsVector(dx / scale, dy / scale));
    }

    /// <summary>ドラッグによるパンを開始する（grab 方式）。</summary>
    /// <param name="position">ポインタ位置（ビューポート座標）。</param>
    /// <returns>UI が適用する効果（ポインタのキャプチャ）。</returns>
    /// <remarks>掴んだ位置のコンテンツ座標を保存し、以降の <see cref="Move"/> でその位置をポインタへ追従させる。</remarks>
    public ZoomFeedback Press(EsPoint position)
    {
        _isPanning = true;
        _grabSourcePos = ClientToSource(position);
        return new ZoomFeedback { CapturePointer = true };
    }

    /// <summary>ドラッグ中は掴んだ位置がポインタに追従するようパンする。</summary>
    /// <param name="position">ポインタ位置（ビューポート座標）。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    /// <remarks>
    /// 描画位置 = スケール × (コンテンツ − オフセット) がポインタ位置と一致するため、<br/>
    /// <c>オフセット = 掴んだコンテンツ座標 − ポインタ位置 / 実効スケール</c> となる（クランプは <see cref="SetOffset"/> が行う）。
    /// </remarks>
    public ZoomFeedback Move(EsPoint position)
    {
        if (!_isPanning)
        {
            return default;
        }

        var scale = EffectiveScale;
        return SetOffset(new EsVector(
            _grabSourcePos.X - position.X / scale,
            _grabSourcePos.Y - position.Y / scale));
    }

    /// <summary>ドラッグによるパンを終了する。</summary>
    /// <returns>UI が適用する効果（ポインタの解放）。</returns>
    public ZoomFeedback Release()
    {
        if (!_isPanning)
        {
            return default;
        }

        _isPanning = false;
        return new ZoomFeedback { ReleasePointer = true };
    }

    /// <summary>ズームインする（⊖⊕ ボタン）。</summary>
    /// <returns>UI が適用する効果（移動先がある場合のみ再描画）。</returns>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。 ログも出力しない。</remarks>
    public ZoomFeedback ZoomIn()
        => ApplyZoomStep((current, fitZoom) => ZoomTransition.NextLevel(current, fitZoom, _config), Log.LogZoomIn);

    /// <summary>ズームアウトする（⊖⊕ ボタン）。</summary>
    /// <returns>UI が適用する効果（移動先がある場合のみ再描画）。</returns>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。 ログも出力しない。</remarks>
    public ZoomFeedback ZoomOut()
        => ApplyZoomStep((current, fitZoom) => ZoomTransition.PrevLevel(current, fitZoom, _config), Log.LogZoomOut);

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    /// <returns>UI が適用する効果（再描画）。</returns>
    /// <remarks>Fit へ移行する唯一の経路（<see cref="ZoomPercent"/> 相当の設定は 0 以下を受け付けない）。</remarks>
    public ZoomFeedback ZoomFit() => SetZoom(0, _pointerPosition);

    /// <summary>Ctrl+ホイールのズームを適用する。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="pointerPosition">ズーム中心とする位置（ビューポート座標）。 null の場合はビューポート中心。</param>
    /// <returns>UI が適用する効果（移動先がある場合のみ再描画）。</returns>
    /// <remarks>
    /// 本コントロール上でホイールが発生した場合と、ホストが他のコントロールで受けたホイールを転送する場合の双方が使用する。<br/>
    /// ズーム中心は呼び出しの間だけ保持する（次回のボタン・スライダ操作はビューポート中心でズームする）。
    /// </remarks>
    public ZoomFeedback ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)
    {
        _pointerPosition = pointerPosition;
        try
        {
            return zoomIn
                ? ApplyZoomStep((current, fitZoom) => ZoomTransition.WheelZoomIn(current, fitZoom, _config), Log.LogZoomInWheel)
                : ApplyZoomStep((current, fitZoom) => ZoomTransition.WheelZoomOut(current, fitZoom, _config), Log.LogZoomOutWheel);
        }
        finally
        {
            _pointerPosition = null;
        }
    }

    /// <summary>現在のスクロールバーの配置を計算する。</summary>
    /// <returns>水平・垂直のスクロールバー。 スクロール不要な軸・非表示の場合は null。</returns>
    /// <remarks>描画とヒット判定が同じ配置を参照するよう、UI 層は本メソッドの結果のみを使う。</remarks>
    public (ZoomScrollBar? Horizontal, ZoomScrollBar? Vertical) CurrentScrollBars()
        => _appearance.ShowScrollBars
            ? ZoomScrollBarGeometry.Compute(
                _sourceSize, _viewportSize, EffectiveScale, _offset,
                _appearance.ScrollBarThickness, _appearance.ScrollBarMargin)
            : (null, null);

    /// <summary>指定軸のスクロールバー同期値（最大オフセット・表示範囲・可否）を計算する。</summary>
    /// <param name="vertical">垂直軸の場合は true（水平軸は false）。</param>
    /// <returns>スクロールバー同期値。 画像未設定・スケール未確定時はスクロール不可。</returns>
    /// <remarks>
    /// 標準のスクロールバー部品を持つ UI が <c>Maximum</c> / <c>ViewportSize</c> / <c>IsVisible</c> へ写すための値である。<br/>
    /// 算出式（<c>ScrollMath</c>）を UI 層へ漏らさないための入口として本メソッドが単一所有する。
    /// </remarks>
    public ZoomScrollBarMetrics ScrollMetrics(bool vertical)
    {
        var scale = EffectiveScale;
        if (!_hasSource || scale <= 0)
        {
            return new ZoomScrollBarMetrics(0, 0, IsScrollable: false);
        }

        var maxOffset = vertical
            ? ZoomScrollBarGeometry.MaxSourceOffset(_sourceSize.Height, _viewportSize.Height, scale)
            : ZoomScrollBarGeometry.MaxSourceOffset(_sourceSize.Width, _viewportSize.Width, scale);
        var viewportSize = ScrollMath.ViewportSize(vertical ? _viewportSize.Height : _viewportSize.Width, scale);
        return new ZoomScrollBarMetrics(maxOffset, viewportSize, IsScrollable: maxOffset > 0);
    }

    /// <summary>指定位置がスクロールバーのトラック上ならドラッグを開始する。</summary>
    /// <param name="position">判定するビューポート座標（DIP）。</param>
    /// <returns>ドラッグを開始した場合は true（UI はポインタをキャプチャする）。</returns>
    /// <remarks>
    /// 垂直・水平の順に判定し、先にヒットした軸のドラッグを開始する。<br/>
    /// トラック上（サム以外）を押した場合はサムをその位置へ移動する（ページジャンプではなく直接移動）。
    /// </remarks>
    public bool TryBeginScrollBarDrag(EsPoint position)
    {
        var (horizontal, vertical) = CurrentScrollBars();

        foreach (var (bar, isVertical) in new[] { (vertical, true), (horizontal, false) })
        {
            if (bar is not { } value || !value.Track.Contains(position))
            {
                continue;
            }

            _scrollBarDragging = isVertical;
            _scrollBarDragStart = position;
            _scrollBarDragStartThumb = isVertical
                ? value.Thumb.Y - value.Track.Y
                : value.Thumb.X - value.Track.X;

            // トラック上（サム以外）を押した場合は、サムをその位置へ移動する
            if (!value.Thumb.Contains(position))
            {
                var delta = isVertical ? position.Y - value.Thumb.Y : position.X - value.Thumb.X;
                _scrollBarDragStartThumb = Math.Clamp(
                    _scrollBarDragStartThumb + delta, 0, ZoomScrollBarGeometry.MovableThumbRange(value, isVertical));
                ApplyScrollBarThumb(_scrollBarDragStartThumb, isVertical);
            }

            return true;
        }

        return false;
    }

    /// <summary>スクロールバーのドラッグを反映する。</summary>
    /// <param name="position">現在のポインタ位置（ビューポート座標）。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    public ZoomFeedback UpdateScrollBarDrag(EsPoint position)
    {
        if (_scrollBarDragging is not { } isVertical)
        {
            return default;
        }

        var (horizontal, vertical) = CurrentScrollBars();
        var bar = isVertical ? vertical : horizontal;
        if (bar is not { } value)
        {
            return default;
        }

        var movable = ZoomScrollBarGeometry.MovableThumbRange(value, isVertical);
        var delta = isVertical ? position.Y - _scrollBarDragStart.Y : position.X - _scrollBarDragStart.X;
        return ApplyScrollBarThumb(Math.Clamp(_scrollBarDragStartThumb + delta, 0, movable), isVertical);
    }

    /// <summary>スクロールバーのドラッグを終了する。</summary>
    /// <returns>UI が適用する効果（ポインタの解放）。</returns>
    public ZoomFeedback ReleaseScrollBarDrag()
    {
        if (_scrollBarDragging is null)
        {
            return default;
        }

        _scrollBarDragging = null;
        return new ZoomFeedback { ReleasePointer = true };
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（オフセット基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>中央寄せの原点補正は含まない（含む場合は <see cref="ClientToDisplayedSource"/> を使用する）。</remarks>
    public EsPoint ClientToSource(EsPoint viewportPos)
        => ZoomMath.ClientToSource(viewportPos, _offset, EffectiveScale);

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。</remarks>
    public EsPoint ClientToDisplayedSource(EsPoint viewportPos)
        => ZoomMath.ClientToDisplayedSource(viewportPos, ImageOrigin, EffectiveScale);

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。</remarks>
    public EsPoint DisplayedSourceToClient(EsPoint contentPos)
        => ZoomMath.DisplayedSourceToClient(contentPos, ImageOrigin, EffectiveScale);

    /// <summary>ビューポートの変化（表示条件の更新）を通知する。</summary>
    /// <returns>UI が適用する効果（再描画）。</returns>
    /// <remarks>UI 固有の理由（レイアウト確定など）でビューポートの再通知が必要な場合に使用する。</remarks>
    public ZoomFeedback NotifyViewportChanged()
    {
        RaiseViewportChanged();
        return new ZoomFeedback { Redraw = true };
    }

    /// <summary>ズーム操作（⊖⊕ ボタン・Ctrl+ホイール）の共通処理（遷移関数の適用・ログ・反映）。</summary>
    /// <param name="transition">現在のズーム率と Fit 実効倍率から遷移先を求める関数。</param>
    /// <param name="log">変更前後のズーム率（%）を記録するログメソッド。</param>
    /// <returns>UI が適用する効果（移動先がある場合のみ再描画）。</returns>
    /// <remarks>
    /// 遷移関数は内部ズーム率（0 = Fit / 0 より大きい値 = 固定倍率）を期待するため、<see cref="InternalZoomPercent"/> を渡す。<br/>
    /// <b>ログの変更前は実効ズーム率</b>（Fit 中は Fit 実効倍率）を記録する。<br/>
    /// 遷移先が null（移動不可）の場合は何もしない。
    /// </remarks>
    private ZoomFeedback ApplyZoomStep(Func<double, double, double?> transition, Action<int, int> log)
    {
        var zoom = _zoomPercent;
        // 遷移関数は Fit 中（current <= 0）のみ fitZoom を使うため、固定倍率中は計算しない
        var fitZoom = zoom <= 0 ? FitZoomPercent : 0;

        if (transition(zoom, fitZoom) is not { } target)
        {
            return default;
        }

        log(ZoomLogFormat.Percent(ZoomPercent), ZoomLogFormat.Percent(target));
        return SetZoom(_config.ClampZoom(target), _pointerPosition);
    }

    /// <summary>サムの位置からスクロールオフセットを算出して適用する。</summary>
    /// <param name="thumbOffset">サムの左上位置（トラック内の相対位置・DIP）。</param>
    /// <param name="isVertical">垂直軸の場合は true。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    private ZoomFeedback ApplyScrollBarThumb(double thumbOffset, bool isVertical)
    {
        var (horizontal, vertical) = CurrentScrollBars();
        var bar = isVertical ? vertical : horizontal;
        if (bar is not { } value)
        {
            return default;
        }

        var maxOffset = isVertical
            ? ZoomScrollBarGeometry.MaxSourceOffset(_sourceSize.Height, _viewportSize.Height, EffectiveScale)
            : ZoomScrollBarGeometry.MaxSourceOffset(_sourceSize.Width, _viewportSize.Width, EffectiveScale);
        var movable = ZoomScrollBarGeometry.MovableThumbRange(value, isVertical);
        var offset = ZoomScrollBarGeometry.SourceOffsetFromThumbInRange(thumbOffset, movable, maxOffset);

        return isVertical
            ? SetOffset(new EsVector(_offset.X, offset))
            : SetOffset(new EsVector(offset, _offset.Y));
    }

    /// <summary>実効ズーム率（%）を導出する（Fit 中はクランプした実効倍率・固定倍率中は内部ズーム率）。</summary>
    /// <param name="internalZoom">内部ズーム率（%）。 0 = Fit。</param>
    /// <returns>実効ズーム率（%）。 0 にはならない（Fit 中も実効倍率を返す）。</returns>
    private double ComputeEffectiveZoomPercent(double internalZoom)
        => internalZoom <= 0 ? _config.ClampZoom(FitZoomPercent) : internalZoom;

    /// <summary>現在の状態に応じてオフセットをスクロール範囲内へクランプする。</summary>
    /// <param name="offset">クランプするオフセット（コンテンツ座標）。</param>
    /// <returns>クランプ後のオフセット。 Fit 中または画像未設定時は原点。</returns>
    /// <remarks>Fit 中（内部スケールが 0）と画像未設定時はパンできないため原点を返す（ゼロ除算も避ける）。</remarks>
    private EsVector ClampOffsetForCurrentState(EsVector offset)
        => !_hasSource || _zoomPercent <= 0
            ? default
            : ZoomMath.ClampOffset(offset, Scale, _sourceSize, _viewportSize);

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
        => Notified?.Invoke(this, new ZoomNotice(ZoomNoticeKind.ZoomChanged, CurrentState, ImageRect));

    /// <summary>ビューポートの変化を通知する。</summary>
    private void RaiseViewportChanged()
        => Notified?.Invoke(this, new ZoomNotice(ZoomNoticeKind.ViewportChanged, CurrentState, ImageRect));
}

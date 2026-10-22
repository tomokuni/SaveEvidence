using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズームビューの状態管理クラス（ズーム遷移）。</summary>
/// <remarks>
/// ズーム率の設定・⊖⊕ の段階遷移・Ctrl+ホイールの遷移・Fit への復帰を担う。<br/>
/// 遷移の規則そのものは <see cref="ZoomTransition"/> が単一所有し、本ファイルは適用（ログ・通知・クランプ）だけを行う。<br/>
/// 状態・読み取りは <c>ZoomViewSession.cs</c>、パンは <c>ZoomViewSession.Pan.cs</c>、<br/>
/// スクロールバーは <c>ZoomViewSession.ScrollBar.cs</c> が担う。
/// </remarks>
public sealed partial class ZoomViewSession
{
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
        return new ZoomFeedback { ShouldRedraw = true };
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

    /// <summary>ズーム操作（⊖⊕ ボタン・Ctrl+ホイール）の共通処理（遷移関数の適用・ログ・反映）。</summary>
    /// <param name="transition">現在のズーム率と Fit 実効倍率から遷移先を求める関数。</param>
    /// <param name="log">変更前後の実効ズーム率（%）を記録するログメソッド（丸めはログ定義が単一所有する）。</param>
    /// <returns>UI が適用する効果（移動先がある場合のみ再描画）。</returns>
    /// <remarks>
    /// 遷移関数は内部ズーム率（0 = Fit / 0 より大きい値 = 固定倍率）を期待するため、<see cref="InternalZoomPercent"/> を渡す。<br/>
    /// <b>ログの変更前は実効ズーム率</b>（Fit 中は Fit 実効倍率）を記録する（丸めはログ定義側が行う）。<br/>
    /// 遷移先が null（移動不可）の場合は何もしない。
    /// </remarks>
    private ZoomFeedback ApplyZoomStep(Func<double, double, double?> transition, Action<double, double> log)
    {
        var zoom = _zoomPercent;
        // 遷移関数は Fit 中（current <= 0）のみ fitZoom を使うため、固定倍率中は計算しない
        var fitZoom = zoom <= 0 ? FitZoomPercent : 0;

        if (transition(zoom, fitZoom) is not { } target)
        {
            return default;
        }

        log(ZoomPercent, target);
        return SetZoom(_config.ClampZoom(target), _pointerPosition);
    }
}

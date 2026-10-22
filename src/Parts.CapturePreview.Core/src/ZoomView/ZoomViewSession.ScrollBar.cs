
namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズームビューの状態管理クラス（スクロールバー）。</summary>
/// <remarks>
/// スクロールバーの配置（トラック・サム）・同期値（標準部品へ写す値）・ドラッグ（サムのヒット判定・<br/>
/// トラック押下のサム移動・サム位置からのオフセット逆算）を担う。<br/>
/// 幾何と逆算そのものは <see cref="ZoomScrollBarGeometry"/> が単一所有する。<br/>
/// 状態・読み取りは <c>ZoomViewSession.cs</c>、ズーム遷移は <c>ZoomViewSession.Zoom.cs</c>、<br/>
/// パンは <c>ZoomViewSession.Pan.cs</c> が担う。
/// </remarks>
public sealed partial class ZoomViewSession
{
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
            ? ScrollMath.MaxOffset(_sourceSize.Height, _viewportSize.Height, scale)
            : ScrollMath.MaxOffset(_sourceSize.Width, _viewportSize.Width, scale);
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
        return new ZoomFeedback { ShouldReleasePointer = true };
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
            ? ScrollMath.MaxOffset(_sourceSize.Height, _viewportSize.Height, EffectiveScale)
            : ScrollMath.MaxOffset(_sourceSize.Width, _viewportSize.Width, EffectiveScale);
        var movable = ZoomScrollBarGeometry.MovableThumbRange(value, isVertical);
        var offset = ZoomScrollBarGeometry.SourceOffsetFromThumbInRange(thumbOffset, movable, maxOffset);

        return isVertical
            ? SetOffset(new EsVector(_offset.X, offset))
            : SetOffset(new EsVector(offset, _offset.Y));
    }
}

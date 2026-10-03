using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>スクロールバー 1 本（トラックとサム）の矩形。</summary>
/// <param name="Track">トラックの矩形（ビューポート座標・DIP）。</param>
/// <param name="Thumb">サム（つまみ）の矩形（ビューポート座標・DIP）。</param>
/// <remarks>トラックはスクロール可能な軸にのみ生成される（画像がビューポートに収まる軸には生成しない）。</remarks>
public readonly record struct ZoomScrollBar(EsRect Track, EsRect Thumb);

/// <summary>ズームビューのオーバーレイスクロールバーの幾何計算を提供する静的クラス。</summary>
/// <remarks>
/// 画像の表示サイズ・ビューポート・オフセットから、トラックとサムの矩形を算出する。<br/>
/// UI フレームワークに依存しないコア型のみで計算するため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// 画像がビューポートに収まる軸にはスクロールバーを生成しない（パン不要のため）。
/// </remarks>
internal static class ZoomScrollBarGeometry
{
    /// <summary>サムの最小長（DIP）。</summary>
    /// <remarks>画像が極端に大きい場合でもサムが操作できる大きさを保つ。</remarks>
    private const double DefaultMinThumbLength = 24.0;

    /// <summary>スクロールバーの配置を計算する。</summary>
    /// <param name="sourceSize">元画像のサイズ（コンテンツ座標）。 0 以下の場合は空を返す。</param>
    /// <param name="viewportSize">ビューポートのサイズ（DIP）。</param>
    /// <param name="scale">コンテンツ座標 → ビューポート DIP の実効スケール（正の値）。</param>
    /// <param name="offset">現在のスクロールオフセット（コンテンツ座標）。</param>
    /// <param name="thickness">スクロールバーの太さ（DIP）。</param>
    /// <param name="margin">ビューポート端からの余白（DIP）。</param>
    /// <param name="minThumbLength">サムの最小長（DIP）。 0 以下の場合は既定値（24）。</param>
    /// <returns>水平・垂直のスクロールバー。 スクロール不要な軸は null。</returns>
    /// <remarks>
    /// 1. 各軸の表示サイズ（<paramref name="sourceSize"/> × <paramref name="scale"/>）がビューポート以下の場合は null を返す。<br/>
    /// 2. トラックはビューポートの右端・下端へ余白を空けて配置する。<br/>
    /// 3. サムの長さは「ビューポート ÷ 表示サイズ」の比率をトラック長へ掛け、最小長で下限を設ける。<br/>
    /// 4. サムの位置はオフセットの進捗率（0〜1）をトラックの可動範囲へ写す。
    /// </remarks>
    public static (ZoomScrollBar? Horizontal, ZoomScrollBar? Vertical) Compute(
        EsSize sourceSize, EsSize viewportSize, double scale, EsVector offset,
        double thickness, double margin, double minThumbLength = 0)
    {
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0 || scale <= 0 || thickness <= 0)
        {
            return (null, null);
        }

        var minThumb = minThumbLength > 0 ? minThumbLength : DefaultMinThumbLength;
        var displayWidth = sourceSize.Width * scale;
        var displayHeight = sourceSize.Height * scale;

        var horizontal = displayWidth > viewportSize.Width
            ? Build(viewportSize.Width, displayWidth, offset.X, MaxSourceOffset(sourceSize.Width, viewportSize.Width, scale), thickness, margin, minThumb, isVertical: false, viewportSize)
            : (ZoomScrollBar?)null;

        var vertical = displayHeight > viewportSize.Height
            ? Build(viewportSize.Height, displayHeight, offset.Y, MaxSourceOffset(sourceSize.Height, viewportSize.Height, scale), thickness, margin, minThumb, isVertical: true, viewportSize)
            : (ZoomScrollBar?)null;

        return (horizontal, vertical);
    }

    /// <summary>指定軸の最大スクロール量（コンテンツ座標）を計算する。</summary>
    /// <param name="contentLength">元画像の当該軸の長さ（コンテンツ座標）。</param>
    /// <param name="viewportLength">ビューポートの当該軸の長さ（DIP）。</param>
    /// <param name="scale">コンテンツ座標 → ビューポート DIP の実効スケール。</param>
    /// <returns>最大スクロール量（コンテンツ座標）。 0 未満にはならない。</returns>
    public static double MaxSourceOffset(double contentLength, double viewportLength, double scale)
        => scale <= 0 ? 0 : Math.Max(0, contentLength - viewportLength / scale);

    /// <summary>サムの位置（トラック内のオフセット）からコンテンツ座標のオフセットを逆算する。</summary>
    /// <param name="thumbOffset">サムの左上位置（トラック内の相対位置・DIP）。</param>
    /// <param name="trackLength">トラックの長さ（DIP）。</param>
    /// <param name="thumbLength">サムの長さ（DIP）。</param>
    /// <param name="maxSourceOffset">最大スクロール量（コンテンツ座標）。</param>
    /// <returns>コンテンツ座標のオフセット。 可動範囲を超える場合は両端へクランプする。</returns>
    /// <remarks>サムのドラッグ中に、ポインタ位置から新しいオフセットを求めるために使用する。</remarks>
    public static double SourceOffsetFromThumb(
        double thumbOffset, double trackLength, double thumbLength, double maxSourceOffset)
        => SourceOffsetFromThumbInRange(thumbOffset, trackLength - thumbLength, maxSourceOffset);

    /// <summary>サムの可動範囲（トラック長 − サム長）を計算する。</summary>
    /// <param name="bar">対象のスクロールバー。</param>
    /// <param name="isVertical">垂直軸の場合は true。</param>
    /// <returns>サムが移動できる長さ（DIP）。 0 未満にはならない。</returns>
    /// <remarks>トラックの可動長の計算を単一所有する（UI 層に <c>track − thumb</c> を書かない）。</remarks>
    public static double MovableThumbRange(ZoomScrollBar bar, bool isVertical)
        => isVertical
            ? Math.Max(0, bar.Track.Height - bar.Thumb.Height)
            : Math.Max(0, bar.Track.Width - bar.Thumb.Width);

    /// <summary>サムの可動範囲内の位置（DIP）からコンテンツ座標のオフセットを逆算する。</summary>
    /// <param name="thumbOffset">サムの左上位置（トラックの可動範囲内の相対位置・DIP）。</param>
    /// <param name="movableRange">サムの可動範囲（<see cref="MovableThumbRange"/> が返す値・DIP）。</param>
    /// <param name="maxSourceOffset">最大スクロール量（コンテンツ座標）。</param>
    /// <returns>コンテンツ座標のオフセット。 可動範囲を超える場合は両端へクランプする。</returns>
    public static double SourceOffsetFromThumbInRange(
        double thumbOffset, double movableRange, double maxSourceOffset)
    {
        if (movableRange <= 0)
        {
            return 0;
        }

        var progress = Math.Clamp(thumbOffset / movableRange, 0, 1);
        return progress * maxSourceOffset;
    }

    /// <summary>1 軸分のスクロールバーを組み立てる。</summary>
    /// <param name="trackLength">トラックの長さ（DIP）。</param>
    /// <param name="displayLength">当該軸の表示サイズ（DIP）。</param>
    /// <param name="offset">現在のオフセット（コンテンツ座標）。</param>
    /// <param name="maxSourceOffset">最大スクロール量（コンテンツ座標）。</param>
    /// <param name="thickness">スクロールバーの太さ（DIP）。</param>
    /// <param name="margin">ビューポート端からの余白（DIP）。</param>
    /// <param name="minThumbLength">サムの最小長（DIP）。</param>
    /// <param name="isVertical">垂直軸の場合は true。</param>
    /// <param name="viewportSize">ビューポートのサイズ（DIP）。</param>
    /// <returns>組み立てたスクロールバー。</returns>
    private static ZoomScrollBar Build(
        double trackLength, double displayLength, double offset, double maxSourceOffset,
        double thickness, double margin, double minThumbLength, bool isVertical, EsSize viewportSize)
    {
        var usableTrack = Math.Max(0, trackLength - margin * 2);
        var ratio = displayLength <= 0 ? 1 : usableTrack * (trackLength / displayLength);
        var thumbLength = Math.Clamp(ratio, Math.Min(minThumbLength, usableTrack), usableTrack);

        var progress = maxSourceOffset <= 0 ? 0 : Math.Clamp(offset / maxSourceOffset, 0, 1);
        var thumbOffset = (usableTrack - thumbLength) * progress;

        EsRect track;
        EsRect thumb;

        if (isVertical)
        {
            var x = viewportSize.Width - margin - thickness;
            track = new EsRect(x, margin, thickness, usableTrack);
            thumb = new EsRect(x, margin + thumbOffset, thickness, thumbLength);
        }
        else
        {
            var y = viewportSize.Height - margin - thickness;
            track = new EsRect(margin, y, usableTrack, thickness);
            thumb = new EsRect(margin + thumbOffset, y, thumbLength, thickness);
        }

        return new ZoomScrollBar(track, thumb);
    }
}

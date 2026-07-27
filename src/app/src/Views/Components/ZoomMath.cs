using Avalonia;

namespace app.Views.Components;

/// <summary>
/// ズーム中心維持の計算（純粋関数）。
/// </summary>
/// <remarks>
/// ズーム中心の画像内の点を求め、ズーム後の画像サイズで再スケーリングして<br/>
/// 新しいスクロールオフセットを計算する。<br/>
/// UI 状態（ViewModel・コントロール）に依存しないため、単体テストで仕様を検証できる。<br/>
/// ZoomManager から呼び出される他、仕様の回帰テストの対象となる。
/// </remarks>
public static class ZoomMath
{
    /// <summary>Fit 表示時の実効倍率（%）をクランプなしで計算する。</summary>
    /// <param name="imageSize">元画像サイズ（ピクセル）。</param>
    /// <param name="viewportSize">ビューポートサイズ（DIP）。</param>
    /// <returns>実効倍率（%）。Uniform（アスペクト比維持）のため、幅・高さのスケールの小さい方（min）を使用する。</returns>
    /// <remarks>現在の表示状態に依存しない（常に一定の Fit 実効倍率を返す）。</remarks>
    public static double FitZoomPercent(Size imageSize, Size viewportSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0) return 100;
        var scaleX = viewportSize.Width / imageSize.Width;
        var scaleY = viewportSize.Height / imageSize.Height;
        return Math.Min(scaleX, scaleY) * 100.0;
    }

    /// <summary>ズーム中心の「画像内の点」を求める（中央寄せ時の画像原点補正込み）。</summary>
    /// <param name="offset">ズーム前のスクロールオフセット。</param>
    /// <param name="center">ズーム中心（ビューポートローカル）。</param>
    /// <param name="viewport">ズーム前のビューポートサイズ。</param>
    /// <param name="imageSize">ズーム前の画像表示サイズ。</param>
    /// <param name="wasCentered">ズーム前の画像配置が中央寄せかどうか。</param>
    /// <returns>ズーム中心に対応する画像内の点。</returns>
    /// <remarks>
    /// 中央寄せの場合は、コンテンツ座標（Offset + Center）と画像内の点が一致しないため、<br/>
    /// 画像のコンテンツ上の原点位置（(ビューポート - 画像サイズ) / 2）を考慮して補正する。
    /// </remarks>
    public static Point ComputeImagePoint(Vector offset, Point center, Size viewport, Size imageSize, bool wasCentered)
    {
        var contentPoint = new Point(offset.X + center.X, offset.Y + center.Y);
        var originX = wasCentered ? (viewport.Width - imageSize.Width) / 2 : 0;
        var originY = wasCentered ? (viewport.Height - imageSize.Height) / 2 : 0;
        return new Point(contentPoint.X - originX, contentPoint.Y - originY);
    }

    /// <summary>ズーム後のスクロールオフセットを求める（画像内の点がズーム中心に来るように）。</summary>
    /// <param name="imagePoint">ズーム中心の画像内の点（<see cref="ComputeImagePoint"/> の結果）。</param>
    /// <param name="prevImageSize">ズーム前の画像表示サイズ。</param>
    /// <param name="newImageSize">ズーム後の画像表示サイズ。</param>
    /// <param name="center">ズーム中心（ビューポートローカル）。</param>
    /// <param name="viewport">ズーム後のビューポートサイズ。</param>
    /// <param name="extent">ズーム後のコンテンツサイズ。</param>
    /// <returns>新しいスクロールオフセット。スクロール範囲（Extent - Viewport）にクランプされる。</returns>
    /// <remarks>
    /// 画像内の点は「ズーム前画像上の相対位置」として正規化し、ズーム後の画像サイズで再スケーリングする。<br/>
    /// ズーム率が変わると画像の表示サイズが変わるため、このスケーリングが必須<br/>
    /// （単純な ContentPoint - Center では prevOffset と同値になり、常に左上基準になる）。
    /// </remarks>
    public static Vector ComputeOffsetAfterZoom(
        Point imagePoint, Size prevImageSize, Size newImageSize,
        Point center, Size viewport, Size extent)
    {
        // ゼロ除算を防ぐ（prevImageSize が 0 の場合はスケールを 1 にする）
        var scaleX = prevImageSize.Width > 0 ? newImageSize.Width / prevImageSize.Width : 1;
        var scaleY = prevImageSize.Height > 0 ? newImageSize.Height / prevImageSize.Height : 1;
        var newOffsetX = Math.Clamp(imagePoint.X * scaleX - center.X, 0, Math.Max(0, extent.Width - viewport.Width));
        var newOffsetY = Math.Clamp(imagePoint.Y * scaleY - center.Y, 0, Math.Max(0, extent.Height - viewport.Height));
        return new Vector(newOffsetX, newOffsetY);
    }

    /// <summary>ズーム率変更後のスクロールオフセットを求める（コンテンツ座標不変・スケールが変わる場合）。</summary>
    /// <param name="offset">ズーム前のオフセット（コンテンツ座標。ビューポート左上に対応）。</param>
    /// <param name="center">ズーム中心（ビューポートローカル）。</param>
    /// <param name="oldScale">ズーム前のスケール（倍率 / 100）。</param>
    /// <param name="newScale">ズーム後のスケール（倍率 / 100）。</param>
    /// <param name="contentSize">コンテンツサイズ（元画像サイズ、コンテンツ座標）。</param>
    /// <param name="viewport">ビューポートサイズ。</param>
    /// <returns>ズーム後のオフセット。スクロール範囲（コンテンツ − ビューポート / スケール）にクランプされる。</returns>
    /// <remarks>
    /// 描画位置 = スケール × (コンテンツ座標 − オフセット) の定義に基づく。<br/>
    /// ズーム中心（ビューポート座標 center）のコンテンツ座標を不変に保つ:<br/>
    /// コンテンツ座標 c = offset + center / oldScale → offset' = c − center / newScale
    /// </remarks>
    public static Vector ComputeOffsetAfterScaleChange(
        Vector offset, Point center, double oldScale, double newScale, Size contentSize, Size viewport)
    {
        // ゼロ除算を防ぐ（スケールが 0 の場合は 1 として扱う）
        oldScale = oldScale > 0 ? oldScale : 1;
        newScale = newScale > 0 ? newScale : 1;

        // ズーム中心のコンテンツ座標 = offset + center / oldScale（不変に保つ）
        var c = new Point(offset.X + center.X / oldScale, offset.Y + center.Y / oldScale);
        var newOffset = new Vector(c.X - center.X / newScale, c.Y - center.Y / newScale);

        // クランプ: offset ∈ [0, content − viewport / scale]
        var maxX = Math.Max(0, contentSize.Width - viewport.Width / newScale);
        var maxY = Math.Max(0, contentSize.Height - viewport.Height / newScale);
        return new Vector(Math.Clamp(newOffset.X, 0, maxX), Math.Clamp(newOffset.Y, 0, maxY));
    }

    /// <summary>オフセットをスクロール範囲（コンテンツ − ビューポート / スケール）内にクランプする。</summary>
    /// <param name="offset">クランプするオフセット（コンテンツ座標）。</param>
    /// <param name="scale">現在のスケール（倍率 / 100）。</param>
    /// <param name="contentSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="viewport">ビューポートサイズ。</param>
    /// <returns>クランプ後のオフセット。</returns>
    public static Vector ClampOffset(Vector offset, double scale, Size contentSize, Size viewport)
    {
        scale = scale > 0 ? scale : 1;
        var maxX = Math.Max(0, contentSize.Width - viewport.Width / scale);
        var maxY = Math.Max(0, contentSize.Height - viewport.Height / scale);
        return new Vector(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（純粋関数）。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <param name="offset">スクロールオフセット（コンテンツ座標）。</param>
    /// <param name="effectiveScale">実効スケール（倍率 / 100。Fit 中は FitScale）。0 以下の場合は 1 として扱う。</param>
    /// <returns>コンテンツ座標。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 変換式: <c>コンテンツ座標 = オフセット + ビューポート座標 / スケール</c>。
    /// </remarks>
    public static Point ViewportToContent(Point viewportPos, Vector offset, double effectiveScale)
    {
        var s = effectiveScale > 0 ? effectiveScale : 1;
        return new Point(offset.X + viewportPos.X / s, offset.Y + viewportPos.Y / s);
    }

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（純粋関数）。</summary>
    /// <param name="contentPos">コンテンツ座標。</param>
    /// <param name="offset">スクロールオフセット（コンテンツ座標）。</param>
    /// <param name="effectiveScale">実効スケール（倍率 / 100。Fit 中は FitScale）。0 以下の場合は 1 として扱う。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 変換式: <c>ビューポート座標 = (コンテンツ座標 − オフセット) × スケール</c>。
    /// </remarks>
    public static Point ContentToViewport(Point contentPos, Vector offset, double effectiveScale)
    {
        var s = effectiveScale > 0 ? effectiveScale : 1;
        return new Point((contentPos.X - offset.X) * s, (contentPos.Y - offset.Y) * s);
    }
}

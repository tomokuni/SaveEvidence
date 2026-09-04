using Avalonia;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>
/// ズーム表示の幾何計算（純粋関数）。
/// </summary>
/// <remarks>
/// Fit 実効倍率・ズーム中心維持（スケール変化時のオフセット）・スクロール範囲クランプ・<br/>
/// ビューポート ↔ コンテンツ座標変換を計算する。<br/>
/// UI 状態（ViewModel・コントロール）に依存しないため、単体テストで仕様を検証できる。<br/>
/// ZoomViewControl / ZoomableView から呼び出される。<br/>
/// スクロール範囲の計算は <see cref="ScrollMetrics"/> へ委譲する。<br/>
/// コントロール内部の計算であり、外部ホストからは直接参照しない。
/// </remarks>
internal static class ZoomMath
{
    /// <summary>Fit 表示時の実効スケール（Uniform）を計算する。</summary>
    /// <param name="imageSize">元画像サイズ（ピクセル）。</param>
    /// <param name="viewportSize">ビューポートサイズ（DIP）。</param>
    /// <returns>実効スケール。アスペクト比維持のため幅・高さのスケールの小さい方（min）を使用する。</returns>
    /// <remarks>
    /// Uniform スケールの単一所有メソッドであり、<see cref="FitZoomPercent"/> と ZoomableView の表示サイズ計算は本メソッドへ委譲する。<br/>
    /// 画像サイズが 0 以下またはスケールが 0 以下の場合は 1 として扱う。
    /// </remarks>
    public static double FitScale(Size imageSize, Size viewportSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0) return 1;
        var scaleX = viewportSize.Width / imageSize.Width;
        var scaleY = viewportSize.Height / imageSize.Height;
        var scale = Math.Min(scaleX, scaleY);
        return scale > 0 ? scale : 1;
    }

    /// <summary>Fit 表示時の実効倍率（%）をクランプなしで計算する。</summary>
    /// <param name="imageSize">元画像サイズ（ピクセル）。</param>
    /// <param name="viewportSize">ビューポートサイズ（DIP）。</param>
    /// <returns>実効倍率（%）。Uniform（アスペクト比維持）のため、幅・高さのスケールの小さい方（min）を使用する。</returns>
    /// <remarks>現在の表示状態に依存しない（常に一定の Fit 実効倍率を返す）。スケール計算は <see cref="FitScale"/> に委譲する。</remarks>
    public static double FitZoomPercent(Size imageSize, Size viewportSize)
        => FitScale(imageSize, viewportSize) * 100.0;

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
        var maxX = ScrollMetrics.MaxOffset(contentSize.Width, viewport.Width, newScale);
        var maxY = ScrollMetrics.MaxOffset(contentSize.Height, viewport.Height, newScale);
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
        var maxX = ScrollMetrics.MaxOffset(contentSize.Width, viewport.Width, scale);
        var maxY = ScrollMetrics.MaxOffset(contentSize.Height, viewport.Height, scale);
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

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準・純粋関数）。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <param name="imageOrigin">画像表示矩形の原点（ビューポート座標）。Fit 中央寄せ等の原点補正を含む。</param>
    /// <param name="effectiveScale">実効スケール（倍率 / 100。Fit 中は FitScale）。0 以下の場合は 1 として扱う。</param>
    /// <returns>コンテンツ座標。</returns>
    /// <remarks>
    /// <see cref="ViewportToContent"/>（オフセット基準）とは異なり、<b>画像表示矩形の原点</b>（<c>imageOrigin</c>）を基準に変換する。<br/>
    /// これにより Fit 中央寄せの原点補正を考慮した、画像内の正確な座標変換ができる。<br/>
    /// 変換式: <c>コンテンツ座標 = (ビューポート座標 − 画像原点) / スケール</c>。
    /// </remarks>
    public static Point ViewportToContentImage(Point viewportPos, Point imageOrigin, double effectiveScale)
    {
        var s = effectiveScale > 0 ? effectiveScale : 1;
        return new Point((viewportPos.X - imageOrigin.X) / s, (viewportPos.Y - imageOrigin.Y) / s);
    }

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準・純粋関数）。</summary>
    /// <param name="contentPos">コンテンツ座標。</param>
    /// <param name="imageOrigin">画像表示矩形の原点（ビューポート座標）。Fit 中央寄せ等の原点補正を含む。</param>
    /// <param name="effectiveScale">実効スケール（倍率 / 100。Fit 中は FitScale）。0 以下の場合は 1 として扱う。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// <see cref="ContentToViewport"/>（オフセット基準）とは異なり、<b>画像表示矩形の原点</b>（<c>imageOrigin</c>）を基準に変換する。<br/>
    /// これにより Fit 中央寄せの原点補正を考慮した、画像内の正確な座標変換ができる。<br/>
    /// 変換式: <c>ビューポート座標 = 画像原点 + コンテンツ座標 × スケール</c>。
    /// </remarks>
    public static Point ContentToViewportImage(Point contentPos, Point imageOrigin, double effectiveScale)
    {
        var s = effectiveScale > 0 ? effectiveScale : 1;
        return new Point(imageOrigin.X + contentPos.X * s, imageOrigin.Y + contentPos.Y * s);
    }
}

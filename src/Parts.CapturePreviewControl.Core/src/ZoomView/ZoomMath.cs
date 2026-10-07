using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>
/// ズーム表示の幾何計算（純粋関数）。
/// </summary>
/// <remarks>
/// Fit 実効倍率・ズーム中心維持（スケール変化時のオフセット）・スクロール範囲クランプ・<br/>
/// ビューポート ↔ コンテンツ座標変換を計算する。<br/>
/// UI 状態（ViewModel・コントロール）に依存しないため、単体テストで仕様を検証できる。<br/>
/// UI 実装（Avalonia / MewUI のズームビュー）から呼び出される。<br/>
/// スクロール範囲の計算は <see cref="ScrollMath"/> へ委譲する。<br/>
/// コントロール内部の計算であり、外部ホストからは直接参照しない。<br/>
/// <b>依存分離</b>: 座標・サイズは UI フレームワーク非依存の <see cref="EsPoint"/> / <see cref="EsVector"/> / <see cref="EsSize"/> で扱い、<br/>
/// UI 境界（各 UI のズームビュー）で UI の型へ変換する（移植性・テスト容易性向上）。
/// </remarks>
internal static class ZoomMath
{
    /// <summary>画像未設定・ビューポート未確定時の Fit 実効倍率（%）のフォールバック。</summary>
    /// <remarks>
    /// Fit 実効倍率が確定できない場合でも表示・通知に使える値が必要なため、本値が単一所有する（従来 100%）。<br/>
    /// UI 層にリテラル 100 を置かない。
    /// </remarks>
    internal const double FallbackFitZoomPercent = 100.0;

    /// <summary>ズーム率（%）を倍率へ換算する。</summary>
    /// <param name="zoomPercent">ズーム率（%）。</param>
    /// <returns>倍率（1.0 = 100%）。</returns>
    /// <remarks>
    /// % と倍率の換算（100 で割る）を単一所有する。 UI 層に <c>/ 100.0</c> を置かない。<br/>
    /// Fit 中（0）は 0 を返す（実効スケールには <see cref="ComputeEffectiveScale"/> を使用する）。
    /// </remarks>
    public static double ScaleFromPercent(double zoomPercent) => zoomPercent / 100.0;

    /// <summary>Fit 表示時の実効スケール（Uniform）を計算する。</summary>
    /// <param name="imageSize">元画像サイズ（ピクセル）。</param>
    /// <param name="viewportSize">ビューポートサイズ（DIP）。</param>
    /// <returns>実効スケール。 アスペクト比維持のため幅・高さのスケールの小さい方（min）を使用する。</returns>
    /// <remarks>
    /// Uniform スケールの単一所有メソッドであり、<see cref="FitZoomPercent"/> と UI 側の表示サイズ計算は本メソッドへ委譲する。<br/>
    /// 画像サイズが 0 以下またはスケールが 0 以下の場合は 1 として扱う。
    /// </remarks>
    public static double FitScale(EsSize imageSize, EsSize viewportSize)
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
    /// <returns>実効倍率（%）。 Uniform（アスペクト比維持）のため、幅・高さのスケールの小さい方（min）を使用する。</returns>
    /// <remarks>現在の表示状態に依存しない（常に一定の Fit 実効倍率を返す）。 スケール計算は <see cref="FitScale"/> に委譲する。</remarks>
    public static double FitZoomPercent(EsSize imageSize, EsSize viewportSize)
        => FitScale(imageSize, viewportSize) * 100.0;

    /// <summary>現在の表示に使用する実効スケールを計算する。</summary>
    /// <param name="zoomPercent">内部ズーム率（%）。 0 = Fit。</param>
    /// <param name="imageSize">元画像サイズ（ピクセル）。</param>
    /// <param name="viewportSize">ビューポートサイズ（DIP）。</param>
    /// <returns>実効スケール。 固定倍率中は <c>zoomPercent / 100</c>、Fit 中は <see cref="FitScale"/>、画像なしは 1。</returns>
    /// <remarks>
    /// UI 側の実効スケール計算を純粋関数として分離する。<br/>
    /// Fit 中（<c>zoomPercent = 0</c>）は <c>zoomPercent / 100</c> が 0 になるため、<br/>
    /// 座標変換・パン量計算では実効スケール（本メソッド）を使用する（ゼロ除算防止）。
    /// </remarks>
    public static double ComputeEffectiveScale(double zoomPercent, EsSize imageSize, EsSize viewportSize)
    {
        if (zoomPercent > 0)
        {
            return zoomPercent / 100.0;
        }

        return imageSize.Width > 0 && imageSize.Height > 0
            ? FitScale(imageSize, viewportSize)
            : 1;
    }

    /// <summary>画像表示の中央寄せ原点補正（ビューポート座標）を軸単位で計算する。</summary>
    /// <param name="displaySize">画像の表示サイズ（ビューポート座標）。</param>
    /// <param name="viewportSize">ビューポートのサイズ（その軸）。</param>
    /// <param name="centerAlign">中央寄せするか（false で左上寄せ）。</param>
    /// <returns>中央寄せ原点。 CenterAlign が false の場合、または表示サイズがビューポートより大きい場合は 0。</returns>
    /// <remarks>
    /// UI 側の画像原点計算における中央寄せ補正を純粋関数として分離する。<br/>
    /// 画像がビューポートより大きい場合はスクロール対象のため補正せず 0 を返す。
    /// </remarks>
    public static double MaxCentered(double displaySize, double viewportSize, bool centerAlign)
    {
        if (!centerAlign || displaySize > viewportSize) return 0;
        return (viewportSize - displaySize) / 2;
    }

    /// <summary>画像原点のビューポート座標（表示矩形の左上位置）を計算する。</summary>
    /// <param name="displaySize">画像の表示サイズ（ビューポート座標）。</param>
    /// <param name="viewportSize">ビューポートのサイズ。</param>
    /// <param name="zoomPercent">内部ズーム率（%）。 0 = Fit。</param>
    /// <param name="offset">スクロールオフセット（コンテンツ座標）。</param>
    /// <param name="scale">現在のスケール（倍率 / 100）。 Fit 中は 0。</param>
    /// <param name="centerAlign">中央寄せするか（false で左上寄せ）。</param>
    /// <returns>画像原点のビューポート座標。</returns>
    /// <remarks>
    /// UI 側の画像原点計算を純粋関数として分離する。<br/>
    /// Fit 中は CenterAlign に応じてビューポート中央（画像が小さい場合）または原点 (0,0)。<br/>
    /// 固定倍率では軸ごとに独立して扱い、スクロール可能な軸はオフセットによるパン位置
    /// （<c>−offset × scale</c>）、スクロール不能な軸は CenterAlign に応じた中央寄せ補正を適用する。
    /// </remarks>
    public static EsPoint ComputeImageOrigin(
        EsSize displaySize, EsSize viewportSize, double zoomPercent,
        EsVector offset, double scale, bool centerAlign)
    {
        if (zoomPercent <= 0)
        {
            // Fit: CenterAlign に応じてビューポート中央（画像が小さい場合）または原点
            return centerAlign
                ? new EsPoint(
                    MaxCentered(displaySize.Width, viewportSize.Width, true),
                    MaxCentered(displaySize.Height, viewportSize.Height, true))
                : new EsPoint(0, 0);
        }

        // 固定倍率では軸ごとに独立して扱う（オフセットは 0 にクランプされ、パン位置と中央寄せは排他）
        var x = displaySize.Width <= viewportSize.Width
            ? MaxCentered(displaySize.Width, viewportSize.Width, centerAlign)
            : -offset.X * scale;
        var y = displaySize.Height <= viewportSize.Height
            ? MaxCentered(displaySize.Height, viewportSize.Height, centerAlign)
            : -offset.Y * scale;
        return new EsPoint(x, y);
    }

    /// <summary>ズーム率変更後のスクロールオフセットを求める（コンテンツ座標不変・スケールが変わる場合）。</summary>
    /// <param name="offset">ズーム前のオフセット（コンテンツ座標。 ビューポート左上に対応）。</param>
    /// <param name="center">ズーム中心（ビューポートローカル）。</param>
    /// <param name="oldScale">ズーム前のスケール（倍率 / 100）。</param>
    /// <param name="newScale">ズーム後のスケール（倍率 / 100）。</param>
    /// <param name="sourceSize">コンテンツサイズ（元画像サイズ、コンテンツ座標）。</param>
    /// <param name="viewport">ビューポートサイズ。</param>
    /// <returns>ズーム後のオフセット。 スクロール範囲（コンテンツ − ビューポート / スケール）にクランプされる。</returns>
    /// <remarks>
    /// 描画位置 = スケール × (コンテンツ座標 − オフセット) の定義に基づく。<br/>
    /// ズーム中心（ビューポート座標 center）のコンテンツ座標を不変に保つ:<br/>
    /// コンテンツ座標 c = offset + center / oldScale → offset' = c − center / newScale
    /// </remarks>
    public static EsVector ComputeOffsetAfterScaleChange(
        EsVector offset, EsPoint center, double oldScale, double newScale, EsSize sourceSize, EsSize viewport)
    {
        // ゼロ除算を防ぐ（スケールが 0 の場合は 1 として扱う）
        oldScale = oldScale > 0 ? oldScale : 1;
        newScale = newScale > 0 ? newScale : 1;

        // ズーム中心のコンテンツ座標 = offset + center / oldScale（不変に保つ）
        var c = new EsPoint(offset.X + center.X / oldScale, offset.Y + center.Y / oldScale);
        var newOffset = new EsVector(c.X - center.X / newScale, c.Y - center.Y / newScale);

        // クランプ: offset ∈ [0, content − viewport / scale]
        var maxX = ScrollMath.MaxOffset(sourceSize.Width, viewport.Width, newScale);
        var maxY = ScrollMath.MaxOffset(sourceSize.Height, viewport.Height, newScale);
        return new EsVector(Math.Clamp(newOffset.X, 0, maxX), Math.Clamp(newOffset.Y, 0, maxY));
    }

    /// <summary>オフセットをスクロール範囲（コンテンツ − ビューポート / スケール）内にクランプする。</summary>
    /// <param name="offset">クランプするオフセット（コンテンツ座標）。</param>
    /// <param name="scale">現在のスケール（倍率 / 100）。</param>
    /// <param name="sourceSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="viewport">ビューポートサイズ。</param>
    /// <returns>クランプ後のオフセット。</returns>
    public static EsVector ClampOffset(EsVector offset, double scale, EsSize sourceSize, EsSize viewport)
    {
        var maxX = ScrollMath.MaxOffset(sourceSize.Width, viewport.Width, scale);
        var maxY = ScrollMath.MaxOffset(sourceSize.Height, viewport.Height, scale);
        return new EsVector(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（純粋関数）。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <param name="offset">スクロールオフセット（コンテンツ座標）。</param>
    /// <param name="effectiveScale">実効スケール（倍率 / 100。 Fit 中は FitScale）。 0 以下の場合は 1 として扱う。</param>
    /// <returns>コンテンツ座標。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 変換式: <c>コンテンツ座標 = オフセット + ビューポート座標 / スケール</c>。
    /// </remarks>
    public static EsPoint ClientToSource(EsPoint viewportPos, EsVector offset, double effectiveScale)
    {
        var s = effectiveScale > 0 ? effectiveScale : 1;
        return new EsPoint(offset.X + viewportPos.X / s, offset.Y + viewportPos.Y / s);
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準・純粋関数）。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <param name="imageOrigin">画像表示矩形の原点（ビューポート座標）。 Fit 中央寄せ等の原点補正を含む。</param>
    /// <param name="effectiveScale">実効スケール（倍率 / 100。 Fit 中は FitScale）。 0 以下の場合は 1 として扱う。</param>
    /// <returns>コンテンツ座標。</returns>
    /// <remarks>
    /// <see cref="ClientToSource"/>（オフセット基準）とは異なり、<b>画像表示矩形の原点</b>（<c>imageOrigin</c>）を基準に変換する。<br/>
    /// これにより Fit 中央寄せの原点補正を考慮した、画像内の正確な座標変換ができる。<br/>
    /// 変換式: <c>コンテンツ座標 = (ビューポート座標 − 画像原点) / スケール</c>。
    /// </remarks>
    public static EsPoint ClientToDisplayedSource(EsPoint viewportPos, EsPoint imageOrigin, double effectiveScale)
    {
        var s = effectiveScale > 0 ? effectiveScale : 1;
        return new EsPoint((viewportPos.X - imageOrigin.X) / s, (viewportPos.Y - imageOrigin.Y) / s);
    }

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準・純粋関数）。</summary>
    /// <param name="contentPos">コンテンツ座標。</param>
    /// <param name="imageOrigin">画像表示矩形の原点（ビューポート座標）。 Fit 中央寄せ等の原点補正を含む。</param>
    /// <param name="effectiveScale">実効スケール（倍率 / 100。 Fit 中は FitScale）。 0 以下の場合は 1 として扱う。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// オフセット基準の変換（<see cref="ClientToSource"/>）とは異なり、<b>画像表示矩形の原点</b>（<c>imageOrigin</c>）を基準に変換する。<br/>
    /// これにより Fit 中央寄せの原点補正を考慮した、画像内の正確な座標変換ができる。<br/>
    /// 変換式: <c>ビューポート座標 = 画像原点 + コンテンツ座標 × スケール</c>。
    /// </remarks>
    public static EsPoint DisplayedSourceToClient(EsPoint contentPos, EsPoint imageOrigin, double effectiveScale)
    {
        var s = effectiveScale > 0 ? effectiveScale : 1;
        return new EsPoint(imageOrigin.X + contentPos.X * s, imageOrigin.Y + contentPos.Y * s);
    }
}

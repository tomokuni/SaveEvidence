namespace Estable.AvaloniaUI.ZoomView;

/// <summary>
/// スクロールバーの範囲・ビューポートサイズの計算（純粋関数）。
/// </summary>
/// <remarks>
/// スクロールバーの幾何計算を UI 状態（ScrollBar コントロール）から分離し、<br/>
/// 単体テストで仕様を検証できるようにする。<br/>
/// 座標系は「コンテンツ座標 = 元画像ピクセル」で、ビューポートサイズはスケールで除してコンテンツ座標に変換する。<br/>
/// コントロール内部のスクロール計算であり、外部ホストからは直接参照しない（テストのみ internal アクセス）。
/// </remarks>
internal static class ScrollMetrics
{
    /// <summary>スクロール範囲の最大オフセット（コンテンツ座標）を計算する。</summary>
    /// <param name="contentSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="viewportSize">ビューポートサイズ（表示座標）。</param>
    /// <param name="scale">スケール（倍率 / 100）。</param>
    /// <returns>最大オフセット。コンテンツがビューポート以下の場合は 0。</returns>
    /// <remarks>
    /// 描画位置 = スケール × (コンテンツ座標 − オフセット) の定義に基づき、<br/>
    /// オフセットの最大値はコンテンツ − ビューポート / スケール。<br/>
    /// スクロール範囲の単一所有メソッドであり、<see cref="ZoomMath"/> のクランプ計算も本メソッドへ委譲する。
    /// </remarks>
    public static double MaxOffset(double contentSize, double viewportSize, double scale)
    {
        scale = scale > 0 ? scale : 1;
        return Math.Max(0, contentSize - viewportSize / scale);
    }

    /// <summary>スクロールバーのビューポートサイズ（コンテンツ座標での表示範囲）を計算する。</summary>
    /// <param name="viewportSize">ビューポートサイズ（表示座標）。</param>
    /// <param name="scale">スケール（倍率 / 100）。</param>
    /// <returns>コンテンツ座標での表示範囲。</returns>
    /// <remarks>サムサイズ = ビューポート / コンテンツ × トラック長 の計算に使用する。</remarks>
    public static double ViewportSize(double viewportSize, double scale)
    {
        scale = scale > 0 ? scale : 1;
        return viewportSize / scale;
    }
}

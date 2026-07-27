namespace app.Controls;

/// <summary>
/// スクロールバーの表示状態・サムサイズ・範囲の計算（純粋関数）。
/// </summary>
/// <remarks>
/// スクロールバーの幾何計算を UI 状態（ScrollBar コントロール）から分離し、<br/>
/// 単体テストで仕様を検証できるようにする。<br/>
/// 座標系は「コンテンツ座標 = 元画像ピクセル」で、ビューポートサイズはスケールで除してコンテンツ座標に変換する。
/// </remarks>
public static class ScrollMetrics
{
    /// <summary>スクロール範囲の最大オフセット（コンテンツ座標）を計算する。</summary>
    /// <param name="contentSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="viewportSize">ビューポートサイズ（表示座標）。</param>
    /// <param name="scale">スケール（倍率 / 100）。</param>
    /// <returns>最大オフセット。コンテンツがビューポート以下の場合は 0。</returns>
    /// <remarks>描画位置 = スケール × (コンテンツ座標 − オフセット) の定義に基づき、<br/>オフセットの最大値はコンテンツ − ビューポート / スケール。</remarks>
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

    /// <summary>スクロールバーを表示すべきかどうかを判定する。</summary>
    /// <param name="contentSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="viewportSize">ビューポートサイズ（表示座標）。</param>
    /// <param name="scale">スケール（倍率 / 100）。</param>
    /// <returns>スクロール可能（コンテンツがビューポートより大きい）なら true。</returns>
    public static bool IsScrollable(double contentSize, double viewportSize, double scale)
        => contentSize > ViewportSize(viewportSize, scale);

    /// <summary>サムのサイズ割合（0〜1）を計算する。</summary>
    /// <param name="contentSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="viewportSize">ビューポートサイズ（表示座標）。</param>
    /// <param name="scale">スケール（倍率 / 100）。</param>
    /// <returns>サムの割合（0 超 1 以下）。コンテンツがビューポート以下の場合は 1。</returns>
    public static double ThumbRatio(double contentSize, double viewportSize, double scale)
    {
        var vs = ViewportSize(viewportSize, scale);
        if (contentSize <= vs) return 1.0;
        return vs / contentSize;
    }
}

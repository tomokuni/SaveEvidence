namespace app.Views.Components;

/// <summary>
/// ズーム遷移の計算ロジック（純粋関数）。
/// </summary>
/// <remarks>
/// ⊖⊕ ボタンの 23 段階遷移と Ctrl+ホイールの ×1.1 遷移を決定する。<br/>
/// UI 状態（ViewModel・コントロール）に依存しないため、単体テストで遷移仕様を検証できる。<br/>
/// ZoomManager から呼び出される他、仕様の回帰テストの対象となる。
/// </remarks>
public static class ZoomTransition
{
    private static readonly int[] s_buttonZoomLevels = [10, 12, 15, 20, 25, 33, 40, 50, 62, 75, 80, 90, 100, 110, 125, 150, 200, 250, 300, 350, 400, 450, 500];

    /// <summary>⊖⊕ボタンのズームレベル（Fit は含まない。仕様に基づく 23 段階・読み取り専用）。</summary>
    public static IReadOnlyList<int> ButtonZoomLevels => s_buttonZoomLevels;

    /// <summary>Ctrl+ホイールのズーム倍率。</summary>
    public const double WheelFactor = 1.1;

    /// <summary>ズーム率の最小値（固定倍率）。</summary>
    public const int MinZoom = 10;

    /// <summary>ズーム率の最大値（固定倍率）。</summary>
    public const int MaxZoom = 500;

    /// <summary>ズームインの次のレベルを返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>次のズームレベル。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率より大きい最初のレベルへ移動する。<br/>
    /// 固定倍率表示中は現在のレベルの次のレベルへ移動する（リストにない値は次に大きいレベル）。<br/>
    /// リスト末尾（500%）では null（移動不可）を返す。
    /// </remarks>
    public static int? NextLevel(int current, double fitZoom)
    {
        if (current <= 0)
        {
            var idx = Array.FindIndex(s_buttonZoomLevels, level => level > fitZoom);
            return idx < 0 ? null : s_buttonZoomLevels[idx];
        }

        var i = Array.IndexOf(s_buttonZoomLevels, current);
        if (i >= 0)
        {
            if (i >= s_buttonZoomLevels.Length - 1) return null; // リスト末尾（500%）で停止
            return s_buttonZoomLevels[i + 1];
        }

        var next = Array.FindIndex(s_buttonZoomLevels, level => level > current);
        return next < 0 ? null : s_buttonZoomLevels[next];
    }

    /// <summary>ズームアウトの前のレベルを返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>前のズームレベル。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率より小さい最後のレベルへ移動する。<br/>
    /// 固定倍率表示中は現在のレベルの前のレベルへ移動する（リストにない値は次に小さいレベル）。<br/>
    /// リスト先頭（10%）では null（移動不可）を返す。
    /// </remarks>
    public static int? PrevLevel(int current, double fitZoom)
    {
        if (current <= 0)
        {
            var idx = Array.FindLastIndex(s_buttonZoomLevels, level => level < fitZoom);
            return idx < 0 ? null : s_buttonZoomLevels[idx];
        }

        var i = Array.IndexOf(s_buttonZoomLevels, current);
        if (i > 0) return s_buttonZoomLevels[i - 1];
        if (i < 0)
        {
            var prev = Array.FindLastIndex(s_buttonZoomLevels, level => level < current);
            return prev < 0 ? null : s_buttonZoomLevels[prev];
        }

        return null; // i == 0（リスト先頭 10%）で停止
    }

    /// <summary>Ctrl+ホイールのズームイン倍率を返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームイン後の倍率（Round(現在 × 1.1)、10〜500 にクランプ）。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率 × 1.1 へ移動する（実効倍率 ≥ 500 なら null = 移動不可）。<br/>
    /// Fit は遷移に含めない。
    /// </remarks>
    public static int? WheelZoomIn(int current, double fitZoom)
    {
        if (current <= 0)
        {
            if (fitZoom >= MaxZoom) return null;
            return (int)Math.Clamp(Math.Round(fitZoom * WheelFactor), MinZoom, MaxZoom);
        }
        return (int)Math.Clamp(Math.Round(current * WheelFactor), MinZoom, MaxZoom);
    }

    /// <summary>Ctrl+ホイールのズームアウト倍率を返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームアウト後の倍率（Round(現在 ÷ 1.1)、10〜500 にクランプ）。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率 ÷ 1.1 へ移動する（実効倍率 ≤ 10 なら null = 移動不可）。<br/>
    /// Fit は遷移に含めない。
    /// </remarks>
    public static int? WheelZoomOut(int current, double fitZoom)
    {
        if (current <= 0)
        {
            if (fitZoom <= MinZoom) return null;
            return (int)Math.Clamp(Math.Round(fitZoom / WheelFactor), MinZoom, MaxZoom);
        }
        return (int)Math.Clamp(Math.Round(current / WheelFactor), MinZoom, MaxZoom);
    }

    /// <summary>ズームイン可能かどうかを返す。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームイン可能なら true。</returns>
    public static bool CanZoomIn(int current, double fitZoom) => NextLevel(current, fitZoom) is not null;

    /// <summary>ズームアウト可能かどうかを返す。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームアウト可能なら true。</returns>
    public static bool CanZoomOut(int current, double fitZoom) => PrevLevel(current, fitZoom) is not null;
}

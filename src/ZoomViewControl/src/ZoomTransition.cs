namespace Estable.AvaloniaUI.ZoomView;

/// <summary>
/// ズーム遷移の計算ロジック（純粋関数）。
/// </summary>
/// <remarks>
/// ⊖⊕ ボタンの 23 段階遷移と Ctrl+ホイールの ×1.1 遷移を決定する。<br/>
/// UI 状態（ViewModel・コントロール）に依存しないため、単体テストで遷移仕様を検証できる。<br/>
/// ZoomViewControl から呼び出される他、仕様の回帰テストの対象となる。
/// </remarks>
public static class ZoomTransition
{
    private static readonly double[] s_buttonZoomLevels = [10, 12, 15, 20, 25, 33, 40, 50, 62, 75, 80, 90, 100, 110, 125, 150, 200, 250, 300, 350, 400, 450, 500];

    /// <summary>⊖⊕ボタンのズームレベル（Fit は含まない。仕様に基づく 23 段階・読み取り専用。整数値の double）。</summary>
    public static IReadOnlyList<double> ButtonZoomLevels => s_buttonZoomLevels;

    /// <summary>Ctrl+ホイールのズーム倍率。</summary>
    public const double WheelFactor = 1.1;

    /// <summary>ズーム率の最小値（固定倍率）。</summary>
    public const double MinZoom = 10;

    /// <summary>ズーム率の最大値（固定倍率）。</summary>
    public const double MaxZoom = 500;

    /// <summary>ズームインの次のレベルを返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>次のズームレベル。移動先がなければ null。</returns>
    /// <remarks>
    /// <b>固定倍率表示中</b>: 現在の値より大きい最初のレベルへ移動する（リスト内は隣、リスト外は次に大きい）。<br/>
    /// <b>Fit 実効倍率を挟む</b>: 次に大きいレベル <c>next</c> と Fit 実効倍率 <c>fitZoom</c> が
    /// <c>current &lt; fitZoom &lt; next</c> を満たす場合、<c>fitZoom</c> を次の倍率とする<br/>
    /// （ズームインで 23 段階のレベルを通過する際、Fit 実効倍率が間に挟まる場合はそれを経由する）。<br/>
    /// <b>Fit 表示中</b>: 実効倍率より大きい最初のレベルへ移動する（実効倍率は <see cref="MinZoom"/>〜<see cref="MaxZoom"/> にクランプ）。<br/>
    /// リスト末尾（500%）で null（移動不可）を返す。Fit 実効倍率がレベルの近傍（<c>fitZoom &lt;= current</c> または <c>fitZoom &gt;= next</c>）の場合は 23 段階レベルを優先する。
    /// </remarks>
    public static double? NextLevel(double current, double fitZoom)
    {
        // 23 段階は整数レベルのため、比較・インデックス探索は整数値として扱う
        var baseValue = current <= 0 ? (int)Math.Floor(fitZoom) : (int)Math.Round(current);
        var idx = Array.BinarySearch(s_buttonZoomLevels, baseValue);
        var nextIdx = idx >= 0 ? idx + 1 : ~idx;
        if (nextIdx >= s_buttonZoomLevels.Length) return null;
        var next = s_buttonZoomLevels[nextIdx];

        // Fit 実効倍率は下限・上限にクランプする（Fit 表示時でも MinZoom〜MaxZoom の範囲内）
        fitZoom = Math.Clamp(fitZoom, MinZoom, MaxZoom);

        // 固定倍率（current > 0）で、Fit 実効倍率が「現在値」と「次に大きいレベル」の間に挟まる場合、
        // fitZoom を次の倍率とする（ズームインで Fit 実効倍率を経由）。近接（fitZoom <= current または fitZoom >= next）の場合は 23 段階レベルを優先。
        if (current > 0 && current < fitZoom && fitZoom < next)
        {
            return fitZoom;
        }
        return next;
    }

    /// <summary>ズームアウトの前のレベルを返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のとき、または固定倍率で Fit 実効倍率を挟むときに使用。</param>
    /// <returns>前のズームレベル。移動先がなければ null。</returns>
    /// <remarks>
    /// <b>固定倍率表示中</b>: 現在の値より小さい最後のレベルへ移動する（リスト内は隣、リスト外は次に小さい）。<br/>
    /// <b>Fit 実効倍率を挟む</b>: 次に小さいレベル <c>prev</c> と Fit 実効倍率 <c>fitZoom</c> が
    /// <c>prev &lt; fitZoom &lt; current</c> を満たす場合、<c>fitZoom</c> を次の倍率とする<br/>
    /// （ズームアウトで 23 段階のレベルを通過する際、Fit 実効倍率が間に挟まる場合はそれを経由する）。<br/>
    /// <b>Fit 表示中</b>: 実効倍率より小さい最後のレベルへ移動する。<br/>
    /// リスト先頭（10%）で null（移動不可）を返す。Fit 実効倍率がレベルの近傍（<c>prev</c> 以下）の場合は 23 段階レベルを優先する。
    /// </remarks>
    public static double? PrevLevel(double current, double fitZoom)
    {
        // 23 段階は整数レベルのため、比較・インデックス探索は整数値として扱う
        var baseValue = current <= 0 ? (int)Math.Ceiling(fitZoom) : (int)Math.Round(current);
        var idx = Array.BinarySearch(s_buttonZoomLevels, baseValue);
        var prevIdx = idx > 0 ? idx - 1 : ~idx - 1;
        if (prevIdx < 0) return null;
        var prev = s_buttonZoomLevels[prevIdx];

        // Fit 実効倍率は下限・上限にクランプする（Fit 表示時でも MinZoom〜MaxZoom の範囲内）
        fitZoom = Math.Clamp(fitZoom, MinZoom, MaxZoom);

        // 固定倍率（current > 0）で、Fit 実効倍率が「次に小さいレベル」と「現在値」の間に挟まる場合、
        // fitZoom を次の倍率とする（ズームアウトで Fit 実効倍率を経由）。近接（prev <= fitZoom）の場合は 23 段階レベルを優先。
        if (current > 0 && prev < fitZoom && fitZoom < current)
        {
            return fitZoom;
        }
        return prev;
    }

    /// <summary>Ctrl+ホイールのズームイン倍率を返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームイン後の倍率（Round(現在 × 1.1)、10〜500 にクランプ）。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率 × 1.1 へ移動する（実効倍率 ≥ 500 なら null = 移動不可）。<br/>
    /// Fit は遷移に含めない。
    /// </remarks>
    public static double? WheelZoomIn(double current, double fitZoom)
    {
        if (current <= 0)
        {
            if (fitZoom >= MaxZoom) return null;
            return Math.Clamp(Math.Round(fitZoom * WheelFactor), MinZoom, MaxZoom);
        }
        return Math.Clamp(Math.Round(current * WheelFactor), MinZoom, MaxZoom);
    }

    /// <summary>Ctrl+ホイールのズームアウト倍率を返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームアウト後の倍率（Round(現在 ÷ 1.1)、10〜500 にクランプ）。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率 ÷ 1.1 へ移動する（実効倍率 ≤ 10 なら null = 移動不可）。<br/>
    /// Fit は遷移に含めない。
    /// </remarks>
    public static double? WheelZoomOut(double current, double fitZoom)
    {
        if (current <= 0)
        {
            if (fitZoom <= MinZoom) return null;
            return Math.Clamp(Math.Round(fitZoom / WheelFactor), MinZoom, MaxZoom);
        }
        return Math.Clamp(Math.Round(current / WheelFactor), MinZoom, MaxZoom);
    }

    /// <summary>ズームイン可能かどうかを返す。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームイン可能なら true。</returns>
    public static bool CanZoomIn(double current, double fitZoom) => NextLevel(current, fitZoom) is not null;

    /// <summary>ズームアウト可能かどうかを返す。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <returns>ズームアウト可能なら true。</returns>
    public static bool CanZoomOut(double current, double fitZoom) => PrevLevel(current, fitZoom) is not null;
}

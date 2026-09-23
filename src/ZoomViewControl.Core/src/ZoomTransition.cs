namespace Estable.ZoomView;

/// <summary>
/// ズーム遷移の計算ロジック（純粋関数）。
/// </summary>
/// <remarks>
/// ⊖⊕ ボタンの 23 段階遷移と Ctrl+ホイールの ×1.1 遷移を決定する。<br/>
/// UI 状態（ViewModel・コントロール）に依存しないため、単体テストで遷移仕様を検証できる。<br/>
/// UI 実装（Avalonia / MewUI のズームビュー）から呼び出される。<br/>
/// 遷移パラメータ（ズーム範囲・倍率・レベル集合）は <see cref="ZoomConfig"/> で受け取る純粋関数であり、状態を持たない。
/// </remarks>
internal static class ZoomTransition
{
    /// <summary>⊖⊕ボタンのズームレベルの既定値（Fit は含まない。23 段階）。</summary>
    internal static readonly double[] DefaultButtonZoomLevels = [10, 12, 15, 20, 25, 33, 40, 50, 62, 75, 80, 90, 100, 110, 125, 150, 200, 250, 300, 350, 400, 450, 500];

    /// <summary>Ctrl+ホイールのズーム倍率の既定値。</summary>
    internal const double DefaultWheelFactor = 1.1;

    /// <summary>ズーム率の最小値（固定倍率）の既定値。</summary>
    internal const double DefaultMinZoom = 10;

    /// <summary>ズーム率の最大値（固定倍率）の既定値。</summary>
    internal const double DefaultMaxZoom = 500;

    /// <summary>ズームインの次のレベルを返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <returns>次のズームレベル。移動先がなければ null。</returns>
    /// <remarks>
    /// <b>固定倍率表示中</b>: 現在の値より大きい最初のレベルへ移動する（リスト内は隣、リスト外は次に大きい）。<br/>
    /// <b>Fit 実効倍率を挟む</b>: 次に大きいレベル <c>next</c> と Fit 実効倍率 <c>fitZoom</c> が
    /// <c>current &lt; fitZoom &lt; next</c> を満たす場合、<c>fitZoom</c> を次の倍率とする<br/>
    /// （ズームインでレベルを通過する際、Fit 実効倍率が間に挟まる場合はそれを経由する）。<br/>
    /// <b>Fit 表示中</b>: 実効倍率より大きい最初のレベルへ移動する（実効倍率は <see cref="ZoomConfig.MinZoom"/>〜<see cref="ZoomConfig.MaxZoom"/> にクランプ）。<br/>
    /// リスト末尾（<see cref="ZoomConfig.MaxZoom"/>）で null（移動不可）を返す。<br/>
    /// Fit 実効倍率がレベルの近傍（<c>fitZoom &lt;= current</c> または <c>fitZoom &gt;= next</c>）の場合は段階レベルを優先する。
    /// </remarks>
    internal static double? NextLevel(double current, double fitZoom, ZoomConfig? config = null)
    {
        var c = config ?? ZoomConfig.Default;
        // レベルは整数値のため、比較・インデックス探索は整数値として扱う
        var levels = c.ButtonZoomLevels;
        var baseValue = current <= 0 ? (int)Math.Floor(fitZoom) : (int)Math.Round(current);
        var idx = BinarySearch(levels, baseValue);
        var nextIdx = idx >= 0 ? idx + 1 : ~idx;
        if (nextIdx >= levels.Count) return null;
        var next = levels[nextIdx];

        // Fit 実効倍率は下限・上限にクランプする（Fit 表示時でも MinZoom〜MaxZoom の範囲内）
        fitZoom = c.ClampZoom(fitZoom);

        // 固定倍率（current > 0）で、Fit 実効倍率が「現在値」と「次に大きいレベル」の間に挟まる場合、
        // fitZoom を次の倍率とする。近接（fitZoom <= current または fitZoom >= next）の場合は段階レベルを優先。
        if (current > 0 && current < fitZoom && fitZoom < next)
        {
            return fitZoom;
        }
        return next;
    }

    /// <summary>ズームアウトの前のレベルを返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のとき、または固定倍率で Fit 実効倍率を挟むときに使用。</param>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <returns>前のズームレベル。移動先がなければ null。</returns>
    /// <remarks>
    /// <b>固定倍率表示中</b>: 現在の値より小さい最後のレベルへ移動する（リスト内は隣、リスト外は次に小さい）。<br/>
    /// <b>Fit 実効倍率を挟む</b>: 次に小さいレベル <c>prev</c> と Fit 実効倍率 <c>fitZoom</c> が
    /// <c>prev &lt; fitZoom &lt; current</c> を満たす場合、<c>fitZoom</c> を次の倍率とする<br/>
    /// （ズームアウトでレベルを通過する際、Fit 実効倍率が間に挟まる場合はそれを経由する）。<br/>
    /// <b>Fit 表示中</b>: 実効倍率より小さい最後のレベルへ移動する。<br/>
    /// リスト先頭（<see cref="ZoomConfig.MinZoom"/>）で null（移動不可）を返す。<br/>
    /// Fit 実効倍率がレベルの近傍（<c>prev</c> 以下）の場合は段階レベルを優先する。
    /// </remarks>
    internal static double? PrevLevel(double current, double fitZoom, ZoomConfig? config = null)
    {
        var c = config ?? ZoomConfig.Default;
        // レベルは整数値のため、比較・インデックス探索は整数値として扱う
        var levels = c.ButtonZoomLevels;
        var baseValue = current <= 0 ? (int)Math.Ceiling(fitZoom) : (int)Math.Round(current);
        var idx = BinarySearch(levels, baseValue);
        var prevIdx = idx > 0 ? idx - 1 : ~idx - 1;
        if (prevIdx < 0) return null;
        var prev = levels[prevIdx];

        // Fit 実効倍率は下限・上限にクランプする（Fit 表示時でも MinZoom〜MaxZoom の範囲内）
        fitZoom = c.ClampZoom(fitZoom);

        // 固定倍率（current > 0）で、Fit 実効倍率が「次に小さいレベル」と「現在値」の間に挟まる場合、
        // fitZoom を次の倍率とする。近接（prev <= fitZoom）の場合は段階レベルを優先。
        if (current > 0 && prev < fitZoom && fitZoom < current)
        {
            return fitZoom;
        }
        return prev;
    }

    /// <summary>Ctrl+ホイールのズームイン倍率を返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <returns>ズームイン後の倍率（Round(現在 × WheelFactor)、MinZoom〜MaxZoom にクランプ）。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率 × WheelFactor へ移動する（実効倍率 ≥ <see cref="ZoomConfig.MaxZoom"/> なら null = 移動不可）。<br/>
    /// Fit は遷移に含めない。
    /// </remarks>
    internal static double? WheelZoomIn(double current, double fitZoom, ZoomConfig? config = null)
    {
        var c = config ?? ZoomConfig.Default;
        if (current <= 0)
        {
            if (fitZoom >= c.MaxZoom) return null;
            return c.ClampZoom(Math.Round(fitZoom * c.WheelFactor));
        }
        return c.ClampZoom(Math.Round(current * c.WheelFactor));
    }

    /// <summary>Ctrl+ホイールのズームアウト倍率を返す。移動先がなければ null。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <returns>ズームアウト後の倍率（Round(現在 ÷ WheelFactor)、MinZoom〜MaxZoom にクランプ）。移動先がなければ null。</returns>
    /// <remarks>
    /// Fit 表示中は実効倍率 ÷ WheelFactor へ移動する（実効倍率 ≤ <see cref="ZoomConfig.MinZoom"/> なら null = 移動不可）。<br/>
    /// Fit は遷移に含めない。
    /// </remarks>
    internal static double? WheelZoomOut(double current, double fitZoom, ZoomConfig? config = null)
    {
        var c = config ?? ZoomConfig.Default;
        if (current <= 0)
        {
            if (fitZoom <= c.MinZoom) return null;
            return c.ClampZoom(Math.Round(fitZoom / c.WheelFactor));
        }
        return c.ClampZoom(Math.Round(current / c.WheelFactor));
    }

    /// <summary>ズームイン可能かどうかを返す。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <returns>ズームイン可能なら true。</returns>
    internal static bool CanZoomIn(double current, double fitZoom, ZoomConfig? config = null) => NextLevel(current, fitZoom, config) is not null;

    /// <summary>ズームアウト可能かどうかを返す。</summary>
    /// <param name="current">現在のズーム率（0 = Fit）。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <returns>ズームアウト可能なら true。</returns>
    internal static bool CanZoomOut(double current, double fitZoom, ZoomConfig? config = null) => PrevLevel(current, fitZoom, config) is not null;

    /// <summary>ズーム操作の可否（ズームイン・ズームアウト）を導出する。</summary>
    /// <param name="current">現在のズーム率（%）。0 = Fit。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。current &lt;= 0 のときのみ使用。</param>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <param name="hasSource">画像ソースが設定されているか（未設定時は可否を false にする）。</param>
    /// <returns>ズームイン・ズームアウトの可否。</returns>
    /// <remarks>
    /// <see cref="ZoomViewSession"/> の可否確定（遷移可否）を純粋ロジックとして集約する。<br/>
    /// 画像未設定（<paramref name="hasSource"/> = false）時は何れも不可（false）を返す。
    /// </remarks>
    internal static (bool CanZoomIn, bool CanZoomOut) BuildAvailability(
        double current, double fitZoom, ZoomConfig config, bool hasSource)
    {
        if (current <= 0 && !hasSource)
        {
            return (false, false);
        }

        return (CanZoomIn(current, fitZoom, config), CanZoomOut(current, fitZoom, config));
    }

    /// <summary>昇順ソート済みの <see cref="IReadOnlyList{T}"/> に対して二分探索を行う（<see cref="Array.BinarySearch{T}(T[], T)"/> と同値の戻り値）。</summary>
    /// <param name="levels">昇順ソート済みのレベル集合。</param>
    /// <param name="value">探索する値。</param>
    /// <returns>値が見つかった場合はそのインデックス、見つからない場合はビット単位補数（~idx）で挿入位置。</returns>
    private static int BinarySearch(IReadOnlyList<double> levels, double value)
    {
        var low = 0;
        var high = levels.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            var item = levels[mid];
            if (item == value) return mid;
            if (item < value) low = mid + 1;
            else high = mid - 1;
        }
        return ~low;
    }
}

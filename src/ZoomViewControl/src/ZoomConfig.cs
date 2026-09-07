namespace Estable.AvaloniaUI.ZoomView;

/// <summary>ズーム遷移の設定（不変値・検証付き）。</summary>
/// <remarks>
/// ズーム遷移のパラメータをまとめた不変値。<br/>
/// <see cref="ZoomViewControl"/> が所有し、純粋関数である <see cref="ZoomTransition"/> へ渡す（状態を純粋関数に持たせない）。<br/>
/// プロパティは読み取り専用で、<see cref="With"/> により検証付きの部分更新を行う（値を直接変更できない）。<br/>
/// 既定値は従来の定数値（MinZoom=10 / MaxZoom=500 / WheelFactor=1.1 / 23 段階）。<br/>
/// <b>拡張点</b>: ズーム範囲・倍率・レベル集合を外部からカスタマイズするための公開値オブジェクト。<br/>
/// コンストラクタまたは <see cref="With"/> で検証付きの部分更新を行い、不正な値はガードされる。
/// </remarks>
public sealed class ZoomConfig
{
    /// <summary>ズーム率の最小値（固定倍率。%）。</summary>
    /// <value>0 より大きい。</value>
    public double MinZoom { get; }

    /// <summary>ズーム率の最大値（固定倍率。%）。</summary>
    /// <value><see cref="MinZoom"/> 以上。</value>
    public double MaxZoom { get; }

    /// <summary>Ctrl+ホイールのズーム倍率。</summary>
    /// <value>1 より大きい。</value>
    public double WheelFactor { get; }

    /// <summary>⊖⊕ ボタンのズームレベル集合（昇順ソート済み・Fit は含まない）。</summary>
    /// <value>非空・昇順・<see cref="MinZoom"/>〜<see cref="MaxZoom"/> の範囲内。</value>
    public IReadOnlyList<double> ButtonZoomLevels { get; }

    /// <summary>既定値に基づく <see cref="ZoomConfig"/> を生成する。</summary>
    public ZoomConfig() : this(
        ZoomTransition.DefaultMinZoom,
        ZoomTransition.DefaultMaxZoom,
        ZoomTransition.DefaultWheelFactor,
        ZoomTransition.DefaultButtonZoomLevels) { }

    /// <summary>すべてのプロパティを指定して <see cref="ZoomConfig"/> を生成する。</summary>
    /// <param name="minZoom">ズーム率の最小値。0 より大きい。</param>
    /// <param name="maxZoom">ズーム率の最大値。<paramref name="minZoom"/> 以上。</param>
    /// <param name="wheelFactor">Ctrl+ホイールのズーム倍率。1 より大きい。</param>
    /// <param name="buttonZoomLevels">⊖⊕ ボタンのズームレベル集合。非空・昇順・範囲内。</param>
    /// <remarks>各値の妥当性を検証し、不正な場合は <see cref="ArgumentException"/> を投げる。</remarks>
    public ZoomConfig(double minZoom, double maxZoom, double wheelFactor, IReadOnlyList<double> buttonZoomLevels)
    {
        MinZoom = minZoom;
        MaxZoom = maxZoom;
        WheelFactor = wheelFactor;
        ButtonZoomLevels = buttonZoomLevels;
        Validate();
    }

    /// <summary>従来の定数値に基づく既定の設定。</summary>
    public static ZoomConfig Default { get; } = new(
        ZoomTransition.DefaultMinZoom,
        ZoomTransition.DefaultMaxZoom,
        ZoomTransition.DefaultWheelFactor,
        ZoomTransition.DefaultButtonZoomLevels);

    /// <summary>一部のプロパティを差し替えた、検証付きの新しい <see cref="ZoomConfig"/> を返す。</summary>
    /// <param name="minZoom">新しい最小値。null の場合は現在値。</param>
    /// <param name="maxZoom">新しい最大値。null の場合は現在値。</param>
    /// <param name="wheelFactor">新しいズーム倍率。null の場合は現在値。</param>
    /// <param name="buttonZoomLevels">新しいレベル集合。null の場合は現在値。</param>
    /// <returns>検証済みの新しい <see cref="ZoomConfig"/>。</returns>
    /// <remarks>コンストラクタ（検証付き）を経由するため、不正な値には <see cref="ArgumentException"/> が投げられる。</remarks>
    public ZoomConfig With(
        double? minZoom = null,
        double? maxZoom = null,
        double? wheelFactor = null,
        IReadOnlyList<double>? buttonZoomLevels = null)
        => new(
            minZoom ?? MinZoom,
            maxZoom ?? MaxZoom,
            wheelFactor ?? WheelFactor,
            buttonZoomLevels ?? ButtonZoomLevels);

    /// <summary>ズーム率を <see cref="MinZoom"/>〜<see cref="MaxZoom"/> にクランプする。</summary>
    /// <param name="zoom">クランプするズーム率（%）。</param>
    /// <returns>クランプ後のズーム率（%）。</returns>
    internal double ClampZoom(double zoom) => Math.Clamp(zoom, MinZoom, MaxZoom);

    /// <summary>不変条件（値・範囲・昇順・非空）を検証する。</summary>
    /// <exception cref="ArgumentException">値が範囲外、またはレベル集合が不正なとき。</exception>
    private void Validate()
    {
        if (MinZoom <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinZoom), MinZoom, "ズーム率の最小値は 0 より大きい必要があります。");
        if (MaxZoom < MinZoom)
            throw new ArgumentOutOfRangeException(nameof(MaxZoom), MaxZoom, "ズーム率の最大値は最小値以上である必要があります。");
        if (WheelFactor <= 1)
            throw new ArgumentOutOfRangeException(nameof(WheelFactor), WheelFactor, "ズーム倍率は 1 より大きい必要があります。");
        ValidateButtonZoomLevels();
    }

    /// <summary>レベル集合（非空・昇順・範囲内）を検証する。</summary>
    /// <exception cref="ArgumentException">レベル集合が空、昇順でない、または範囲外の値が含まれるとき。</exception>
    private void ValidateButtonZoomLevels()
    {
        if (ButtonZoomLevels is null)
            throw new ArgumentNullException(nameof(ButtonZoomLevels), "ズームレベル集合は null にできません。");
        if (ButtonZoomLevels.Count == 0)
            throw new ArgumentException("ズームレベル集合は空にできません。", nameof(ButtonZoomLevels));

        for (var i = 0; i < ButtonZoomLevels.Count; i++)
        {
            var level = ButtonZoomLevels[i];
            if (level < MinZoom || level > MaxZoom)
                throw new ArgumentOutOfRangeException(nameof(ButtonZoomLevels), level, "ズームレベルはズーム範囲内である必要があります。");
            if (i > 0 && ButtonZoomLevels[i - 1] >= level)
                throw new ArgumentException($"ズームレベルは昇順である必要があります（インデックス {i}）。", nameof(ButtonZoomLevels));
        }
    }
}

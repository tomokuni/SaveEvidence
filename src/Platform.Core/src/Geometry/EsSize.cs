namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元サイズを表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークのサイズ型へ変換する（一方向依存）。<br/>
/// Avalonia のサイズ型との変換は <c>EsGeometryAvaloniaInterop</c> が提供する。<br/>
/// 「レイアウト前の領域（幅・高さが 0）ではクランプしない」という領域の規則は本型が単一所有する。<br/>
/// </remarks>
/// <param name="Width">幅（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
/// <param name="Height">高さ（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
public readonly record struct EsSize(double Width, double Height)
{
    /// <summary>領域として面積が確定しているか（幅・高さとも正か）を取得する。</summary>
    /// <value>幅・高さとも 0 より大きい場合は true。</value>
    /// <remarks>レイアウト前・親未接続の領域は幅または高さが 0 になるため、確定していないと判定する。</remarks>
    public bool HasArea => Width > 0 && Height > 0;

    /// <summary>サイズを本領域の最小辺へクランプする（領域が未確定の場合はクランプしない）。</summary>
    /// <param name="size">クランプするサイズ（正の値）。</param>
    /// <returns>クランプ後のサイズ。領域が未確定の場合は <paramref name="size"/> のまま。</returns>
    /// <remarks>正方形の領域へ収める用途を想定し、幅・高さの小さい方（最小辺）を上限とする。</remarks>
    public double ClampToArea(double size)
        => HasArea ? Math.Min(size, Math.Min(Width, Height)) : size;

    /// <summary>本領域内の位置（左上オフセット）を 1 軸分クランプする（領域が未確定の場合はクランプしない）。</summary>
    /// <param name="offset">クランプするオフセット（領域の左上を原点とする）。</param>
    /// <param name="size">領域内へ収める対象の 1 軸のサイズ。</param>
    /// <param name="axis">クランプに使う軸（<see cref="EsAxis.X"/> = 幅 / <see cref="EsAxis.Y"/> = 高さ）。</param>
    /// <returns>クランプ後のオフセット（0〜<c>領域の長さ − サイズ</c>）。対象が収まらない場合は 0。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> が <see cref="EsAxis"/> の定義値でない場合。</exception>
    /// <remarks>
    /// 軸ごとに呼ぶ（縦横で領域の長さが異なるため）。領域の長さと対象のサイズの差を上限とするため、<br/>
    /// 対象が領域より大きい場合は 0（左上寄せ）になる。
    /// </remarks>
    public double ClampOffset(double offset, double size, EsAxis axis)
    {
        var areaLength = axis switch
        {
            EsAxis.X => Width,
            EsAxis.Y => Height,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "軸は EsAxis の定義値である必要があります。"),
        };

        return areaLength > 0 ? Math.Clamp(offset, 0, Math.Max(0, areaLength - size)) : offset;
    }
}

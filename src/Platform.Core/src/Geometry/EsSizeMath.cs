namespace EsUtil.Platform.Core.Geometry;

/// <summary>サイズ（領域）の判定とクランプの計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 「領域が確定しているか（幅・高さとも正か）」「サイズや位置を領域内へクランプする」という、<br/>
/// レイアウト前の未確定な領域を扱う規則を単一所有する（<b>未確定ならクランプしない</b>）。<br/>
/// 各コア・各 UI が <c>width &gt; 0 &amp;&amp; height &gt; 0</c> を書くと、同じ意味の述語が散り、<br/>
/// 片方だけがレイアウト前の値を勝手に動かして要素が意図しない位置へ飛ぶ。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class EsSizeMath
{
    /// <summary>領域のサイズが確定しているか（幅・高さとも正か）を判定する。</summary>
    /// <param name="area">判定する領域のサイズ。</param>
    /// <returns>幅・高さとも 0 より大きい場合は true。</returns>
    /// <remarks>レイアウト前・親未接続の領域は幅または高さが 0 になるため、確定していないと判定する。</remarks>
    public static bool HasArea(EsSize area) => area.Width > 0 && area.Height > 0;

    /// <summary>サイズを領域の最小辺へクランプする（領域が未確定の場合はクランプしない）。</summary>
    /// <param name="size">クランプするサイズ（正の値）。</param>
    /// <param name="area">クランプ先の領域のサイズ。幅または高さが 0 以下の場合はクランプしない。</param>
    /// <returns>クランプ後のサイズ。領域が未確定の場合は <paramref name="size"/> のまま。</returns>
    /// <remarks>正方形の領域へ収める用途を想定し、幅・高さの小さい方（最小辺）を上限とする。</remarks>
    public static double ClampToArea(double size, EsSize area)
        => HasArea(area) ? Math.Min(size, Math.Min(area.Width, area.Height)) : size;

    /// <summary>領域内の位置（左上オフセット）を 1 軸分クランプする（領域が未確定の場合はクランプしない）。</summary>
    /// <param name="offset">クランプするオフセット（領域の左上を原点とする）。</param>
    /// <param name="areaLength">領域の 1 軸の長さ。0 以下の場合はクランプしない。</param>
    /// <param name="size">領域内へ収める対象の 1 軸のサイズ。</param>
    /// <returns>クランプ後のオフセット（0〜<c>領域の長さ − サイズ</c>）。対象が収まらない場合は 0。</returns>
    /// <remarks>
    /// 軸ごとに呼ぶ（縦横で領域の長さが異なるため）。領域の長さと対象のサイズの差を上限とするため、<br/>
    /// 対象が領域より大きい場合は 0（左上寄せ）になる。
    /// </remarks>
    public static double ClampOffsetToArea(double offset, double areaLength, double size)
        => areaLength > 0 ? Math.Clamp(offset, 0, Math.Max(0, areaLength - size)) : offset;
}

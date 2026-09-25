namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の整数のサイズ（幅・高さ）を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="EsSize"/>（小数を扱う DIP 相当のサイズ）の<b>整数版</b>である。<br/>
/// 画像の実寸・画面物理ピクセルの寸法など、整数でしか意味を持たない寸法に使用する<br/>
/// （<see cref="EsPointInt"/> / <see cref="EsRectInt"/> と同じ整数系の値型）。<br/>
/// 各 UI 実装の寸法型との変換は Interop 層が提供する。
/// </remarks>
/// <param name="Width">幅（整数）。</param>
/// <param name="Height">高さ（整数）。</param>
public readonly record struct EsSizeInt(int Width, int Height)
{
    /// <summary>幅または高さが 0 以下かどうか（面積を持たない）を取得する。</summary>
    /// <value>幅または高さが 0 以下の場合は true。</value>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>小数を扱うサイズ（<see cref="EsSize"/>）へ変換する。</summary>
    /// <returns>幅・高さをそのまま引き継いだサイズ。</returns>
    /// <remarks>整数の寸法をコアの寸法型として扱う場合に使用する（値は変わらない）。</remarks>
    public EsSize ToEsSize() => new(Width, Height);
}

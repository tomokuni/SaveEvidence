namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の画面物理座標（整数ピクセル）を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークの座標型へ変換する（一方向依存）。<br/>
/// 小数を扱う <see cref="EsPoint"/> への変換は本型が単一所有する（UI が成分を書き写すと、同じ換算が各所に散る）。
/// </remarks>
/// <remarks>
/// 画面の物理ピクセル座標（DPI スケーリング適用後）を表す。<br/>
/// DIP（論理座標）は <see cref="EsPoint"/> を使用する。<br/>
/// 各 UI 実装の物理座標型との変換は Interop 層が提供する。<br/>
/// </remarks>
/// <param name="X">X 座標（画面物理ピクセル）。</param>
/// <param name="Y">Y 座標（画面物理ピクセル）。</param>
public readonly record struct EsPixelPoint(int X, int Y)
{
    /// <summary>小数を扱う座標（<see cref="EsPoint"/>）へ変換する。</summary>
    /// <returns>X / Y をそのまま引き継いだ座標。</returns>
    /// <remarks>画面物理座標（整数）をコアの座標型として扱う場合に使用する（値は変わらない）。</remarks>
    public EsPoint ToEsPoint() => new(X, Y);
}

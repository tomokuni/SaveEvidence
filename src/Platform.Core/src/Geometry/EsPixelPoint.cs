namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の画面物理座標（整数ピクセル）を表す値オブジェクト。</summary>
/// <remarks>
/// 画面の物理ピクセル座標（DPI スケーリング適用後）を表す。<br/>
/// DIP（論理座標）は <see cref="EsPoint"/> を使用する。<br/>
/// 各 UI 実装の物理座標型との変換は Interop 層が提供する。<br/>
/// </remarks>
/// <param name="X">X 座標（画面物理ピクセル）。</param>
/// <param name="Y">Y 座標（画面物理ピクセル）。</param>
public readonly record struct EsPixelPoint(int X, int Y);

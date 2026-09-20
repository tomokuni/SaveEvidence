namespace Estable.Platform.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元座標を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークの座標型へ変換する（一方向依存）。<br/>
/// Avalonia の座標型との変換は <c>EsGeometryAvaloniaInterop</c> が提供する。<br/>
/// </remarks>
/// <param name="X">X 座標（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
/// <param name="Y">Y 座標（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
public readonly record struct EsPoint(double X, double Y);

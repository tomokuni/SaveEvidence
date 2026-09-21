namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元ベクター（オフセット）を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークのベクター型へ変換する（一方向依存）。<br/>
/// Avalonia のベクター型との変換は <c>EsGeometryAvaloniaInterop</c> が提供する。<br/>
/// </remarks>
/// <param name="X">X 方向の成分（呼び出し側の座標系に従う）。</param>
/// <param name="Y">Y 方向の成分（呼び出し側の座標系に従う）。</param>
public readonly record struct EsVector(double X, double Y);

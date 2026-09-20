namespace Estable.Platform.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元サイズを表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークのサイズ型へ変換する（一方向依存）。<br/>
/// Avalonia のサイズ型との変換は <c>EsGeometryAvaloniaInterop</c> が提供する。<br/>
/// </remarks>
/// <param name="Width">幅（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
/// <param name="Height">高さ（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
public readonly record struct EsSize(double Width, double Height);

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>アイコンの図形を枠いっぱいへ広げる変換。</summary>
/// <param name="Scale">拡大率（縦横共通）。</param>
/// <param name="OffsetX">拡大後の X 方向の平行移動。</param>
/// <param name="OffsetY">拡大後の Y 方向の平行移動。</param>
/// <remarks>適用順は「拡大してから平行移動」である（<c>x' = x * Scale + OffsetX</c>）。</remarks>
internal readonly record struct IconFitTransform(double Scale, double OffsetX, double OffsetY);

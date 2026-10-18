namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>影のグラデーションの 1 停止点。</summary>
/// <param name="Offset">グラデーションの始点からの位置（0〜1）。</param>
/// <param name="Argb">色（<c>0xAARRGGBB</c>）。 透過度は影の濃さそのもの。</param>
public readonly record struct ShadowStop(double Offset, uint Argb);

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>影を塗る 1 辺分の矩形と、その辺の計画。</summary>
/// <param name="X">左端（DIP）。</param>
/// <param name="Y">上端（DIP）。</param>
/// <param name="Width">幅（DIP）。</param>
/// <param name="Height">高さ（DIP）。</param>
/// <param name="Edge">辺の計画（濃さの停止点と向き）。</param>
public readonly record struct ShadowEdgeFill(double X, double Y, double Width, double Height, ShadowEdgePlan Edge);

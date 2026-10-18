namespace EsUtil.SaveEvidence.Platform.Skia;

/// <summary>影の 1 画素分の帯（物理ピクセルの矩形と色）。</summary>
/// <param name="X">左端（物理ピクセル）。</param>
/// <param name="Y">上端（物理ピクセル）。</param>
/// <param name="Width">幅（物理ピクセル）。</param>
/// <param name="Height">高さ（物理ピクセル）。</param>
/// <param name="Argb">塗る色（<c>0xAARRGGBB</c>）。 透過度が影の濃さ。</param>
/// <remarks>濃さの計画はアプリの <c>ShadowPlan</c> が単一所有する（本型は描画の入力にすぎない）。</remarks>
public readonly record struct SkiaShadowBand(int X, int Y, int Width, int Height, uint Argb);
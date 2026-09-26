using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Contract;


namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary>テストで使用する描画実装のスタブ（描画せず、呼び出しだけを数える）。</summary>
/// <remarks>
/// コアは描画層を参照しないため、コアのテストは <see cref="ILoupeRenderer"/> の実装を注入する<br/>
/// （描画の手順そのものは描画層のテストが検証する）。本スタブは「ファサードが注入された実装へ委譲すること」<br/>
/// と「実装を差し替えられること」の検証に使う。
/// </remarks>
internal sealed class StubLoupeRenderer : ILoupeRenderer
{
    /// <summary>図形の描画を要求された回数。</summary>
    public int DrawShapesCalls { get; private set; }

    /// <summary><see cref="CoordLabelFontSize"/> が返す値（既定は 12.0）。</summary>
    public double CoordLabelFontSize { get; set; } = 12.0;

    /// <summary><see cref="CoordLabelCornerRadius"/> が返す値（既定は 2.0）。</summary>
    public double CoordLabelCornerRadius { get; set; } = 2.0;

    /// <inheritdoc/>
    public void DrawShapes(
        IEsCanvas canvas, EsBitmap? source, in LoupeRenderRequest request, in LoupeDrawContent content)
        => DrawShapesCalls++;
}

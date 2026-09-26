using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Contract;
using EsUtil.Feature.MarqueeCore.Drawing;


namespace EsUtil.Feature.MarqueeCore.Tests;

/// <summary>テストで使用する描画実装のスタブ（描画せず、呼び出しだけを数える）。</summary>
/// <remarks>
/// コアは描画層を参照しないため、コアのテストは <see cref="IMarqueeRenderer"/> の実装を注入する<br/>
/// （描画の手順そのものは描画層のテストが検証する）。本スタブは「ファサードが注入された実装へ委譲すること」<br/>
/// と「実装を差し替えられること」の検証に使う。
/// </remarks>
internal sealed class StubMarqueeRenderer : IMarqueeRenderer
{
    /// <summary>図形の描画を要求された回数。</summary>
    public int DrawShapesCalls { get; private set; }

    /// <summary><see cref="SizeLabelFontSize"/> が返す値（既定は 14.0）。</summary>
    public double SizeLabelFontSize { get; set; } = 14.0;

    /// <summary><see cref="CoordLabelFontSize"/> が返す値（既定は 12.0）。</summary>
    public double CoordLabelFontSize { get; set; } = 12.0;

    /// <inheritdoc/>
    public void DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content) => DrawShapesCalls++;
}

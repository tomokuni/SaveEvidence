using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Contracts;
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
    /// <summary>ラベル要素の解決を要求された回数。</summary>
    public int ResolveLabelsCalls { get; private set; }

    /// <summary><see cref="ResolveLabels"/> が返す要素（ファサードの戻り値の検証に使う）。</summary>
    public IReadOnlyList<EsLabelElement> Elements { get; set; } = [];

    /// <inheritdoc/>
    public void DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content)
    {
    }

    /// <inheritdoc/>
    public IReadOnlyList<EsLabelElement> ResolveLabels(
        in MarqueeDrawContent content, Func<string, double, EsSize> measureText)
    {
        ResolveLabelsCalls++;
        return Elements;
    }
}

using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Contract;
using EsUtil.Feature.MarqueeCore.Drawing;


namespace EsUtil.SaveEvidence.Marquee.UIBase.Tests;

/// <summary>テストで使用する描画実装のスタブ（描画しない）。</summary>
/// <remarks>
/// 本テストはホイールの解釈とビューポート変化の通知を検証するため、描画は要求されない。<br/>
/// コアは描画層を参照しないため、ファサードの生成には <see cref="IMarqueeRenderer"/> の実装が必要である。
/// </remarks>
internal sealed class StubMarqueeRenderer : IMarqueeRenderer
{
    /// <inheritdoc/>
    public void DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content)
    {
    }
}

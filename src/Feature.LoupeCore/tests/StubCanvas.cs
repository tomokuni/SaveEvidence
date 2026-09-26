using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary>テストで使用する描画ポートのスタブ（描画せず、呼び出しだけ受け付ける）。</summary>
/// <remarks>
/// コアのテストは描画内容の解決と描画実装への委譲を検証するため、実際の描画面は不要である。<br/>
/// ピクセルを検証する場合は描画層のテスト（<c>Platform.Skia</c> の <c>SkiaEsCanvas</c>）を使う。
/// </remarks>
internal sealed class StubCanvas : IEsCanvas
{
    /// <summary>共有インスタンス（状態を持たないためテスト間で共有できる）。</summary>
    public static StubCanvas Instance { get; } = new();

    /// <inheritdoc/>
    public void Clear(EsRect surface, DrawingColor color)
    {
    }

    /// <inheritdoc/>
    public void FillRect(EsRect rect, DrawingColor color)
    {
    }

    /// <inheritdoc/>
    public void FillRects(IReadOnlyList<EsRect> rects, DrawingColor color)
    {
    }

    /// <inheritdoc/>
    public void FillRects<TWalker>(TWalker rects, DrawingColor color)
        where TWalker : struct, IEsRectWalker<TWalker>
    {
    }

    /// <inheritdoc/>
    public void FillRoundRect(EsRect rect, double cornerRadius, DrawingColor color)
    {
    }

    /// <inheritdoc/>
    public void StrokeRect(EsRect rect, DrawingColor color, double thickness)
    {
    }

    /// <inheritdoc/>
    public void DrawLine(EsPoint from, EsPoint to, DrawingColor color, double thickness)
    {
    }

    /// <inheritdoc/>
    public void DrawLabel(
        EsRect background,
        DrawingColor backgroundColor,
        string text,
        EsPoint textOrigin,
        double fontSize,
        DrawingColor textColor,
        double cornerRadius)
    {
    }

    /// <inheritdoc/>
    public void DrawImage(EsBitmap image, EsRect source, EsRect destination)
    {
    }

    /// <inheritdoc/>
    public EsSize MeasureText(string text, double fontSize) => default;
}

using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Estable.ZoomView;
using SkiaSharp;

namespace Estable.MewUI.ZoomView;

/// <summary>ズームビューの画像描画を Skia で行うレンダラー。</summary>
/// <remarks>
/// UI 非依存コア（<see cref="ZoomMath"/>）の計算結果（実効スケール・画像原点・表示サイズ）を使って<br/>
/// <see cref="SKCanvas"/> へ画像を描画するだけの層。<br/>
/// 座標はすべて DIP で扱い、呼び出し側が DPI スケーリングを適用済みであることを前提とする<br/>
/// （キャンバスを <c>Scale</c> しておけば、コアの計算と同じ DIP 座標で描画できる）。<br/>
/// Avalonia 版は <c>Image</c> + <c>RenderTransform</c> で拡大するが、MewUI 版は 1 回の <c>DrawBitmap</c> で描画する。
/// </remarks>
internal static class ZoomSkiaRenderer
{
    /// <summary>画像の表示矩形（ビューポート座標・DIP）を計算する。</summary>
    /// <param name="contentSize">元画像のサイズ（コンテンツ座標）。</param>
    /// <param name="viewportSize">ビューポートのサイズ（DIP）。</param>
    /// <param name="zoomPercent">内部ズーム率（%）。0 = Fit。</param>
    /// <param name="offset">スクロールオフセット（コンテンツ座標）。</param>
    /// <param name="centerAlign">画像がビューポートより小さい軸を中央寄せするか。</param>
    /// <returns>画像の表示矩形（DIP）。画像サイズが 0 以下の場合は空の矩形。</returns>
    /// <remarks>
    /// 実効スケール・表示サイズ・画像原点の計算はコア（<see cref="ZoomMath"/>）へ委譲する（単一所有）。<br/>
    /// コントロールの描画と <c>ImageRectViewport</c> が本メソッドを共用する（表示矩形の単一所有）。
    /// </remarks>
    public static EsRect ComputeImageRect(
        EsSize contentSize, EsSize viewportSize, double zoomPercent, EsVector offset, bool centerAlign)
    {
        if (contentSize.Width <= 0 || contentSize.Height <= 0)
        {
            return default;
        }

        var scale = ZoomMath.ComputeEffectiveScale(zoomPercent, contentSize, viewportSize);
        var displaySize = new EsSize(contentSize.Width * scale, contentSize.Height * scale);
        var origin = ZoomMath.ComputeImageOrigin(
            displaySize, viewportSize, zoomPercent, offset, zoomPercent / 100.0, centerAlign);

        return new EsRect(origin.X, origin.Y, displaySize.Width, displaySize.Height);
    }

    /// <summary>画像を表示矩形へ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="source">表示する画像。null の場合は何も描画しない。</param>
    /// <param name="contentSize">元画像のサイズ（コンテンツ座標）。</param>
    /// <param name="viewportSize">ビューポートのサイズ（DIP）。</param>
    /// <param name="zoomPercent">内部ズーム率（%）。0 = Fit。</param>
    /// <param name="offset">スクロールオフセット（コンテンツ座標）。</param>
    /// <param name="centerAlign">画像がビューポートより小さい軸を中央寄せするか。</param>
    /// <remarks>
    /// 元画像の全面を表示矩形へ拡大描画する（<c>Stretch.Uniform</c> 相当の位置合わせは表示矩形が担う）。<br/>
    /// <c>ToSKBitmap()</c> は内部インスタンスの共有参照を返すため破棄しない。
    /// </remarks>
    public static void Render(
        SKCanvas canvas,
        EsBitmap? source,
        EsSize contentSize,
        EsSize viewportSize,
        double zoomPercent,
        EsVector offset,
        bool centerAlign)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (source is null)
        {
            return;
        }

        var rect = ComputeImageRect(contentSize, viewportSize, zoomPercent, offset, centerAlign);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var src = new SKRect(0, 0, (float)contentSize.Width, (float)contentSize.Height);
        var dst = new SKRect((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

        canvas.DrawBitmap(source.ToSKBitmap(), src, dst);
    }
}

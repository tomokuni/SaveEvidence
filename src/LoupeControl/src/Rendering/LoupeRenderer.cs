using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Loupe.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの描画（拡大画像＋市松模様背景＋十字線＋外枠）を提供するレンダラー。</summary>
/// <remarks>
/// ルーペのビットマップ生成を一元管理する（DragRect の DragRectDrawer と同格の描画層）。<br/>
/// 低レベルピクセル操作を排除し、Avalonia 標準の描画 API に統一することで OS非依存かつ可読性の高い実装となっている。<br/>
/// ソース画像のピクセル抽出は LoupeImageExtractor が担い、合成時の WriteableBitmap はUIスレッドで受け取る。<br/>
/// 領域切り出しは <see cref="LoupeImageExtractor"/> が担う（責務分離）。<br/>
/// 色は <see cref="IBrush"/> で受け取り、文字列解析（string→Color）は行わない（呼び出し側で変換済み）。<br/>
/// </remarks>
internal static class LoupeRenderer
{
    /// <summary>ビットマップ生成時の DPI（96 = 1 ピクセル = 1 DIP）。</summary>
    private static readonly Vector s_dpi = new(96, 96);

    /// <summary>Bitmapと中心座標からルーペ画像を生成します。</summary>
    /// <param name="srcBitmap">拡大元の100%画像の Bitmap。</param>
    /// <param name="centerPoint">拡大中心（元画像ピクセル座標）。</param>
    /// <param name="loupeSize">ルーペの一辺のサイズ（ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="crossColor">十字線の色（不透明度は色のアルファに統合）。null の場合は十字線を描画しない。</param>
    /// <param name="checkerSize">市松模様の1タイルサイズ。</param>
    /// <param name="checkerColorA">市松模様の色A（明色）。null の場合は透明。</param>
    /// <param name="checkerColorB">市松模様の色B（暗色）。null の場合は透明。</param>
    /// <param name="frameColor">外枠の色。null の場合は外枠を描画しない。</param>
    /// <param name="frameWidth">外枠の太さ（ピクセル）。</param>
    /// <returns>生成したルーペ画像。</returns>
    public static Bitmap Render(
        Bitmap? srcBitmap, Point? centerPoint,
        int loupeSize, int zoomLevel,
        IBrush? crossColor,
        int checkerSize, IBrush? checkerColorA, IBrush? checkerColorB,
        IBrush? frameColor, int frameWidth)
    {
        if (srcBitmap is null || centerPoint is not { } center)
        {
            return DrawLoupeCheckerboardOnly(
                loupeSize, crossColor, checkerSize,
                checkerColorA, checkerColorB, frameColor, frameWidth);
        }

        var captureSize = LoupeImageExtractor.ComputeCaptureSize(loupeSize, zoomLevel);
        using var region = LoupeImageExtractor.ExtractRegionAt(
            srcBitmap, (int)center.X, (int)center.Y, captureSize);
        using var cropped = region.ToWriteableBitmap();
        return DrawLoupe(cropped, loupeSize, zoomLevel, crossColor,
            checkerSize, checkerColorA, checkerColorB, frameColor, frameWidth);
    }

    /// <summary>ルーペ画像を描画する。</summary>
    /// <param name="srcBitmap">拡大元の100%画像の Bitmap（切り出し済み）。</param>
    /// <param name="loupeSize">ルーペの一辺のサイズ（ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="crossColor">十字線の色（不透明度は色のアルファに統合）。null の場合は十字線を描画しない。</param>
    /// <param name="checkerSize">市松模様の1タイルサイズ。</param>
    /// <param name="checkerColorA">市松模様の色A（明色）。null の場合は透明。</param>
    /// <param name="checkerColorB">市松模様の色B（暗色）。null の場合は透明。</param>
    /// <param name="frameColor">外枠の色。null の場合は外枠を描画しない。</param>
    /// <param name="frameWidth">外枠の太さ（ピクセル）。</param>
    /// <returns>描画結果の WriteableBitmap（ホストが Lock してオーバーレイを描画できる）。</returns>
    /// <remarks>
    /// ソース Bitmap は <c>DrawImage</c> のスケーリングで拡大描画する（中間バッファ不要）。<br/>
    /// 描画は <see cref="RenderTargetBitmap"/> で行い、結果を <see cref="WriteableBitmap"/> へコピーして返す<br/>
    /// （ホストが <see cref="LoupeControl.Rendered"/> イベントの引数で受け取り、<c>Lock</c> して<br/>
    /// オーバーレイ（線分の破線等）を直接描画できるようにするため）。<br/>
    /// 例外はそのまま伝播し、記録は呼び出し側（<see cref="LoupeControl"/>）が一元的に行う。
    /// </remarks>
    public static WriteableBitmap DrawLoupe(
        Bitmap srcBitmap,
        int loupeSize,
        int zoomLevel,
        IBrush? crossColor,
        int checkerSize,
        IBrush? checkerColorA,
        IBrush? checkerColorB,
        IBrush? frameColor,
        int frameWidth)
    {
        var pixelSize = new PixelSize(loupeSize, loupeSize);
        var rtb = new RenderTargetBitmap(pixelSize, s_dpi);

        using (var ctx = rtb.CreateDrawingContext())
        {
            // 1. 市松模様の背景
            LoupeBackgroundRenderer.DrawCheckerboard(ctx, loupeSize, checkerSize, checkerColorA, checkerColorB);

            // 2. ソース画像を zoomLevel 倍に拡大して描画
            LoupeImageRenderer.Draw(ctx, srcBitmap, zoomLevel);

            // 3. 十字線
            LoupeDecorationRenderer.DrawCrosshair(ctx, loupeSize, zoomLevel, crossColor);

            // 4. 外枠
            LoupeDecorationRenderer.DrawFrame(ctx, loupeSize, frameColor, frameWidth);
        }

        return ToWriteableBitmap(rtb);
    }

    /// <summary>画像なし時の市松模様のみのルーペを描画する。</summary>
    /// <param name="loupeSize">ルーペの一辺のサイズ（ピクセル）。</param>
    /// <param name="crossColor">十字線の色。市松模様のみの描画では使用しない（呼び出し側互換のため）。</param>
    /// <param name="checkerSize">市松模様の1タイルサイズ。</param>
    /// <param name="checkerColorA">市松模様の色A（明色）。null の場合は透明。</param>
    /// <param name="checkerColorB">市松模様の色B（暗色）。null の場合は透明。</param>
    /// <param name="frameColor">外枠の色。null の場合は外枠を描画しない。</param>
    /// <param name="frameWidth">外枠の太さ（ピクセル）。</param>
    /// <returns>市松模様のみの WriteableBitmap。</returns>
    public static WriteableBitmap DrawLoupeCheckerboardOnly(
        int loupeSize,
        IBrush? crossColor,
        int checkerSize,
        IBrush? checkerColorA,
        IBrush? checkerColorB,
        IBrush? frameColor,
        int frameWidth)
    {
        var pixelSize = new PixelSize(loupeSize, loupeSize);
        var rtb = new RenderTargetBitmap(pixelSize, s_dpi);

        using (var ctx = rtb.CreateDrawingContext())
        {
            // 市松模様の背景
            LoupeBackgroundRenderer.DrawCheckerboard(ctx, loupeSize, checkerSize, checkerColorA, checkerColorB);

            // 外枠
            LoupeDecorationRenderer.DrawFrame(ctx, loupeSize, frameColor, frameWidth);
        }

        return ToWriteableBitmap(rtb);
    }

    /// <summary>RenderTargetBitmap の描画結果を WriteableBitmap へコピーして返す。</summary>
    /// <param name="source">描画済みの RenderTargetBitmap。</param>
    /// <returns>ピクセルをコピーした WriteableBitmap（ホストが Lock してオーバーレイを描画できる）。</returns>
    /// <remarks>
    /// ホスト（上位 AP）がイベント内で Bitmap にオーバーレイを描画できるよう、<br/>
    /// CPU 書き込み可能な WriteableBitmap へ変換する（描画自体は RenderTargetBitmap で行う）。
    /// </remarks>
    private static WriteableBitmap ToWriteableBitmap(RenderTargetBitmap source)
    {
        var size = source.PixelSize;
        var stride = size.Width * 4;
        var bitmap = new WriteableBitmap(size, s_dpi);

        using var locked = bitmap.Lock();
        source.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), locked.Address, stride * size.Height, stride);
        return bitmap;
    }

}

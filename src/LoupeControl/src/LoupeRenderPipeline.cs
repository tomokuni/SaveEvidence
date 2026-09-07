using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Estable.AvaloniaUI.Loupe.Imaging;
using Estable.AvaloniaUI.Loupe.Rendering;
using Estable.AvaloniaUI.Platform.Coordinate;
using Estable.AvaloniaUI.Platform.Imaging;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペのビットマップ生成パイプライン（ソース解決・切り出し・描画合成）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> の描画オーケストレーションから、ユーザーが入力したソース画像を<br/>
/// 切り出してルーペ画像（<see cref="PbBitmap"/>）を生成するパイプラインを分離する。<br/>
/// UI スレッド（<see cref="Dispatcher.UIThread"/>）での描画合成とバックグラウンドでの切り出しを担う。<br/>
/// <see cref="LoupeControl"/> の状態（<c>Source</c> / <c>CenterPoint</c> / 外観）は引数で受け取る。
/// </remarks>
internal static class LoupeRenderPipeline
{
    /// <summary>画像抽出を非同期に実行し、Avaloniaの描画オブジェクトをUIスレッドで合成する。</summary>
    /// <param name="source">ルーペのソース画像。</param>
    /// <param name="center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定時は市松模様のみ。</param>
    /// <param name="pixelSize">ルーペの一辺の物理ピクセルサイズ。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="crossColor">十字線の色。</param>
    /// <param name="checkerSize">市松模様の1タイルサイズ（DIP）。</param>
    /// <param name="checkerColorA">色A（明色）。</param>
    /// <param name="checkerColorB">色B（暗色）。</param>
    /// <param name="frameColor">外枠の色。</param>
    /// <param name="frameWidth">外枠の太さ（DIP）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>生成したルーペ画像（<see cref="PbBitmap"/>）。</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="source"/> が <see cref="Bitmap"/> / <see cref="PbBitmapAvaloniaImage"/> 以外の場合。
    /// </exception>
    public static async Task<PbBitmap> RenderAsync(
        Avalonia.Media.IImage? source, Point? center, int pixelSize, int zoomLevel,
        IBrush? crossColor, int checkerSize,
        IBrush? checkerColorA, IBrush? checkerColorB, IBrush? frameColor,
        int frameWidth, double scaling)
    {
        if (source is not null and not Bitmap and not PbBitmapAvaloniaImage)
        {
            throw new NotSupportedException(
                $"LoupeControl は {source.GetType().FullName} をサポートしていません。Bitmap または PbBitmapAvaloniaImage を指定してください。");
        }

        var checkerPixelSize = DpiScaler.ScaleToPixel(checkerSize, scaling);
        var framePixelWidth = DpiScaler.ScaleToPixel(frameWidth, scaling);

        // 画像なし・破棄済み・抽出失敗のいずれでも市松模様のみで描画する（生成経路の一元化）
        Task<PbBitmap> DrawCheckerboardOnly() =>
            Dispatcher.UIThread.InvokeAsync(() => LoupeRenderer.DrawLoupeCheckerboardOnly(
                pixelSize, crossColor, checkerPixelSize,
                checkerColorA, checkerColorB, frameColor, framePixelWidth)).GetTask();

        if (center is not { } centerPoint)
        {
            return await DrawCheckerboardOnly();
        }

        // 抽出元の PbBitmap を解決する（Bitmap は複製、PbBitmapAvaloniaImage は共有参照。破棄済みは市松模様へフォールバック）
        var resolved = LoupeSourceResolver.Resolve(source, out var sourceBitmap);
        if (sourceBitmap is null)
        {
            return await DrawCheckerboardOnly();
        }

        try
        {
            var captureSize = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);
            var region = await Task.Run(() =>
            {
                // Task.Run 実行中に破棄される場合に備え、抽出内でも ObjectDisposedException を捕捉する。
                // この場合も市松模様で描画する（例外を UI へ伝播させない）。
                try
                {
                    return LoupeImageExtractor.ExtractRegionAt(sourceBitmap, (int)centerPoint.X, (int)centerPoint.Y, captureSize);
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
            });

            if (region is null)
            {
                // 抽出中に Source が破棄された: 市松模様のみで描画する
                return await DrawCheckerboardOnly();
            }

            // RenderTargetBitmap生成・DrawingContext操作はUIスレッドに限定する。
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using (region)
                {
                    return LoupeRenderer.DrawLoupe(
                        region, pixelSize, zoomLevel, crossColor,
                        checkerPixelSize, checkerColorA, checkerColorB, frameColor, framePixelWidth);
                }
            });
        }
        finally
        {
            // Bitmap 由来の場合のみ複製を破棄する（PbBitmapAvaloniaImage の共有参照は破棄しない）。
            if (resolved.IsSourceBitmap)
            {
                sourceBitmap.Dispose();
            }
        }
    }
}

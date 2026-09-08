using Avalonia;
using Avalonia.Media;
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
    /// <summary>描画モード（市松模様のみ / ソース画像合成）。</summary>
    internal enum RenderMode
    {
        /// <summary>市松模様のみ（ソースなし・中心未設定・解決不能時）。</summary>
        CheckerboardOnly,
        /// <summary>ソース画像を切り出して合成する。</summary>
        Composite,
    }

    /// <summary>ソース画像・中心座標から描画モードを決定する（純粋関数）。</summary>
    /// <param name="source">ルーペのソース画像。</param>
    /// <param name="center">拡大中心（ドメイン座標）。</param>
    /// <returns>市松模様のみ（<see cref="RenderMode.CheckerboardOnly"/>）または合成（<see cref="RenderMode.Composite"/>）。</returns>
    /// <remarks>
    /// 中心未設定、またはソース画像が対応可能型でない（null 以外の未対応型 / 破棄済み）場合は市松模様のみで描画する。<br/>
    /// 破棄済み <see cref="Bitmap"/> は <see cref="LoupeSourceResolver.Resolve"/> が検出するが、<br/>
    /// 本関数では <c>Resolve</c> を呼ばず <see cref="LoupeSourceResolver.IsSourceSupported"/> のみで判定する<br/>
    /// （解決処理を <see cref="RenderAsync"/> に委譲し、複製ビットマップのリークを防ぐ）。<br/>
    /// UI 非依存のため <c>[Fact]</c> で並列検証できる。
    /// </remarks>
    internal static RenderMode DecideRenderMode(IImage? source, Point? center)
        => center is not null && LoupeSourceResolver.IsSourceSupported(source)
            ? RenderMode.Composite
            : RenderMode.CheckerboardOnly;

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
        // 未対応型の判定は LoupeSourceResolver（単一所有）へ委譲する。
        if (!LoupeSourceResolver.IsSourceSupported(source))
        {
            throw new NotSupportedException(
                $"LoupeControl は {source!.GetType().FullName} をサポートしていません。Bitmap または PbBitmapAvaloniaImage を指定してください。");
        }

        var checkerPixelSize = DpiScaler.ScaleToPixel(checkerSize, scaling);
        var framePixelWidth = DpiScaler.ScaleToPixel(frameWidth, scaling);

        // 画像なし・破棄済み・抽出失敗のいずれでも市松模様のみで描画する（生成経路の一元化）
        Task<PbBitmap> DrawCheckerboardOnly() =>
            Dispatcher.UIThread.InvokeAsync(() => LoupeRenderer.DrawLoupeCheckerboardOnly(
                pixelSize, crossColor, checkerPixelSize,
                checkerColorA, checkerColorB, frameColor, framePixelWidth), DispatcherPriority.Render).GetTask();

        // 中心未設定・ソース解決不能の場合は市松模様のみで描画する（純粋関数で判定・単一所有）。
        if (DecideRenderMode(source, center) == RenderMode.CheckerboardOnly)
        {
            return await DrawCheckerboardOnly();
        }

        // 抽出元の PbBitmap を解決する（Bitmap は複製、PbBitmapAvaloniaImage は共有参照。破棄済みは市松模様へフォールバック）
        var resolved = LoupeSourceResolver.Resolve(source, out var sourceBitmap);

        // 破棄済み Source（DecideRenderMode では型判定のみのため、解決で null になる場合）は市松模様のみで描画する。
        if (sourceBitmap is null)
        {
            return await DrawCheckerboardOnly();
        }

        try
        {
            var centerPoint = center!.Value;
            var captureSize = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);

            // 抽出は PbBitmap.Crop（Buffer.MemoryCopy のみ）で軽量なため、UI スレッドで同期実行する。
            // Task.Run によるバックグラウンド抽出は、マウス移動中の連続再描画でスレッドプールが競合し、
            // 抽出結果が UI スレッドへ戻る前に次の再描画が要求されて、ルーペ画像が移動停止まで更新されない
            // （描画が追いつかない）問題を引き起こす。UI スレッドでの同期抽出はこの競合を排除し、確実に追従する。
            // 破棄済み Source に備え、抽出内で ObjectDisposedException を捕捉する（市松模様へフォールバック）。
            PbBitmap region;
            try
            {
                region = LoupeImageExtractor.ExtractRegionAt(sourceBitmap, (int)centerPoint.X, (int)centerPoint.Y, captureSize);
            }
            catch (ObjectDisposedException)
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
            }, DispatcherPriority.Render);
        }
        finally
        {
            // Bitmap 由来の場合のみ複製を破棄する（PbBitmapAvaloniaImage の共有参照は破棄しない）。
            if (resolved.IsSourceBitmap)
            {
                sourceBitmap!.Dispose();
            }
        }
    }
}

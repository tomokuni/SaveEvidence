using Avalonia;
using Estable.AvaloniaUI.Loupe.Imaging;
using Estable.AvaloniaUI.Platform.Coordinate;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペの描画完了イベント引数（<see cref="LoupeRenderedEventArgs"/>）の表示情報計算ロジック（純粋関数）。</summary>
/// <remarks>
/// <see cref="LoupeControl.RaiseRendered"/> の表示情報（ピクセルサイズ・切り出しサイズ・切り出し左上・スケール）計算を<br/>
/// UI 非依存の純粋関数として提供する。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeRenderArgsCalculator
{
    /// <summary>ルーペの表示情報（描画完了イベント引数用）を計算する。</summary>
    /// <param name="size">ルーペの表示サイズ（一辺・DIP）。</param>
    /// <param name="renderScaling">表示 DIP を物理ピクセルへ変換するスケーリング係数。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定時は null。</param>
    /// <returns>
    /// ピクセルサイズ・切り出し領域の一辺（ドメインピクセル）・切り出し領域の左上（ドメイン座標・中心未設定時は null）・<br/>
    /// ドメイン座標 → ルーペ画像ピクセル座標の変換係数。
    /// </returns>
    /// <remarks>
    /// 切り出し領域の左上計算は <see cref="LoupeImageExtractor.ComputeCaptureTopLeft"/> に委譲する（単一所有・DRY）。
    /// </remarks>
    public static RenderArgs Compute(
        double size, double renderScaling, int zoomLevel, Point? center)
    {
        var pixelSize = DpiScaler.ScaleToPixel(size, renderScaling);
        var captureSize = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);

        return new RenderArgs(
            pixelSize,
            captureSize,
            center is { } c
                ? LoupeImageExtractor.ComputeCaptureTopLeft((int)c.X, (int)c.Y, captureSize)
                : null,
            (double)size * zoomLevel / pixelSize);
    }

    /// <summary>ルーペの表示情報（描画完了イベント引数用）。</summary>
    /// <param name="PixelSize">ルーペの一辺の物理ピクセルサイズ。</param>
    /// <param name="CaptureSize">切り出し領域の一辺（ドメインピクセル）。</param>
    /// <param name="CaptureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
    /// <param name="Scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    public readonly record struct RenderArgs(
        int PixelSize, int CaptureSize, Point? CaptureTopLeft, double Scale);
}

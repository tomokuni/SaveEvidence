using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.LineDetection.Tests;

/// <summary>LogicLineDetector が PbBitmap 入力を正しく検出できることを検証する。</summary>
/// <remarks>
/// 検出器（<see cref="ILineDetector.Detect"/>）は <see cref="PbBitmap"/> を受け取る。<br/>
/// 本テストは <c>[AvaloniaFact]</c>（ヘッドレス）で、純粋なピクセルパターン（人工画像）から<br/>
/// 矩形（ウィンドウの 4 辺）を検出できることを検証する。<br/>
/// ヘッドレスの <see cref="WriteableBitmap"/> は <c>Lock()</c> 書き込みが部分的にしか保持されないことがあるため、<br/>
/// 初期化は <see cref="PbBitmap"/>（SkiaSharp 内部）で行い、<c>[Fact]</c> 化してヘッドレス不要にした。
/// </remarks>
public sealed class LogicLineDetectorTests
{
    /// <summary>白背景に黒い枠線の矩形を描画した PbBitmap を構築する。</summary>
    /// <param name="width">画像の幅。</param>
    /// <param name="height">画像の高さ。</param>
    /// <param name="borderThickness">枠線の太さ（ピクセル）。</param>
    /// <returns>構築した PbBitmap（BGRA）。</returns>
    /// <remarks>
    /// 枠線は矩形の外周（上・下・左・右）に黒（BGR=0, A=255）で描画し、内部は白のままにする。<br/>
    /// 検出器の最小長（<c>MinLineLength=50</c>）を満たすよう、十分なサイズの矩形を描画する。
    /// </remarks>
    private static PbBitmap CreateRectImage(int width, int height, int borderThickness = 3)
    {
        var bmp = PbBitmap.Create(width, height);
        bmp.Clear(b: 255, g: 255, r: 255, a: 255); // 白

        // 外周に黒い枠線を描く
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isBorder = y < borderThickness || y >= height - borderThickness
                    || x < borderThickness || x >= width - borderThickness;
                if (isBorder)
                {
                    bmp.SetPixel(x, y, b: 0, g: 0, r: 0, a: 255);
                }
            }
        }
        return bmp;
    }

    /// <summary>白背景に黒い枠線の矩形を検出し、4 辺を復元できることを検証する。</summary>
    /// <remarks>
    /// 1. 200x200 の矩形画像を構築する<br/>
    /// 2. <see cref="LogicLineDetector.Detect"/> で線分を検出する<br/>
    /// 3. 垂直 2 本・水平 2 本（外側の境界）が検出されることを検証する
    /// </remarks>
    [Fact]
    public void Detect_白背景の矩形枠を検出する()
    {
        using var image = CreateRectImage(200, 200);
        var detector = new LogicLineDetector();
        var results = detector.Detect(image);

        Assert.NotEmpty(results);

        var verticalX = results
            .Where(r => r.Orientation == LineOrientation.Vertical && r.Score >= 50)
            .Select(r => (int)Math.Round(r.Start.X))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        var horizontalY = results
            .Where(r => r.Orientation == LineOrientation.Horizontal && r.Score >= 50)
            .Select(r => (int)Math.Round(r.Start.Y))
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        // 矩形の 4 辺（垂直 2 本・水平 2 本）が検出されることを検証する
        Assert.True(verticalX.Count >= 2, $"垂直線が 2 本以上検出されるべき（実際: {verticalX.Count}）");
        Assert.True(horizontalY.Count >= 2, $"水平線が 2 本以上検出されるべき（実際: {horizontalY.Count}）");
    }
}

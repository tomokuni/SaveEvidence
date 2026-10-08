using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Overlay;

/// <summary><see cref="OverlayRectRasterizer"/> の単体テスト（4 辺のラスタライズ・破線・太さ）。</summary>
/// <remarks>
/// <see cref="EsWritableBitmap"/> への書き込みのみで完結するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 座標変換（対象座標 → ルーペピクセル）・切り出し左上の差し引き・スケール適用・範囲外の早期リターンを検証する。
/// </remarks>
public class OverlayRectRasterizerTests
{
    /// <summary>テストで使う前景色（不透明）。</summary>
    private static readonly DrawingColor Foreground = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>テストで使う背景色（不透明）。</summary>
    private static readonly DrawingColor Background = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>指定座標が指定色かどうかを判定する（前景色用）。</summary>
    private static void AssertPixelIs(EsWritableBitmap bitmap, int x, int y, DrawingColor expected)
    {
        var (b, g, r, a) = bitmap.GetPixel(x, y);

        Assert.Equal(expected.R, r);
        Assert.Equal(expected.G, g);
        Assert.Equal(expected.B, b);
        Assert.Equal(expected.A, a);
    }

    /// <summary>指定座標が透明（未描画）かどうかを判定する。</summary>
    private static void AssertPixelIsTransparent(EsWritableBitmap bitmap, int x, int y)
    {
        var (_, _, _, a) = bitmap.GetPixel(x, y);

        Assert.Equal(0, a);
    }

    /// <summary>テスト用の矩形を生成する（角の丸めを避けた整数サイズ）。</summary>
    private static OverlayRect CreateRect(OverlayLineStyle style = OverlayLineStyle.Solid, double thickness = 1.0)
        => new(new EsRect(0, 0, 9, 9), style, Foreground, Background, thickness);

    // ====================================================================
    // 4 辺の描画
    // ====================================================================

    [Fact]
    internal void Draw_実線は4辺を描画し内側は残す()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);

        // 対象 (0,0)-(9,9)・スケール 1・切り出し左上 (0,0) → ルーペ (0,0)-(10,10) の枠
        OverlayRectRasterizer.Draw(bitmap, CreateRect(), new EsPoint(0, 0), 1.0);

        // 4 隅と各辺の中央が前景色
        AssertPixelIs(bitmap, 0, 0, Foreground);
        AssertPixelIs(bitmap, 10, 0, Foreground);
        AssertPixelIs(bitmap, 0, 10, Foreground);
        AssertPixelIs(bitmap, 10, 10, Foreground);
        AssertPixelIs(bitmap, 5, 0, Foreground);
        AssertPixelIs(bitmap, 5, 10, Foreground);
        AssertPixelIs(bitmap, 0, 5, Foreground);
        AssertPixelIs(bitmap, 10, 5, Foreground);

        // 内側は塗りつぶさない（枠線のみ）
        AssertPixelIsTransparent(bitmap, 5, 5);
    }

    [Fact]
    internal void Draw_切り出し左上を差し引いた位置へ描画する()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);
        var rect = new OverlayRect(new EsRect(5, 5, 9, 9), OverlayLineStyle.Solid, Foreground, Background, 1.0);

        // 対象 (5,5) が切り出し左上 (5,5) に対応するため、ルーペ (0,0) から描画される
        OverlayRectRasterizer.Draw(bitmap, rect, new EsPoint(5, 5), 1.0);

        AssertPixelIs(bitmap, 0, 0, Foreground);
        AssertPixelIs(bitmap, 10, 10, Foreground);
        AssertPixelIsTransparent(bitmap, 5, 5);
    }

    [Fact]
    internal void Draw_スケールを掛けたピクセル位置へ描画する()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);
        var rect = new OverlayRect(new EsRect(0, 0, 4, 4), OverlayLineStyle.Solid, Foreground, Background, 1.0);

        // スケール 2 → 幅・高さ (4+1)*2 = 10 ピクセル
        OverlayRectRasterizer.Draw(bitmap, rect, new EsPoint(0, 0), 2.0);

        AssertPixelIs(bitmap, 0, 0, Foreground);
        AssertPixelIs(bitmap, 10, 0, Foreground);
        AssertPixelIs(bitmap, 10, 10, Foreground);
        AssertPixelIsTransparent(bitmap, 5, 5);
    }

    [Fact]
    internal void Draw_描画範囲外の矩形は何も描画しない()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);
        var rect = new OverlayRect(new EsRect(100, 100, 9, 9), OverlayLineStyle.Solid, Foreground, Background, 1.0);

        OverlayRectRasterizer.Draw(bitmap, rect, new EsPoint(0, 0), 1.0);

        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                AssertPixelIsTransparent(bitmap, x, y);
            }
        }
    }

    [Fact]
    internal void Draw_矩形が描画範囲の右外へはみ出しても描画を開始する()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);
        var rect = new OverlayRect(new EsRect(15, 0, 9, 9), OverlayLineStyle.Solid, Foreground, Background, 1.0);

        // x=15（<= 20）から始まるため早期リターンせず、範囲内の部分（左辺）が描画される
        OverlayRectRasterizer.Draw(bitmap, rect, new EsPoint(0, 0), 1.0);

        AssertPixelIs(bitmap, 15, 5, Foreground);
    }

    // ====================================================================
    // 破線（前景色 / 背景色の交互）
    // ====================================================================

    [Fact]
    internal void Draw_破線はダッシュごとに前景色と背景色を交互に描画する()
    {
        using var bitmap = EsWritableBitmap.Create(40, 40);
        var rect = new OverlayRect(new EsRect(0, 0, 29, 29), OverlayLineStyle.Dashed, Foreground, Background, 1.0);

        // 上辺: ダッシュ長 8 / 間隔 4（OverlayLineDash）
        OverlayRectRasterizer.Draw(bitmap, rect, new EsPoint(0, 0), 1.0);

        // 1 本目（0〜8）は前景色、2 本目（12〜20）は背景色、3 本目（24〜30）は前景色
        AssertPixelIs(bitmap, 4, 0, Foreground);
        AssertPixelIs(bitmap, 15, 0, Background);
        AssertPixelIs(bitmap, 26, 0, Foreground);

        // ダッシュの間隔（9〜11、21〜23）は未描画
        AssertPixelIsTransparent(bitmap, 10, 0);
        AssertPixelIsTransparent(bitmap, 22, 0);
    }

    [Fact]
    internal void Draw_点線は破線より短いダッシュで描画する()
    {
        using var bitmap = EsWritableBitmap.Create(40, 40);
        var rect = new OverlayRect(new EsRect(0, 0, 29, 29), OverlayLineStyle.Dotted, Foreground, Background, 1.0);

        // 上辺: ダッシュ長 2 / 間隔 2 → 0〜2 前景、4〜6 背景、8〜10 前景
        OverlayRectRasterizer.Draw(bitmap, rect, new EsPoint(0, 0), 1.0);

        AssertPixelIs(bitmap, 1, 0, Foreground);
        AssertPixelIs(bitmap, 5, 0, Background);
        AssertPixelIs(bitmap, 9, 0, Foreground);
        AssertPixelIsTransparent(bitmap, 3, 0);
    }

    [Fact]
    internal void Draw_実線は背景色を使用しない()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);

        OverlayRectRasterizer.Draw(bitmap, CreateRect(), new EsPoint(0, 0), 1.0);

        // 4 辺すべてが前景色（背景色は破線の 2 色目専用）
        AssertPixelIs(bitmap, 5, 0, Foreground);
        AssertPixelIs(bitmap, 5, 10, Foreground);
        AssertPixelIs(bitmap, 0, 5, Foreground);
        AssertPixelIs(bitmap, 10, 5, Foreground);
    }

    // ====================================================================
    // 線の太さ
    // ====================================================================

    [Fact]
    internal void Draw_太さ3は中心の両側へ広げて描画する()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);

        OverlayRectRasterizer.Draw(bitmap, CreateRect(thickness: 3.0), new EsPoint(0, 0), 1.0);

        // 上辺（y=0）は y=-1〜1 へ広がる（y=-1 は範囲外のため無視される）
        AssertPixelIs(bitmap, 5, 0, Foreground);
        AssertPixelIs(bitmap, 5, 1, Foreground);

        // 左辺（x=0）は x=-1〜1 へ広がる
        AssertPixelIs(bitmap, 1, 5, Foreground);

        // 内側は太さの範囲外のため未描画
        AssertPixelIsTransparent(bitmap, 5, 5);
    }

    [Fact]
    internal void Draw_太さ0でも最低1ピクセルの線を描画する()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);

        OverlayRectRasterizer.Draw(bitmap, CreateRect(thickness: 0), new EsPoint(0, 0), 1.0);

        AssertPixelIs(bitmap, 5, 0, Foreground);
        AssertPixelIsTransparent(bitmap, 5, 1);
    }

    [Fact]
    internal void Draw_太さは四捨五入して整数化する()
    {
        using var bitmap = EsWritableBitmap.Create(20, 20);

        // 2.4 → 2（半幅 1）、上辺は y=0..1 に広がる
        OverlayRectRasterizer.Draw(bitmap, CreateRect(thickness: 2.4), new EsPoint(0, 0), 1.0);

        AssertPixelIs(bitmap, 5, 0, Foreground);
        AssertPixelIs(bitmap, 5, 1, Foreground);
        AssertPixelIsTransparent(bitmap, 5, 2);
    }
}

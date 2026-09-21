using Estable.Platform.Geometry;
using SkiaSharp;
using Xunit;
using MewUIColor = Aprillz.MewUI.Color;

namespace Estable.MewUI.Loupe.Tests;

/// <summary><see cref="LoupeSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証）。</summary>
/// <remarks>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 矩形計算は <c>LoupeControl.Core</c> が単一所有するため、本テストは描画結果（どの位置に何色が出るか）を検証する。
/// </remarks>
public class LoupeSkiaRendererTests
{
    /// <summary>市松模様が明暗のタイルで描画されることを検証する。</summary>
    [Fact]
    public void Render_市松模様を明暗のタイルで描画する()
    {
        const int size = 64;
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(
            target.Canvas, null, null, size, 4,
            null, 8, ToColor(SKColors.White), ToColor(SKColors.Black), null, 1);

        // タイル (0,0) は明色、タイル (1,0) は暗色（(col + row) が奇数）
        Assert.Equal(SKColors.White, target.GetPixel(4, 4));
        Assert.Equal(SKColors.Black, target.GetPixel(12, 4));
        // 行が変わると明暗が反転する
        Assert.Equal(SKColors.Black, target.GetPixel(4, 12));
        Assert.Equal(SKColors.White, target.GetPixel(12, 12));
    }

    /// <summary>市松模様のタイルサイズが 0 以下でも描画できることを検証する。</summary>
    [Fact]
    public void Render_タイルサイズが0以下でも描画できる()
    {
        const int size = 8;
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(
            target.Canvas, null, null, size, 4,
            null, 0, ToColor(SKColors.White), ToColor(SKColors.Black), null, 1);

        // タイルサイズは 1 に正規化される（隣接タイルが交互に並ぶ）
        Assert.Equal(SKColors.White, target.GetPixel(0, 0));
        Assert.Equal(SKColors.Black, target.GetPixel(1, 0));
    }

    /// <summary>ソース画像が中心座標を中心に拡大描画されることを検証する。</summary>
    [Fact]
    public void Render_ソース画像を中心座標を中心に拡大する()
    {
        // 101x101 の画像の中心画素だけを赤にして、拡大中心が正しく写るかを確かめる
        using var source = TestHelper.CreateBitmapWithCenter(101, SKColors.Blue, SKColors.Red);

        const int size = 64;
        using var target = TestHelper.CreateTarget(size);

        // pixelSize=64, zoom=4 → captureSize=16, topLeft=(50-8, 50-8)=(42,42)
        LoupeSkiaRenderer.Render(
            target.Canvas, source, new EsPoint(50, 50), size, 4,
            null, 8, null, null, null, 1);

        // ルーペ中央は切り出し領域の中央 = 元画像の中心画素（赤）になる
        Assert.Equal(SKColors.Red, target.GetPixel(size / 2, size / 2));
        // 中心から離れた位置は背景色（青）
        Assert.Equal(SKColors.Blue, target.GetPixel(2, 2));
    }

    /// <summary>ソースまたは中心が未指定では市松模様のみを描画することを検証する。</summary>
    /// <param name="withSource">ソースのみを指定する場合は true（中心を未指定にする）。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Render_ソースまたは中心が未指定なら市松模様のみ(bool withSource)
    {
        const int size = 32;
        using var source = TestHelper.CreateBitmap(64, SKColors.Red);
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(
            target.Canvas, withSource ? source : null, withSource ? null : new EsPoint(10, 10), size, 4,
            null, 8, ToColor(SKColors.White), ToColor(SKColors.Black), null, 1);

        // 画像が描かれず市松模様が残る（赤ではなく明暗のタイル）
        Assert.Equal(SKColors.White, target.GetPixel(4, 4));
        Assert.Equal(SKColors.Black, target.GetPixel(12, 4));
    }

    /// <summary>十字線が中央の縦横に描画されることを検証する。</summary>
    [Fact]
    public void Render_十字線を中央の縦横に描画する()
    {
        const int size = 64;
        using var target = TestHelper.CreateTarget(size);

        // pixelSize=64, zoom=4 → blocks=16, start=32（十字線は 4 ピクセル幅）
        LoupeSkiaRenderer.Render(
            target.Canvas, null, null, size, 4,
            ToColor(SKColors.Yellow), 8, null, null, null, 1);

        Assert.Equal(SKColors.Yellow, target.GetPixel(33, 5)); // 縦
        Assert.Equal(SKColors.Yellow, target.GetPixel(5, 33)); // 横
    }

    /// <summary>外枠が四辺に描画されることを検証する。</summary>
    [Fact]
    public void Render_外枠を四辺に描画する()
    {
        const int size = 64;
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(
            target.Canvas, null, null, size, 4,
            null, 8, null, null, ToColor(SKColors.Lime), 4);

        Assert.Equal(SKColors.Lime, target.GetPixel(20, 1));  // 上
        Assert.Equal(SKColors.Lime, target.GetPixel(20, 62)); // 下
        Assert.Equal(SKColors.Lime, target.GetPixel(1, 20));  // 左
        Assert.Equal(SKColors.Lime, target.GetPixel(62, 20)); // 右
        // 内側は外枠で塗られない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(20, 20));
    }

    /// <summary>すべての色が null でも例外なく描画することを検証する。</summary>
    [Fact]
    public void Render_色がすべてnullでも例外にしない()
    {
        const int size = 16;
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(target.Canvas, null, null, size, 4, null, 8, null, null, null, 2);

        // 何も描画しない（透明のまま）
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(1, 1));
    }

    /// <summary>切り出し領域が画像外へ出た場合の描画（透明パディングで市松模様が透ける）を検証する。</summary>
    /// <remarks>
    /// 画像の外へはみ出した部分は透明パディングとなり、下地の市松模様が見える。<br/>
    /// 画像は切り出し領域との位置関係を保って描画される（左上へ詰めない）。
    /// </remarks>
    [Fact]
    public void Render_切り出し領域が画像外なら透明パディングされる()
    {
        // 中心を画像の左上端 (0,0) に置くと、切り出し領域の左上が画像外（負）になる
        using var source = TestHelper.CreateBitmap(16, SKColors.Blue);

        const int size = 32;
        using var target = TestHelper.CreateTarget(size);

        // pixelSize=32, zoom=1 → captureSize=32、topLeft=(-16,-16)。ソース (0,0) は画像 (16,16) へ写る
        LoupeSkiaRenderer.Render(
            target.Canvas, source, new EsPoint(0, 0), size, 1,
            null, 8, ToColor(SKColors.White), ToColor(SKColors.Black), null, 1);

        // 画像外（座標 0〜15）は透明パディングとなり、下地の市松模様が見える
        Assert.Equal(SKColors.White, target.GetPixel(4, 4));
        // 画像は元の位置関係を保って描画される（座標 16〜31）
        Assert.Equal(SKColors.Blue, target.GetPixel(20, 20));
        Assert.Equal(SKColors.Blue, target.GetPixel(31, 31));
    }

    /// <summary>破棄済みのソースを渡しても例外にしないことを検証する。</summary>
    [Fact]
    public void Render_破棄済みソースでも例外にしない()
    {
        var source = TestHelper.CreateBitmap(32, SKColors.Blue);
        source.Dispose();

        const int size = 32;
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(
            target.Canvas, source, new EsPoint(16, 16), size, 4,
            null, 8, ToColor(SKColors.White), ToColor(SKColors.Black), null, 1);

        // 市松模様のみが残る
        Assert.Equal(SKColors.White, target.GetPixel(4, 4));
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Render_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => LoupeSkiaRenderer.Render(null!, null, null, 16, 4, null, 8, null, null, null, 1));
    }

    /// <summary>Skia の色を MewUI の色へ変換する（不透明度はアルファに統合）。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>MewUI の色。</returns>
    private static MewUIColor ToColor(SKColor color)
        => MewUIColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
}

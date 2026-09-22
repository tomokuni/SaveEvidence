using Estable.Loupe;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Loupe.Tests;

/// <summary><see cref="LoupeSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証）。</summary>
/// <remarks>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 矩形計算と描画可否（透明な色は描画しない）は <c>LoupeControl.Core</c> が単一所有するため、<br/>
/// 本テストは描画結果（どの位置に何色が出るか）を検証する。
/// </remarks>
public class LoupeSkiaRendererTests
{
    /// <summary>透明色（アルファ 0。描画しない指定）。</summary>
    private static readonly DrawingColor Transparent = DrawingColor.FromArgb(0, 0, 0, 0);

    /// <summary>テスト用の外観を生成する（座標ラベルは描かない。ラベルは専用テストが検証する）。</summary>
    /// <param name="cross">十字線の色。</param>
    /// <param name="checkerSize">市松模様のタイルサイズ（DIP）。</param>
    /// <param name="checkerA">市松模様の明色。</param>
    /// <param name="checkerB">市松模様の暗色。</param>
    /// <param name="frame">外枠の色。</param>
    /// <param name="frameWidth">外枠の太さ（DIP）。</param>
    /// <returns>テスト用の外観。</returns>
    private static LoupeAppearance CreateAppearance(
        DrawingColor cross, int checkerSize, DrawingColor checkerA, DrawingColor checkerB, DrawingColor frame, int frameWidth)
        => new()
        {
            CrossColor = cross,
            CheckerSize = checkerSize,
            CheckerColorA = checkerA,
            CheckerColorB = checkerB,
            FrameColor = frame,
            FrameWidth = frameWidth,
            // 座標ラベルは上端中央に描かれ、市松模様・拡大画像の画素検証と重なるため既定で無効にする
            ShowCoordLabel = false,
        };

    /// <summary>テスト用の描画入力を生成する。</summary>
    /// <param name="center">拡大中心（ドメイン座標）。未設定は null。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="appearance">装飾の外観。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest Request(
        EsPoint? center, int pixelSize, int zoomLevel, LoupeAppearance appearance, double scaling = 1.0)
        => new(center, pixelSize, zoomLevel, appearance, scaling);

    /// <summary>下地（ベース色）が最初に全面へ塗られることを検証する。</summary>
    /// <remarks>
    /// 市松模様の色は半透明のため、背後の要素を透かしたくない場合はホストが不透明色を指定する。<br/>
    /// 下地は市松模様・拡大画像より先に塗る（描画順は描画層が単一所有する）。
    /// </remarks>
    [Fact]
    public void Render_下地を最初に全面へ塗る()
    {
        const int size = 16;
        using var target = TestHelper.CreateTarget(size);

        // 市松模様・十字線・外枠は描かず、下地（不透明の赤）だけを指定する
        var appearance = CreateAppearance(Transparent, 8, Transparent, Transparent, Transparent, 1)
            with { BackgroundColor = ToColor(SKColors.Red) };

        LoupeSkiaRenderer.Render(target.Canvas, null, Request(null, size, 4, appearance));

        // 全体が下地色になる
        Assert.Equal(SKColors.Red, target.GetPixel(1, 1));
        Assert.Equal(SKColors.Red, target.GetPixel(size - 1, size - 1));
    }

    /// <summary>下地の上へ市松模様が重ねて描画されることを検証する。</summary>
    [Fact]
    public void Render_下地の上へ市松模様を描く()
    {
        const int size = 16;
        using var target = TestHelper.CreateTarget(size);

        // 下地（不透明の赤）+ 市松模様（不透明の白/黒）
        var appearance = CreateAppearance(Transparent, 8, ToColor(SKColors.White), ToColor(SKColors.Black), Transparent, 1)
            with { BackgroundColor = ToColor(SKColors.Red) };

        LoupeSkiaRenderer.Render(target.Canvas, null, Request(null, size, 4, appearance));

        // 市松模様が下地を覆う（下地は見えない）
        Assert.Equal(SKColors.White, target.GetPixel(4, 4));
        Assert.Equal(SKColors.Black, target.GetPixel(12, 4));
    }

    /// <summary>市松模様が明暗のタイルで描画されることを検証する。</summary>
    [Fact]
    public void Render_市松模様を明暗のタイルで描画する()
    {
        const int size = 64;
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(
            target.Canvas, null,
            Request(null, size, 4, CreateAppearance(Transparent, 8, ToColor(SKColors.White), ToColor(SKColors.Black), Transparent, 1)));

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
            target.Canvas, null,
            Request(null, size, 4, CreateAppearance(Transparent, 0, ToColor(SKColors.White), ToColor(SKColors.Black), Transparent, 1)));

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
            target.Canvas, source,
            Request(new EsPoint(50, 50), size, 4, CreateAppearance(Transparent, 8, Transparent, Transparent, Transparent, 1)));

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
            target.Canvas, withSource ? source : null,
            Request(withSource ? null : new EsPoint(10, 10), size, 4,
                CreateAppearance(Transparent, 8, ToColor(SKColors.White), ToColor(SKColors.Black), Transparent, 1)));

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
            target.Canvas, null,
            Request(null, size, 4, CreateAppearance(ToColor(SKColors.Yellow), 8, Transparent, Transparent, Transparent, 1)));

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
            target.Canvas, null,
            Request(null, size, 4, CreateAppearance(Transparent, 8, Transparent, Transparent, ToColor(SKColors.Lime), 4)));

        Assert.Equal(SKColors.Lime, target.GetPixel(20, 1));  // 上
        Assert.Equal(SKColors.Lime, target.GetPixel(20, 62)); // 下
        Assert.Equal(SKColors.Lime, target.GetPixel(1, 20));  // 左
        Assert.Equal(SKColors.Lime, target.GetPixel(62, 20)); // 右
        // 内側は外枠で塗られない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(20, 20));
    }

    /// <summary>アルファ 0 の色（透明）は描画しないことを検証する。</summary>
    /// <remarks>
    /// 描画の可否はコアの <see cref="LoupeAppearance.HasCrosshair"/> 等が判定する。<br/>
    /// 判定を無視して既定色で描画すると本テストが失敗する（見た目が変わる退行の検出）。
    /// </remarks>
    [Fact]
    public void Render_透明な十字線と外枠は描画しない()
    {
        const int size = 32;
        using var target = TestHelper.CreateTarget(size);

        // 市松模様は不透明の白（全面が白）・十字線と外枠は透明
        LoupeSkiaRenderer.Render(
            target.Canvas, null,
            Request(null, size, 4, CreateAppearance(Transparent, 8, ToColor(SKColors.White), ToColor(SKColors.White), Transparent, 4)));

        // 十字線（中央）と外枠（四辺）の位置も白のまま（透明な色は描画されない）
        Assert.Equal(SKColors.White, target.GetPixel(16, 5));
        Assert.Equal(SKColors.White, target.GetPixel(5, 16));
        Assert.Equal(SKColors.White, target.GetPixel(20, 1));
        Assert.Equal(SKColors.White, target.GetPixel(1, 20));
    }

    /// <summary>すべての色が透明なら何も描画しないことを検証する。</summary>
    [Fact]
    public void Render_色がすべて透明なら何も描画しない()
    {
        const int size = 16;
        using var target = TestHelper.CreateTarget(size);

        LoupeSkiaRenderer.Render(
            target.Canvas, null,
            Request(null, size, 4, CreateAppearance(Transparent, 8, Transparent, Transparent, Transparent, 2)));

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
            target.Canvas, source,
            Request(new EsPoint(0, 0), size, 1,
                CreateAppearance(Transparent, 8, ToColor(SKColors.White), ToColor(SKColors.Black), Transparent, 1)));

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
            target.Canvas, source,
            Request(new EsPoint(16, 16), size, 4,
                CreateAppearance(Transparent, 8, ToColor(SKColors.White), ToColor(SKColors.Black), Transparent, 1)));

        // 市松模様のみが残る
        Assert.Equal(SKColors.White, target.GetPixel(4, 4));
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Render_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => LoupeSkiaRenderer.Render(null!, null, Request(null, 16, 4, new LoupeAppearance())));
    }

    /// <summary>外観が null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Render_外観がnullなら例外()
    {
        const int size = 16;
        using var target = TestHelper.CreateTarget(size);

        Assert.Throws<ArgumentNullException>(
            () => LoupeSkiaRenderer.Render(target.Canvas, null, Request(null, size, 4, null!)));
    }

    /// <summary>Skia の色をコアの色へ変換する（不透明度はアルファに統合）。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>コアの色（<see cref="DrawingColor"/>）。</returns>
    private static DrawingColor ToColor(SKColor color)
        => DrawingColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
}

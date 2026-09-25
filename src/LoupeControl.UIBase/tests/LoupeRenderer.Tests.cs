using EsUtil.Feature.LoupeCore;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Loupe.UIBase.Tests;

/// <summary><see cref="LoupeRenderer"/> の描画テスト（描画ポートの画素を検証する）。</summary>
/// <remarks>
/// 描画の実行は UI 非依存の描画ポート（Skia）で行うため、UI 初期化とウィンドウなしで検証できる。<br/>
/// 本テストが「描画の手順」の唯一の検証点である（各 UI はアダプタのみを持つため、UI ごとの描画テストは不要）。<br/>
/// 色は描画の設定（<see cref="LoupePaint"/>）が単一所有するため、各テストが色を与えて描画実装を生成する。
/// </remarks>
public class LoupeRendererTests
{
    /// <summary>下地の色（赤・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>市松模様の明色（青・不透明）。</summary>
    private static readonly DrawingColor s_checkerA = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>市松模様の暗色（緑・不透明）。</summary>
    private static readonly DrawingColor s_checkerB = DrawingColor.FromArgb(255, 0, 128, 0);

    /// <summary>外枠の色（黄・不透明）。</summary>
    private static readonly DrawingColor s_frame = DrawingColor.FromArgb(255, 255, 255, 0);

    /// <summary>十字線の色（白・不透明）。</summary>
    private static readonly DrawingColor s_cross = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>下地の赤（画素比較用）。</summary>
    private static readonly SKColor s_backgroundPixel = new(255, 0, 0);

    /// <summary>描画されていない画素（透明）。</summary>
    private static readonly SKColor s_transparent = new(0, 0, 0, 0);

    /// <summary>ルーペの一辺（物理ピクセル）。</summary>
    private const int LoupeSize = 32;

    /// <summary>拡大中心（描画先の中央）。</summary>
    private static readonly EsPoint s_center = new(16, 16);

    // ====================================================================
    // 下地
    // ====================================================================

    [Fact]
    public void DrawShapes_下地で描画面を置き換える()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);
        // 前フレームの残像を模して別の色で塗っておく
        target.Canvas.FillRect(new EsRect(0, 0, LoupeSize, LoupeSize), s_checkerB);

        DrawShapes(target, CreatePaint(background: s_background), [LoupeDrawLayer.Background]);

        Assert.Equal(s_backgroundPixel, target.GetPixel(0, 0));
        Assert.Equal(s_backgroundPixel, target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    [Fact]
    public void DrawShapes_下地の層が無ければ描画面を変更しない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(target, CreatePaint(crosshair: s_cross), [LoupeDrawLayer.Crosshair]);

        // 十字線だけが描かれ、下地は塗られない
        Assert.Equal(s_transparent, target.GetPixel(1, 1));
    }

    // ====================================================================
    // 市松模様
    // ====================================================================

    [Fact]
    public void DrawShapes_市松模様を明色で塗り暗色タイルを重ねる()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(target, CreatePaint(checkerA: s_checkerA, checkerB: s_checkerB), [LoupeDrawLayer.Checkerboard]);

        // タイルサイズ 6（既定）× スケーリング 1.0。明暗は (col + row) の偶奇で決まる
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(0, 0));   // (0,0) 明色
        Assert.Equal(new SKColor(0, 128, 0), target.GetPixel(6, 0));   // (1,0) 暗色
        Assert.Equal(new SKColor(0, 128, 0), target.GetPixel(0, 6));   // (0,1) 暗色
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(6, 6));   // (1,1) 明色
    }

    [Fact]
    public void DrawShapes_市松模様の暗色が透明なら明色のみを塗る()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(checkerA: s_checkerA, checkerB: DrawingColor.Transparent),
            [LoupeDrawLayer.Checkerboard]);

        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(6, 0));
    }

    [Fact]
    public void DrawShapes_市松模様の明暗とも透明なら何も塗らない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(checkerA: DrawingColor.Transparent, checkerB: DrawingColor.Transparent),
            [LoupeDrawLayer.Checkerboard]);

        Assert.Equal(s_transparent, target.GetPixel(0, 0));
    }

    // ====================================================================
    // 拡大画像
    // ====================================================================

    [Fact]
    public void DrawShapes_拡大中心の周囲を切り出して拡大描画する()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_checkerA);

        DrawShapes(target, CreatePaint(background: s_background), [LoupeDrawLayer.Image], source);

        // 切り出した領域（一様な色）がルーペ全体へ拡大される
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(16, 16));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    [Fact]
    public void DrawShapes_ソースが無ければ拡大画像を描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(background: s_background),
            [LoupeDrawLayer.Background, LoupeDrawLayer.Image]);

        // 下地のみ（拡大画像は描かれない）
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    [Fact]
    public void DrawShapes_拡大中心が未設定なら拡大画像を描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_checkerA);

        // 中心を指定しない描画入力を組み立てる（コアは中心が無ければ切り出さない）
        var request = LoupeRenderRequest.Create(center: null, LoupeSize, 1.0, LoupeOptions.Default);

        new LoupeRenderer(CreatePaint(background: s_background)).DrawShapes(
            target.Canvas,
            source,
            request,
            new LoupeRenderNotification(null, 4, [LoupeDrawLayer.Background, LoupeDrawLayer.Image]));

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    // ====================================================================
    // 十字線・外枠
    // ====================================================================

    [Fact]
    public void DrawShapes_十字線を切り出し領域の中央へ描く()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(background: s_background, crosshair: s_cross),
            [LoupeDrawLayer.Background, LoupeDrawLayer.Crosshair]);

        // 拡大率 4・一辺 32 → ブロック数 8、交点は 8 / 2 × 4 = 16
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(17, 1));
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(1, 17));
        // 十字の外側は下地のまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(1, 1));
    }

    [Fact]
    public void DrawShapes_十字線が透明なら描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(background: s_background, crosshair: DrawingColor.Transparent),
            [LoupeDrawLayer.Background, LoupeDrawLayer.Crosshair]);

        Assert.Equal(s_backgroundPixel, target.GetPixel(17, 1));
    }

    [Fact]
    public void DrawShapes_外枠を四辺へ太さ分だけ描く()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(background: s_background, frame: s_frame),
            [LoupeDrawLayer.Background, LoupeDrawLayer.Frame]);

        // 上辺（太さ 2）
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(16, 1));
        // 左辺
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(1, 16));
        // 内側は下地のまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    [Fact]
    public void DrawShapes_外枠が透明なら描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(background: s_background, frame: DrawingColor.Transparent),
            [LoupeDrawLayer.Background, LoupeDrawLayer.Frame]);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 1));
    }

    // ====================================================================
    // ラベル（描画実装は描かない）
    // ====================================================================

    [Fact]
    public void DrawShapes_はラベル層を描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(background: s_background, coordLabel: s_frame),
            [LoupeDrawLayer.Background, LoupeDrawLayer.CoordLabel]);

        // ラベルは UI 要素が配置するため、描画ポートには何も現れない（下地のみ）
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 2));
    }

    // ====================================================================
    // 引数の検証
    // ====================================================================

    [Fact]
    public void DrawShapes_描画ポートがnullなら例外()
    {
        var renderer = new LoupeRenderer(CreatePaint());
        var request = CreateRequest();

        Assert.Throws<ArgumentNullException>(
            () => renderer.DrawShapes(null!, null, request, CreateNotification([])));
    }

    [Fact]
    public void DrawShapes_層の一覧がnullなら例外()
    {
        using var target = TestHelper.CreateTarget(4, 4);
        var renderer = new LoupeRenderer(CreatePaint());

        Assert.Throws<ArgumentNullException>(
            () => renderer.DrawShapes(
                target.Canvas, null, CreateRequest(), new LoupeRenderNotification(null, 4, null!)));
    }

    [Fact]
    public void 生成_色がnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => new LoupeRenderer(null!));

    [Fact]
    public void ResolveLabels_はLoupeLabelPlanへ委譲する()
    {
        var coordLabel = CreateCoordLabel();
        var paint = CreatePaint(coordLabel: s_frame);
        static EsSize Measure(string text, double fontSize) => new(text.Length * 6 + fontSize, 16);

        var viaRenderer = new LoupeRenderer(paint).ResolveLabels(coordLabel, Measure);
        var viaPlan = LoupeLabelPlan.Resolve(coordLabel, paint, Measure);

        Assert.Equal(viaPlan, viaRenderer);
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>図形の層を描く（テストの可読性のための短い呼び出し）。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="paint">描画に使う色。</param>
    /// <param name="layers">描く層。</param>
    /// <param name="source">拡大元の画像。</param>
    private static void DrawShapes(
        RenderTarget target, LoupePaint paint, IReadOnlyList<LoupeDrawLayer> layers, EsBitmap? source = null)
        => new LoupeRenderer(paint).DrawShapes(
            target.Canvas, source, CreateRequest(), CreateNotification(layers));

    /// <summary>テスト用の描画完了通知を生成する。</summary>
    /// <param name="layers">描く層。</param>
    /// <returns>描画完了通知。</returns>
    private static LoupeRenderNotification CreateNotification(IReadOnlyList<LoupeDrawLayer> layers)
        => new(s_center, 4, layers);

    /// <summary>テスト用の描画入力（一辺 32 DIP・スケーリング 1.0・拡大中心は描画先の中央）を生成する。</summary>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest CreateRequest()
        => LoupeRenderRequest.Create(s_center, LoupeSize, 1.0, LoupeOptions.Default);

    /// <summary>テスト用の色を生成する（指定しない色は透明＝描かない）。</summary>
    /// <param name="background">下地の色。</param>
    /// <param name="checkerA">市松模様の明色。</param>
    /// <param name="checkerB">市松模様の暗色。</param>
    /// <param name="frame">外枠の色。</param>
    /// <param name="crosshair">十字線の色。</param>
    /// <param name="coordLabel">中心座標ラベルの背景色。</param>
    /// <returns>描画に使う色。</returns>
    /// <remarks>外枠の太さは寸法（コアの <see cref="LoupeMetrics.FrameWidth"/>）が単一所有するため、ここでは与えない。</remarks>
    private static LoupePaint CreatePaint(
        DrawingColor? background = null,
        DrawingColor? checkerA = null,
        DrawingColor? checkerB = null,
        DrawingColor? frame = null,
        DrawingColor? crosshair = null,
        DrawingColor? coordLabel = null)
        => new()
        {
            BackgroundColor = background ?? DrawingColor.Transparent,
            CheckerColorA = checkerA ?? DrawingColor.Transparent,
            CheckerColorB = checkerB ?? DrawingColor.Transparent,
            FrameColor = frame ?? DrawingColor.Transparent,
            CrossColor = crosshair ?? DrawingColor.Transparent,
            CoordLabelBackgroundColor = coordLabel ?? DrawingColor.Transparent,
        };

    /// <summary>テスト用の中心座標ラベルを生成する。</summary>
    /// <returns>中心座標ラベル。</returns>
    private static LoupeCoordLabel CreateCoordLabel()
        => new("(50, 60)", LoupeSize, 12.0, 0.0);
}

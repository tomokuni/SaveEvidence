using LoupeFacade = EsUtil.Feature.LoupeCore.Loupe;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.SaveEvidence.Loupe.UIBase.Tests;

/// <summary><see cref="LoupeRenderer"/> の描画テスト（描画ポートの画素を検証する）。</summary>
/// <remarks>
/// 描画の実行は UI 非依存の描画ポート（Skia）で行うため、UI 初期化とウィンドウなしで検証できる。<br/>
/// 本テストが「描画の手順」の唯一の検証点である（各 UI はアダプタのみを持つため、UI ごとの描画テストは不要）。<br/>
/// 色は描画の設定（<see cref="LoupePaint"/>）が単一所有するため、各テストが色を与えて描画実装を生成する。
/// </remarks>
public class LoupeRendererTests
{
    /// <summary>ペーストボード（画像外）の色（赤・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>拡大画像に使う色（青・不透明）。</summary>
    private static readonly DrawingColor s_image = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>枠線の色（黄・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 255, 255, 0);

    /// <summary>十字線の色（白・不透明）。</summary>
    private static readonly DrawingColor s_cross = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>ペーストボードの赤（画素比較用）。</summary>
    private static readonly SKColor s_backgroundPixel = new(255, 0, 0);

    /// <summary>描画されていない画素（透明）。</summary>
    private static readonly SKColor s_transparent = new(0, 0, 0, 0);

    /// <summary>ルーペの一辺（物理ピクセル）。</summary>
    private const int LoupeSize = 32;

    /// <summary>拡大中心（描画先の中央）。</summary>
    private static readonly EsPoint s_center = new(16, 16);

    // ====================================================================
    // ペーストボード（画像外の色）
    // ====================================================================

    [Fact]
    public void DrawShapes_ペーストボードで描画面を置き換える()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);
        // 前フレームの残像を模して別の色で塗っておく
        target.Canvas.FillRect(new EsRect(0, 0, LoupeSize, LoupeSize), s_image);

        DrawShapes(target, CreatePaint(pasteboard: s_background), [LoupeDrawLayer.Pasteboard]);

        Assert.Equal(s_backgroundPixel, target.GetPixel(0, 0));
        Assert.Equal(s_backgroundPixel, target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    [Fact]
    public void DrawShapes_ペーストボードの層が無ければ描画面を変更しない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(target, CreatePaint(crosshair: s_cross), [LoupeDrawLayer.Crosshair]);

        // 十字線だけが描かれ、ペーストボードは塗られない
        Assert.Equal(s_transparent, target.GetPixel(1, 1));
    }

    // ====================================================================
    // 拡大画像
    // ====================================================================

    /// <summary>画像の範囲外（画像端の外）はペーストボードの色（無地）になることを検証する。</summary>
    /// <remarks>
    /// 画像外は「透明パディング」として切り出されるため、描画面全体を塗るペーストボードの色がそのまま見える
    /// （市松模様ではなく無地）。中心を画像の左外へ寄せ、切り出し領域の左端が画像の外へ出る状態を作る。
    /// </remarks>
    [Fact]
    public void DrawShapes_画像の外はペーストボードの色になる()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_image);

        // 中心 x=1・一辺 32・拡大率 4 → 切り出し 8 px の左上 x=-3（画像の外）→ 描画先の左側が画像外になる
        var request = LoupeRenderRequest.Create(new EsPoint(1, LoupeSize / 2), LoupeSize, 1.0, LoupeOptions.Default);
        var content = new LoupeFacade().ResolveContent(request);

        new LoupeRenderer(CreatePaint(pasteboard: s_background)).DrawShapes(target.Canvas, source, content);

        // 画像の外（左端）はペーストボードの色、画像の中（右端）は拡大画像の色
        Assert.Equal(s_backgroundPixel, target.GetPixel(0, LoupeSize / 2));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(LoupeSize - 1, LoupeSize / 2));
    }

    [Fact]
    public void DrawShapes_拡大中心の周囲を切り出して拡大描画する()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_image);

        DrawShapes(target, CreatePaint(pasteboard: s_background), [LoupeDrawLayer.Image], source);

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
            CreatePaint(pasteboard: s_background),
            [LoupeDrawLayer.Pasteboard, LoupeDrawLayer.Image]);

        // ペーストボードのみ（拡大画像は描かれない）
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    [Fact]
    public void DrawShapes_拡大中心が未設定なら拡大画像を描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_image);

        // 中心を指定しない描画内容（コアは中心が無ければ切り出し領域を解決しない）
        var content = new LoupeFacade().ResolveContent(
            LoupeRenderRequest.Create(center: null, LoupeSize, 1.0, LoupeOptions.Default))
            with { Layers = [LoupeDrawLayer.Pasteboard, LoupeDrawLayer.Image] };

        new LoupeRenderer(CreatePaint(pasteboard: s_background)).DrawShapes(target.Canvas, source, content);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    // ====================================================================
    // 十字線・枠線
    // ====================================================================

    [Fact]
    public void DrawShapes_十字線を切り出し領域の中央へ描く()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(pasteboard: s_background, crosshair: s_cross),
            [LoupeDrawLayer.Pasteboard, LoupeDrawLayer.Crosshair]);

        // 拡大率 4・一辺 32 → ブロック数 8、交点は 8 / 2 × 4 = 16
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(17, 1));
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(1, 17));
        // 十字の外側はペーストボードのまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(1, 1));
    }

    [Fact]
    public void DrawShapes_十字線の層が無ければ十字線を描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        // 層の一覧に十字線が含まれない場合、描画実装は十字線を描かない（描くかどうかはコアが決める）
        DrawShapes(
            target,
            CreatePaint(pasteboard: s_background, crosshair: s_cross),
            [LoupeDrawLayer.Pasteboard, LoupeDrawLayer.Border]);

        Assert.Equal(s_backgroundPixel, target.GetPixel(17, 1));
    }

    [Fact]
    public void DrawShapes_枠線を四辺へ太さ分だけ描く()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(pasteboard: s_background, border: s_border),
            [LoupeDrawLayer.Pasteboard, LoupeDrawLayer.Border]);

        // 上辺（太さ 2）
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(16, 1));
        // 左辺
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(1, 16));
        // 内側はペーストボードのまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    [Fact]
    public void DrawShapes_枠線の層が無ければ枠線を描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        // 層の一覧に枠線が含まれない場合、描画実装は枠線を描かない（描くかどうかはコアが決める）
        DrawShapes(
            target,
            CreatePaint(pasteboard: s_background, border: s_border),
            [LoupeDrawLayer.Pasteboard, LoupeDrawLayer.Crosshair]);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 1));
    }

    // ====================================================================
    // テキスト（描画実装は描かない）
    // ====================================================================

    [Fact]
    public void DrawShapes_はテキストを描かない()
    {
        using var target = TestHelper.CreateTarget(LoupeSize, LoupeSize);

        DrawShapes(
            target,
            CreatePaint(pasteboard: s_background, coordLabel: s_border),
            [LoupeDrawLayer.Pasteboard]);

        // テキストは UI 層が要素として置くか画像として描くかを決めるため、描画ポートには何も現れない（ペーストボードのみ）
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 2));
    }

    // ====================================================================
    // 引数の検証
    // ====================================================================

    [Fact]
    public void DrawShapes_描画ポートがnullなら例外()
    {
        var renderer = new LoupeRenderer(CreatePaint());

        Assert.Throws<ArgumentNullException>(
            () => renderer.DrawShapes(null!, null, CreateContent([])));
    }

    [Fact]
    public void DrawShapes_層の一覧がnullなら例外()
    {
        using var target = TestHelper.CreateTarget(4, 4);
        var renderer = new LoupeRenderer(CreatePaint());

        Assert.Throws<ArgumentNullException>(
            () => renderer.DrawShapes(
                target.Canvas, null, CreateContent(null!)));
    }

    [Fact]
    public void 生成_色がnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => new LoupeRenderer(null!));

    [Fact]
    public void フォントサイズと角丸は描画の設定から公開する()
    {
        var paint = new LoupePaint { CoordLabelFontSize = 20.0, CoordLabelCornerRadius = 4 };
        var renderer = new LoupeRenderer(paint);

        // コアは値を持たず、描画の契約を通して UI 層へ公開する
        Assert.Equal(20.0, renderer.CoordLabelFontSize);
        Assert.Equal(4.0, renderer.CoordLabelCornerRadius);
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
            target.Canvas, source, CreateContent(layers));

    /// <summary>テスト用の描画内容を生成する（層だけを差し替える）。</summary>
    /// <param name="layers">描く層。</param>
    /// <returns>描画内容。</returns>
    private static LoupeDrawContent CreateContent(IReadOnlyList<LoupeDrawLayer> layers)
        => new LoupeFacade().ResolveContent(CreateRequest()) with { Layers = layers };

    /// <summary>テスト用の描画入力（一辺 32 DIP・スケーリング 1.0・拡大中心は描画先の中央）を生成する。</summary>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest CreateRequest()
        => LoupeRenderRequest.Create(s_center, LoupeSize, 1.0, LoupeOptions.Default);

    /// <summary>テスト用の色を生成する（指定しない色は透明＝描かない）。</summary>
    /// <param name="pasteboard">ペーストボード（画像外）の色。</param>
    /// <param name="border">枠線の色。</param>
    /// <param name="crosshair">十字線の色。</param>
    /// <param name="coordLabel">中心座標ラベルの背景色。</param>
    /// <returns>描画に使う色。</returns>
    /// <remarks>枠線の太さは寸法（コアの <see cref="LoupeOptions.BorderThickness"/>）が単一所有するため、ここでは与えない。</remarks>
    private static LoupePaint CreatePaint(
        DrawingColor? pasteboard = null,
        DrawingColor? border = null,
        DrawingColor? crosshair = null,
        DrawingColor? coordLabel = null)
        => new()
        {
            PasteboardColor = pasteboard ?? DrawingColor.Transparent,
            BorderColor = border ?? DrawingColor.Transparent,
            CrossColor = crosshair ?? DrawingColor.Transparent,
            CoordLabelBackgroundColor = coordLabel ?? DrawingColor.Transparent,
        };
}

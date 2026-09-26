using EsUtil.Feature.MarqueeCore;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Decoration;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.MarqueeCore.Drawing;

namespace EsUtil.SaveEvidence.Marquee.UIBase.Tests;

/// <summary><see cref="MarqueeRenderer"/> の描画テスト（描画ポートの画素を検証する）。</summary>
/// <remarks>
/// 描画の実行は UI 非依存の描画ポート（Skia）で行うため、UI 初期化とウィンドウなしで検証できる。<br/>
/// 本テストが「描画の手順」の唯一の検証点である（各 UI はアダプタのみを持つため、UI ごとの描画テストは不要）。
/// </remarks>
public class MarqueeRendererTests
{
    /// <summary>ハンドルの塗り色（白・不透明）。</summary>
    private static readonly DrawingColor s_white = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>枠線色（赤・不透明）。</summary>
    private static readonly DrawingColor s_red = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>装飾の枠線色（青・不透明）。</summary>
    private static readonly DrawingColor s_blue = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>内側の塗り色（緑・不透明）。</summary>
    private static readonly DrawingColor s_green = DrawingColor.FromArgb(255, 0, 128, 0);

    /// <summary>塗られていない画素（透明）。</summary>
    private static readonly SKColor s_transparent = new(0, 0, 0, 0);

    /// <summary>検証対象（描画の手順を所有する基底実装）。色は本テストが与える。</summary>
    private static readonly MarqueeRenderer s_renderer = new(CreatePaint());

    /// <summary>空の装飾フレーム（描くものが無い）。</summary>
    private static OuterDecoFrame EmptyOuter => new([], []);

    /// <summary>空の内側装飾フレーム（描くものが無い）。</summary>
    private static InnerDecoFrame EmptyInner => new(null, []);

    // ====================================================================
    // ハンドル
    // ====================================================================

    [Fact]
    public void DrawShapes_8方向のハンドル中心を塗る()
    {
        using var target = TestHelper.CreateTarget(120, 120);
        var content = CreateContent(CreateHandles(new EsRect(20, 20, 60, 40)), MarqueeDrawLayer.Handles);

        s_renderer.DrawShapes(target.Canvas, content);

        // 各ハンドルの中心（塗りの内側）が白になる
        Assert.Equal(SKColors.White, target.GetPixel(20, 20)); // TopLeft
        Assert.Equal(SKColors.White, target.GetPixel(80, 20)); // TopRight
        Assert.Equal(SKColors.White, target.GetPixel(20, 60)); // BottomLeft
        Assert.Equal(SKColors.White, target.GetPixel(80, 60)); // BottomRight
        Assert.Equal(SKColors.White, target.GetPixel(50, 20)); // TopCenter
        Assert.Equal(SKColors.White, target.GetPixel(20, 40)); // MiddleLeft
        Assert.Equal(SKColors.White, target.GetPixel(80, 40)); // MiddleRight
        Assert.Equal(SKColors.White, target.GetPixel(50, 60)); // BottomCenter

        // 矩形の中心（ハンドルなし）は描画されない
        Assert.Equal(s_transparent, target.GetPixel(50, 40));
    }

    [Fact]
    public void DrawShapes_ハンドルの枠線を契約の太さで描く()
    {
        using var target = TestHelper.CreateTarget(120, 120);
        // TopLeft のハンドルは (45,45)-(55,55)。枠線は矩形の辺の上に中心を置く
        var content = CreateContent(CreateHandles(new EsRect(50, 50, 40, 40)), MarqueeDrawLayer.Handles);

        s_renderer.DrawShapes(target.Canvas, content);

        Assert.True(target.CountColor(40, 40, 60, 60, new SKColor(255, 0, 0, 255)) > 0, "枠線が描かれていない");
    }

    [Fact]
    public void DrawShapes_ハンドル非表示なら持たないため何も描かない()
    {
        using var target = TestHelper.CreateTarget(60, 60);
        // コアはハンドルを描かない場合 Handles を null にして層に含めない
        var content = CreateContent(handles: null, MarqueeDrawLayer.Decoration);

        s_renderer.DrawShapes(target.Canvas, content);

        Assert.Equal(s_transparent, target.GetPixel(30, 30));
    }

    // ====================================================================
    // 始点マーカー
    // ====================================================================

    [Fact]
    public void DrawShapes_始点マーカーの十字線を描く()
    {
        using var target = TestHelper.CreateTarget(80, 80);
        var marker = new MarqueeMarker(new EsPoint(40, 40), Thickness: 2, Size: 8);
        var content = CreateContent(handles: null, marker: marker, layer: MarqueeDrawLayer.Marker);

        s_renderer.DrawShapes(target.Canvas, content);

        // 水平線の左側（太さ 2 は中心の上下へ 1px ずつ）
        Assert.Equal(SKColors.White, target.GetPixel(35, 39));
        // 垂直線の上側
        Assert.Equal(SKColors.White, target.GetPixel(39, 35));
        // 十字の外側は描画されない
        Assert.Equal(s_transparent, target.GetPixel(30, 30));
        Assert.Equal(s_transparent, target.GetPixel(40, 60));
    }

    // ====================================================================
    // 装飾
    // ====================================================================

    [Fact]
    public void DrawShapes_外側装飾の暗転領域と枠線を描く()
    {
        using var target = TestHelper.CreateTarget(100, 60);
        var decoration = new OuterDecoFrame(
            [new EsRect(0, 0, 100, 10)],
            [new EsRect(10, 10, 80, 4)]);
        var content = CreateContent(decoration: decoration, layer: MarqueeDrawLayer.Decoration);

        s_renderer.DrawShapes(target.Canvas, content);

        // 暗転領域（上帯）
        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(50, 5));
        // 枠線（内側 4 辺の 1 本）
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(50, 11));
        // 暗転も枠線も無い位置
        Assert.Equal(s_transparent, target.GetPixel(50, 40));
    }

    [Fact]
    public void DrawShapes_内側装飾の塗りと枠線を描く()
    {
        using var target = TestHelper.CreateTarget(100, 60);
        var inner = new InnerDecoFrame(
            new EsRect(20, 20, 20, 20),
            [new EsRect(20, 20, 20, 2)]);
        var content = CreateContent(inner: inner, layer: MarqueeDrawLayer.Decoration);

        s_renderer.DrawShapes(target.Canvas, content);

        // 枠線は塗りの後に描かれる（同じ位置は枠線の色になる）
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(30, 20));
        // 塗りのみの位置
        Assert.Equal(new SKColor(0, 128, 0), target.GetPixel(30, 30));
    }

    [Fact]
    public void DrawShapes_描くものが無い層は何もしない()
    {
        using var target = TestHelper.CreateTarget(40, 40);
        var content = CreateContent(handles: null, layer: MarqueeDrawLayer.Handles);

        s_renderer.DrawShapes(target.Canvas, content);

        Assert.Equal(s_transparent, target.GetPixel(20, 20));
    }

    // ====================================================================
    // ラベル（描画実装は描かない）
    // ====================================================================

    [Fact]
    public void DrawShapes_はラベル層を描かない()
    {
        using var target = TestHelper.CreateTarget(120, 60);
        var content = CreateContent(
            sizeLabel: new MarqueeSizeLabel("W", new EsPoint(10, 10), 14.0),
            layer: MarqueeDrawLayer.SizeLabel);

        s_renderer.DrawShapes(target.Canvas, content);

        // ラベルは UI 要素が配置するため、描画ポートには何も現れない
        Assert.Equal(s_transparent, target.GetPixel(11, 11));
    }

    // ====================================================================
    // 引数の検証
    // ====================================================================

    [Fact]
    public void DrawShapes_描画ポートがnullなら例外()
    {
        var content = CreateContent(handles: null, layer: MarqueeDrawLayer.Handles);

        Assert.Throws<ArgumentNullException>(() => s_renderer.DrawShapes(null!, content));
    }

    [Fact]
    public void ResolveLabels_はMarqueeLabelPlanへ委譲する()
    {
        var content = CreateContent(
            sizeLabel: new MarqueeSizeLabel("W", new EsPoint(10, 10), 14.0),
            layer: MarqueeDrawLayer.SizeLabel);
        static EsSize Measure(string text, double fontSize) => new(text.Length * 6 + fontSize, 16);

        var viaRenderer = s_renderer.ResolveLabels(content, Measure);
        var viaPlan = MarqueeLabelPlan.Resolve(content, CreatePaint(), Measure);

        Assert.Equal(viaPlan, viaRenderer);
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>テスト用のハンドル描画内容を生成する（大きさ 10・枠線の太さ 1.5 の 8 方向）。</summary>
    /// <param name="rect">選択矩形（DIP）。</param>
    /// <returns>8 方向のハンドル矩形。</returns>
    /// <remarks>
    /// ハンドルの位置はコアが解決するため、本テストは期待値を独立に組み立てる（実装と同じ式を使わない）。
    /// </remarks>
    private static MarqueeHandles CreateHandles(EsRect rect)
    {
        const double size = 10.0;
        var half = size / 2;
        var midX = rect.X + rect.Width / 2;
        var midY = rect.Y + rect.Height / 2;

        EsRect At(double x, double y) => new(x - half, y - half, size, size);

        return new MarqueeHandles(
            [
                At(rect.X, rect.Y),           // 左上
                At(midX, rect.Y),             // 上中央
                At(rect.Right, rect.Y),       // 右上
                At(rect.X, midY),             // 左中央
                At(rect.Right, midY),         // 右中央
                At(rect.X, rect.Bottom),      // 左下
                At(midX, rect.Bottom),        // 下中央
                At(rect.Right, rect.Bottom),  // 右下
            ],
            BorderThickness: 1.5);
    }

    /// <summary>描画に使う色を生成する（本テストは色ごとの画素を検証する）。</summary>
    /// <returns>ハンドル白／枠線赤・マーカー白・暗転赤・穴あけ枠青・内側の塗り緑の色。</returns>
    private static MarqueePaint CreatePaint()
        => new()
        {
            HandleColor = s_white,
            HandleBorderColor = s_red,
            MarkerColor = s_white,
            OverlayColor = s_red,
            OuterBorderColor = s_blue,
            InnerFillColor = s_green,
            InnerBorderColor = s_blue,
        };

    /// <summary>テスト用の描画内容を生成する（対象の層だけを積む）。</summary>
    /// <param name="handles">ハンドル。</param>
    /// <param name="layer">積む層。</param>
    /// <param name="marker">始点マーカー。</param>
    /// <param name="decoration">外側の装飾。</param>
    /// <param name="inner">内側の装飾。</param>
    /// <param name="sizeLabel">サイズラベル。</param>
    /// <returns>描画内容。</returns>
    private static MarqueeDrawContent CreateContent(
        MarqueeHandles? handles = null,
        MarqueeDrawLayer layer = MarqueeDrawLayer.Decoration,
        MarqueeMarker? marker = null,
        OuterDecoFrame? decoration = null,
        InnerDecoFrame? inner = null,
        MarqueeSizeLabel? sizeLabel = null)
        => new(
            decoration ?? EmptyOuter,
            inner ?? EmptyInner,
            null,
            handles,
            marker,
            sizeLabel,
            null,
            [layer]);
}

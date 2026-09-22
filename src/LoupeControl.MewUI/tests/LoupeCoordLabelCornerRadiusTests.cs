using Estable.Loupe;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Loupe.Tests;

/// <summary>座標ラベルの描画（表示可否・角丸）のテスト。</summary>
/// <remarks>
/// ラベルの描画は描画層（<see cref="LoupeSkiaRenderer"/>）が担うため、ウィンドウを必要とせず画素で検証できる。<br/>
/// 背景矩形・余白・文字位置はコア（<c>LoupeLabelLayout</c>）が単一所有し、本テストは描画結果（塗られた範囲の形）を検証する。<br/>
/// 座標を固定値で書くと文字列の測定幅（フォント依存）に左右されるため、塗られた範囲の外接矩形を実測して判定する。
/// </remarks>
public class LoupeCoordLabelCornerRadiusTests
{
    /// <summary>描画先の一辺（物理ピクセル）。</summary>
    private const int CanvasSize = 64;

    /// <summary>角丸なし（半径 0）では外接矩形の四隅まで塗られる（四角形）ことを検証する。</summary>
    [Fact]
    public void 角丸なしなら四隅まで塗る()
    {
        using var target = TestHelper.CreateTarget(CanvasSize);

        DrawLabel(target, cornerRadius: 0);
        var bounds = FindPaintedBounds(target);

        Assert.True(bounds.Right >= 0, "座標ラベルが描画されていない");
        // 外接矩形の左上は塗られる（四角形）
        Assert.NotEqual(0, target.GetPixel(bounds.Left, bounds.Top).Alpha);
    }

    /// <summary>角丸ありでは外接矩形の四隅が塗られない（角が丸い）ことを検証する。</summary>
    [Fact]
    public void 角丸ありなら四隅を塗らない()
    {
        using var target = TestHelper.CreateTarget(CanvasSize);

        DrawLabel(target, cornerRadius: 6);
        var bounds = FindPaintedBounds(target);

        Assert.True(bounds.Right >= 0, "座標ラベルが描画されていない");
        // 外接矩形の左上は丸められて塗られない（角丸は四隅のみ）
        Assert.Equal(0, target.GetPixel(bounds.Left, bounds.Top).Alpha);
        // 左端の中央（角丸の影響を受けない位置）は塗られる
        Assert.NotEqual(0, target.GetPixel(bounds.Left, (bounds.Top + bounds.Bottom) / 2).Alpha);
    }

    /// <summary>中心が未設定のときは座標ラベルを描画しないことを検証する。</summary>
    [Fact]
    public void 中心未設定ならラベルを描画しない()
    {
        using var target = TestHelper.CreateTarget(CanvasSize);

        DrawLabel(target, cornerRadius: 0, hasCenter: false);

        // 何も塗られない（市松模様・拡大画像・装飾は透明指定）
        Assert.Equal(-1, FindPaintedBounds(target).Right);
    }

    /// <summary>指定の角丸で座標ラベルを描画する（市松模様・拡大画像・装飾は描かない）。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="cornerRadius">角丸半径（DIP）。</param>
    /// <param name="hasCenter">拡大中心を設定するかどうか（false の場合はラベルを描画しない）。</param>
    private static void DrawLabel(LoupeRenderTarget target, int cornerRadius, bool hasCenter = true)
    {
        var appearance = new LoupeAppearance
        {
            // 市松模様・十字線・外枠は描かない（ラベルの背景だけを検証する）
            CheckerColorA = DrawingColor.FromArgb(0, 0, 0, 0),
            CheckerColorB = DrawingColor.FromArgb(0, 0, 0, 0),
            CrossColor = DrawingColor.FromArgb(0, 0, 0, 0),
            FrameColor = DrawingColor.FromArgb(0, 0, 0, 0),
            // ラベル背景（不透明の白）
            CoordLabelColor = DrawingColor.White,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(255, 255, 255, 255),
            CoordLabelFontSize = 12,
            CoordLabelCornerRadius = cornerRadius,
        };

        LoupeSkiaRenderer.Render(
            target.Canvas, null,
            new LoupeRenderRequest(hasCenter ? new EsPoint(8, 8) : null, CanvasSize, 1, appearance, 1.0));
    }

    /// <summary>塗られた画素の外接矩形を求める（描画されていない場合は Right = -1）。</summary>
    /// <param name="target">描画先。</param>
    /// <returns>外接矩形。</returns>
    private static (int Left, int Top, int Right, int Bottom) FindPaintedBounds(LoupeRenderTarget target)
    {
        int left = CanvasSize, top = CanvasSize, right = -1, bottom = -1;

        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                if (target.GetPixel(x, y).Alpha == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return (left, top, right, bottom);
    }
}

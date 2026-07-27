using Avalonia;
using Estable.AvaloniaUI.Loupe.Rendering;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeDecorationRenderer"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 選択矩形の座標変換・交差判定のみを検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に<br/>
/// 依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// 描画そのものの検証は <see cref="LoupeRendererTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeDecorationRendererStateTests
{
    // ====================================================================
    // ComputeSelectionRectTransform（選択矩形 → ルーペ座標変換・交差判定）
    // ====================================================================

    [Fact]
    public void 変換_ドメイン座標をルーペ座標に変換し終点を閉区間化する()
    {
        // 選択矩形 (10,10,20,20)・切り出し左上 (0,0)・ズーム 4 → (40,40,84,84)
        var result = LoupeDecorationRenderer.ComputeSelectionRectTransform(
            new Rect(10, 10, 20, 20), new Point(0, 0), 4, 200);

        Assert.NotNull(result);
        Assert.Equal(new Rect(40, 40, 84, 84), result);
    }

    [Fact]
    public void 変換_切り出し左上を減算して変換する()
    {
        // 切り出し左上 (50,50) の場合、選択矩形 (60,60,10,10) は (10,10,44,44) に変換される（ズーム 4）
        var result = LoupeDecorationRenderer.ComputeSelectionRectTransform(
            new Rect(60, 60, 10, 10), new Point(50, 50), 4, 200);

        Assert.NotNull(result);
        Assert.Equal(new Rect(40, 40, 44, 44), result);
    }

    [Fact]
    public void 変換_ズーム1倍はnullを返す()
    {
        var result = LoupeDecorationRenderer.ComputeSelectionRectTransform(
            new Rect(10, 10, 20, 20), new Point(0, 0), 1, 200);

        Assert.Null(result);
    }

    [Fact]
    public void 変換_切り出し領域より左に完全に外れるとnullを返す()
    {
        // x + w = -40 < 0 → 交差なし
        var result = LoupeDecorationRenderer.ComputeSelectionRectTransform(
            new Rect(-30, 10, 20, 20), new Point(50, 0), 4, 200);

        Assert.Null(result);
    }

    [Fact]
    public void 変換_切り出し領域より右に完全に外れるとnullを返す()
    {
        // x = 250 > 200 → 交差なし
        var result = LoupeDecorationRenderer.ComputeSelectionRectTransform(
            new Rect(110, 10, 20, 20), new Point(50, 0), 4, 200);

        Assert.Null(result);
    }

    [Fact]
    public void 変換_切り出し領域と交差する場合は矩形を返す()
    {
        // 選択矩形 (20,10,10,10)・切り出し左上 (15,0)・ズーム 4 → (20,40,44,44)（領域内）
        var result = LoupeDecorationRenderer.ComputeSelectionRectTransform(
            new Rect(20, 10, 10, 10), new Point(15, 0), 4, 200);

        Assert.NotNull(result);
        Assert.Equal(new Rect(20, 40, 44, 44), result);
    }
}

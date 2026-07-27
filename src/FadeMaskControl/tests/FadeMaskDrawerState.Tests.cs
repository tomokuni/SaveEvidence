using Avalonia;
using Xunit;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskDrawer"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 穴あけマスク矩形・サイズラベルテキストの計算のみを検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に<br/>
/// 依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// 描画そのものの検証は <see cref="FadeMaskDrawerTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class FadeMaskDrawerStateTests
{
    // ====================================================================
    // GetMaskRects（穴あけマスクの 4 辺矩形計算）
    // ====================================================================

    [Fact]
    public void GetMaskRects_中央の選択矩形で4辺すべてを返す()
    {
        var client = new Rect(0, 0, 100, 100);
        var sel = new Rect(20, 30, 40, 40);

        var rects = FadeMaskDrawer.GetMaskRects(client, sel);

        Assert.Equal(4, rects.Count);
        // 上: (0,0) 幅100 高さ30
        Assert.Equal(new Rect(0, 0, 100, 30), rects[0]);
        // 下: (0,70) 幅100 高さ30
        Assert.Equal(new Rect(0, 70, 100, 30), rects[1]);
        // 左: (0,30) 幅20 高さ40
        Assert.Equal(new Rect(0, 30, 20, 40), rects[2]);
        // 右: (60,30) 幅40 高さ40
        Assert.Equal(new Rect(60, 30, 40, 40), rects[3]);
    }

    [Fact]
    public void GetMaskRects_上端に接する選択矩形は上辺を返さない()
    {
        var client = new Rect(0, 0, 100, 100);
        var sel = new Rect(20, 0, 40, 40);

        var rects = FadeMaskDrawer.GetMaskRects(client, sel);

        Assert.Equal(3, rects.Count);
        // 上辺（Top == 0）は生成されない。下・左・右の 3 辺のみ
        Assert.Equal(new Rect(0, 40, 100, 60), rects[0]); // 下
        Assert.Equal(new Rect(0, 0, 20, 40), rects[1]); // 左
        Assert.Equal(new Rect(60, 0, 40, 40), rects[2]); // 右
    }

    [Fact]
    public void GetMaskRects_全面を覆う選択矩形は辺を返さない()
    {
        var client = new Rect(0, 0, 100, 100);
        var sel = new Rect(0, 0, 100, 100);

        var rects = FadeMaskDrawer.GetMaskRects(client, sel);

        Assert.Empty(rects);
    }

    [Fact]
    public void GetMaskRects_右端と下端に接する選択矩形は右辺と下辺を返さない()
    {
        var client = new Rect(0, 0, 100, 100);
        var sel = new Rect(50, 50, 50, 50);

        var rects = FadeMaskDrawer.GetMaskRects(client, sel);

        // 上・左の 2 辺のみ（右端・下端に接するため右辺・下辺は生成されない）
        Assert.Equal(2, rects.Count);
        Assert.Equal(new Rect(0, 0, 100, 50), rects[0]); // 上
        Assert.Equal(new Rect(0, 50, 50, 50), rects[1]); // 左
    }

    // ====================================================================
    // FormatSizeLabel（サイズラベルのテキスト生成）
    // ====================================================================

    [Fact]
    public void FormatSizeLabel_スケーリング1でDIP座標をそのまま表示する()
    {
        var text = FadeMaskDrawer.FormatSizeLabel(new Rect(10, 20, 30, 40), 1.0);

        Assert.Equal("(10, 20) - (40, 60),  (30×40), DisplayScale: (100%)", text);
    }

    [Fact]
    public void FormatSizeLabel_スケーリング1ポイント25で物理ピクセルに変換する()
    {
        var text = FadeMaskDrawer.FormatSizeLabel(new Rect(10, 20, 30, 40), 1.25);

        // 10*1.25=12, 20*1.25=25, 40*1.25=50, 60*1.25=75, 30*1.25=37, 40*1.25=50
        Assert.Equal("(12, 25) - (50, 75),  (37×50), DisplayScale: (125%)", text);
    }

    [Fact]
    public void FormatSizeLabel_スケーリング0以下は1として扱う()
    {
        var text = FadeMaskDrawer.FormatSizeLabel(new Rect(10, 20, 30, 40), 0);

        Assert.Equal("(10, 20) - (40, 60),  (30×40), DisplayScale: (100%)", text);
    }

    [Fact]
    public void FormatSizeLabel_小数座標は切り捨てる()
    {
        // Rect(10.9, 20.9, 30.9, 40.9): Right = 41.8 → 41, Bottom = 61.8 → 61
        var text = FadeMaskDrawer.FormatSizeLabel(new Rect(10.9, 20.9, 30.9, 40.9), 1.0);

        Assert.Equal("(10, 20) - (41, 61),  (30×40), DisplayScale: (100%)", text);
    }
}

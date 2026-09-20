using Avalonia;
using Estable.AvaloniaUI.DragRect.Drawing;
using Xunit;

namespace Estable.AvaloniaUI.DragRect.Tests;

/// <summary><see cref="DragRectDrawer"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// カーソル座標ラベルの配置計算のみを検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に<br/>
/// 依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// 描画そのものの検証は <see cref="DragRectControlTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class DragRectDrawerStateTests
{
    // ====================================================================
    // ComputeCoordLabelPosition（カーソル座標ラベル配置・クランプ）
    // ====================================================================

    [Fact]
    public void 配置_カーソル右側のオフセット位置に配置する()
    {
        var pos = DragRectDrawer.ComputeCoordLabelPosition(
            new Point(100, 50), new Size(800, 600), 120, 24);

        // カーソル (100,50) + オフセット (8,8)
        Assert.Equal(new Point(108, 58), pos);
    }

    [Fact]
    public void 配置_右端をはみ出す場合は領域内へクランプする()
    {
        var pos = DragRectDrawer.ComputeCoordLabelPosition(
            new Point(790, 50), new Size(800, 600), 120, 24);

        // x = 798 → クランプ: 800 - 120 = 680
        Assert.Equal(680, pos.X);
        Assert.Equal(58, pos.Y);
    }

    [Fact]
    public void 配置_下端をはみ出す場合は領域内へクランプする()
    {
        var pos = DragRectDrawer.ComputeCoordLabelPosition(
            new Point(100, 590), new Size(800, 600), 120, 24);

        // y = 598 → クランプ: 600 - 24 = 576
        Assert.Equal(108, pos.X);
        Assert.Equal(576, pos.Y);
    }

    [Fact]
    public void 配置_負座標でも0未満にならない()
    {
        var pos = DragRectDrawer.ComputeCoordLabelPosition(
            new Point(0, 0), new Size(800, 600), 120, 24);

        // x = 8, y = 8（負にならない）
        Assert.Equal(8, pos.X);
        Assert.Equal(8, pos.Y);
    }

    [Fact]
    public void 配置_クライアントサイズ0ではクランプしない()
    {
        var pos = DragRectDrawer.ComputeCoordLabelPosition(
            new Point(100, 50), new Size(0, 0), 120, 24);

        // サイズ 0 の場合はクランプ条件をスキップし、オフセットのみ
        Assert.Equal(108, pos.X);
        Assert.Equal(58, pos.Y);
    }

    [Fact]
    public void 配置_ラベルが領域より大きい場合は0にクランプする()
    {
        var pos = DragRectDrawer.ComputeCoordLabelPosition(
            new Point(100, 50), new Size(50, 50), 120, 24);

        // ラベル幅 120 > 領域幅 50 → x = 0
        Assert.Equal(0, pos.X);
    }
}

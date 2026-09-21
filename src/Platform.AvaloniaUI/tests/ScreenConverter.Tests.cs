using Avalonia;
using Avalonia.Controls;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using Avalonia.Headless.XUnit;

namespace EsUtil.Platform.AvaloniaUI.Tests;

/// <summary><see cref="ScreenConverter"/> の単体テスト（画面物理座標変換ポート）。</summary>
/// <remarks>
/// <see cref="Window"/>（<c>TopLevel</c> 接続）を使うテストは <c>[AvaloniaFact]</c> で直列実行する。<br/>
/// <see cref="Panel"/> の未接続（<c>TopLevel</c> 非接続）と純粋な座標変換のみのテストは <c>[Fact]</c> で並列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ScreenConverterTests
{
    // ====================================================================
    // ScreenToLocalDip（画面物理 → コントロールローカル DIP）
    // ====================================================================

    [AvaloniaFact]
    public void ScreenToLocalDip_ウィンドウ内のコントロールで逆変換できる()
    {
        var window = new Window { Width = 300, Height = 300 };
        var panel = new Panel { Width = 200, Height = 200 };
        window.Content = panel;
        window.Show();

        // ローカル (30, 40) → 画面物理 → ローカル DIP の往復（ヘッドレスはスケーリング 1.0・原点 (0,0)）
        var screen = ScreenConverter.LocalDipToScreen(panel, new Point(30, 40));
        var local = ScreenConverter.ScreenToLocalDip(panel, screen);

        Assert.Equal(30, local.X, 6);
        Assert.Equal(40, local.Y, 6);
        window.Close();
    }

    // ====================================================================
    // CreateVisualDipPort（コントロールローカル DIP ↔ 画面物理）
    // ====================================================================

    [AvaloniaFact]
    public void CreateVisualDipPort_往復で元の座標に戻る()
    {
        var window = new Window { Width = 300, Height = 300 };
        var panel = new Panel { Width = 200, Height = 200 };
        window.Content = panel;
        window.Show();

        var port = ScreenConverter.CreateVisualDipPort(panel);
        // Inverse = ローカル DIP → 画面物理、Forward = 画面物理 → ローカル DIP
        var roundTrip = port.Forward(port.Inverse(new EsPoint(30, 40)));

        Assert.Equal(30, roundTrip.X, 6);
        Assert.Equal(40, roundTrip.Y, 6);
        window.Close();
    }
}

/// <summary><see cref="ScreenConverter"/> のビジュアルツリー未接続テスト（<see cref="Control"/> 未接続・並列実行）。</summary>
/// <remarks>
/// <see cref="Panel"/> を生成するが <see cref="TopLevel"/> に接続しないため、<c>TopLevel.GetTopLevel</c> は null を返し等価変換される。<br/>
/// <c>Controls</c> を <c>Window</c> に表示しないことから UI スレッド（Dispatcher）を必要とせず <c>[Fact]</c> で並列実行できる。<br/>
/// <c>Window</c> に表示するテスト（<see cref="ScreenConverterTests"/>）はヘッドレス直列で実行する。
/// </remarks>
public class ScreenConverterVisualPortTests
{
    [Fact]
    public void ScreenToLocalDip_ビジュアルツリー未接続時は等価変換する()
    {
        var control = new Panel();

        var p = ScreenConverter.ScreenToLocalDip(control, new PixelPoint(10, 20));

        Assert.Equal(new Point(10, 20), p);
    }

    [Fact]
    public void CreateVisualDipPort_ビジュアルツリー未接続時は等価変換する()
    {
        var control = new Panel();
        var port = ScreenConverter.CreateVisualDipPort(control);

        Assert.Equal(new EsPoint(10, 20), port.Forward(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(10, 20), port.Inverse(new EsPoint(10, 20)));
    }
}

/// <summary><see cref="ScreenConverter.CreateDipPort"/> の純粋座標変換の単体テスト。</summary>
/// <remarks>
/// <see cref="Control"/> を使用せず、<c>PixelPoint</c> / <c>Point</c> のみの純粋演算のため<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class ScreenConverterDipPortTests
{
    // ====================================================================
    // CreateDipPort（ローカル DIP ↔ 画面物理）
    // ====================================================================

    [Fact]
    public void CreateDipPort_逆変換でDIPを画面物理に変換する()
    {
        // 原点 (100, 50)、スケーリング 1.25
        var port = ScreenConverter.CreateDipPort(new PixelPoint(100, 50), 1.25);

        // DIP (200, 100) → 物理 (350, 175)
        var p = port.Inverse(new EsPoint(200, 100));
        Assert.Equal(350, p.X, 6);
        Assert.Equal(175, p.Y, 6);
    }

    [Fact]
    public void CreateDipPort_順変換で画面物理をDIPに変換する()
    {
        var port = ScreenConverter.CreateDipPort(new PixelPoint(100, 50), 1.25);

        // 物理 (350, 175) → DIP (200, 100)
        var p = port.Forward(new EsPoint(350, 175));
        Assert.Equal(200, p.X, 6);
        Assert.Equal(100, p.Y, 6);
    }

    [Fact]
    public void CreateDipPort_往復で元の座標に戻る()
    {
        var port = ScreenConverter.CreateDipPort(new PixelPoint(37, 88), 1.5);
        var dip = new EsPoint(123.4, 456.7);
        var roundTrip = port.Forward(port.Inverse(dip));
        Assert.Equal(dip.X, roundTrip.X, 6);
        Assert.Equal(dip.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void CreateDipPort_スケーリング0以下は等倍として扱う()
    {
        var port = ScreenConverter.CreateDipPort(new PixelPoint(10, 20), 0);

        // スケール 1.0 として変換（物理 = DIP + 原点）
        var p = port.Inverse(new EsPoint(100, 200));
        Assert.Equal(110, p.X, 6);
        Assert.Equal(220, p.Y, 6);
    }
}

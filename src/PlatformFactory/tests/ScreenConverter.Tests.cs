using Avalonia;
using Avalonia.Controls;
using Estable;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="ScreenConverter"/> の単体テスト（画面物理座標変換ポート）。</summary>
[Collection("AvaloniaHeadless")]
public class ScreenConverterTests
{
    // ====================================================================
    // CreateDipPort（ローカル DIP ↔ 画面物理）
    // ====================================================================

    [AvaloniaFact]
    public void CreateDipPort_逆変換でDIPを画面物理に変換する()
    {
        // 原点 (100, 50)、スケーリング 1.25
        var port = ScreenConverter.CreateDipPort(new PixelPoint(100, 50), 1.25);

        // DIP (200, 100) → 物理 (350, 175)
        var p = port.Inverse(new PointD(200, 100));
        Assert.Equal(350, p.X, 6);
        Assert.Equal(175, p.Y, 6);
    }

    [AvaloniaFact]
    public void CreateDipPort_順変換で画面物理をDIPに変換する()
    {
        var port = ScreenConverter.CreateDipPort(new PixelPoint(100, 50), 1.25);

        // 物理 (350, 175) → DIP (200, 100)
        var p = port.Forward(new PointD(350, 175));
        Assert.Equal(200, p.X, 6);
        Assert.Equal(100, p.Y, 6);
    }

    [AvaloniaFact]
    public void CreateDipPort_往復で元の座標に戻る()
    {
        var port = ScreenConverter.CreateDipPort(new PixelPoint(37, 88), 1.5);
        var dip = new PointD(123.4, 456.7);
        var roundTrip = port.Forward(port.Inverse(dip));
        Assert.Equal(dip.X, roundTrip.X, 6);
        Assert.Equal(dip.Y, roundTrip.Y, 6);
    }

    [AvaloniaFact]
    public void CreateDipPort_スケーリング0以下は等倍として扱う()
    {
        var port = ScreenConverter.CreateDipPort(new PixelPoint(10, 20), 0);

        // スケール 1.0 として変換（物理 = DIP + 原点）
        var p = port.Inverse(new PointD(100, 200));
        Assert.Equal(110, p.X, 6);
        Assert.Equal(220, p.Y, 6);
    }

    // ====================================================================
    // ScreenToLocalDip（画面物理 → コントロールローカル DIP）
    // ====================================================================

    [AvaloniaFact]
    public void ScreenToLocalDip_ビジュアルツリー未接続時は等価変換する()
    {
        var control = new Panel();

        var p = ScreenConverter.ScreenToLocalDip(control, new PixelPoint(10, 20));

        Assert.Equal(new PointD(10, 20), p);
    }

    [AvaloniaFact]
    public void ScreenToLocalDip_ウィンドウ内のコントロールで逆変換できる()
    {
        var window = new Window { Width = 300, Height = 300 };
        var panel = new Panel { Width = 200, Height = 200 };
        window.Content = panel;
        window.Show();

        // ローカル (30, 40) → 画面物理 → ローカル DIP の往復（ヘッドレスはスケーリング 1.0・原点 (0,0)）
        var screen = ScreenConverter.LocalDipToScreen(panel, new PointD(30, 40));
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
        var roundTrip = port.Forward(port.Inverse(new PointD(30, 40)));

        Assert.Equal(30, roundTrip.X, 6);
        Assert.Equal(40, roundTrip.Y, 6);
        window.Close();
    }

    [AvaloniaFact]
    public void CreateVisualDipPort_ビジュアルツリー未接続時は等価変換する()
    {
        var control = new Panel();
        var port = ScreenConverter.CreateVisualDipPort(control);

        Assert.Equal(new PointD(10, 20), port.Forward(new PointD(10, 20)));
        Assert.Equal(new PointD(10, 20), port.Inverse(new PointD(10, 20)));
    }
}

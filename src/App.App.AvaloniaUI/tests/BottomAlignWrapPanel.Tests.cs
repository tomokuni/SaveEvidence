using EsUtil.SaveEvidence.App.AvaloniaUI.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Tests;

/// <summary>
/// <see cref="BottomAlignWrapPanel"/> の委譲確認テスト（Control の Measure / Arrange が
/// <see cref="BottomAlignWrapLayout"/> の純粋計算へ正しく委譲することを検証）。
/// </summary>
/// <remarks>
/// 折り返し・行高さ・下端揃えの計算ロジック自体は <see cref="BottomAlignWrapLayoutTests"/>（<c>[Fact]</c>・並列）で検証する。<br/>
/// 本テストは <see cref="Control"/> のレイアウトエンジン経由で委譲が機能することを確認するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）で直列実行する（最小限）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class BottomAlignWrapPanelTests
{
    /// <summary>指定サイズの子要素（Border）を生成する。</summary>
    /// <param name="w">幅。</param>
    /// <param name="h">高さ。</param>
    /// <returns>生成した Border。</returns>
    private static Border CreateChild(double w, double h) => new() { Width = w, Height = h };

    /// <summary>子要素を追加し、指定幅で Measure / Arrange したパネルを生成する。</summary>
    /// <param name="width">配置幅。</param>
    /// <param name="children">追加する子要素。</param>
    /// <returns>配置済みのパネル。</returns>
    private static BottomAlignWrapPanel Layout(double width, params Control[] children)
    {
        var panel = new BottomAlignWrapPanel();
        foreach (var child in children) panel.Children.Add(child);
        panel.Measure(new Size(width, double.PositiveInfinity));
        // Arrange は有限の高さが必要（無限は InvalidOperationException）
        panel.Arrange(new Rect(0, 0, width, 10000));
        return panel;
    }

    /// <summary>Measure が <see cref="BottomAlignWrapLayout"/> の計算結果を子要素の計測から委譲することを確認する。</summary>
    [AvaloniaFact]
    public void Measure_折り返しと行高さの計算が委譲される()
    {
        // 4 アイテムを幅 120 で折り返す（1 行目 100 + 2 行目 80 = 180）
        var panel = Layout(120, CreateChild(60, 100), CreateChild(60, 50), CreateChild(60, 80), CreateChild(60, 40));

        Assert.Equal(180.0, panel.DesiredSize.Height);
        Assert.Equal(120.0, panel.DesiredSize.Width);
    }

    /// <summary>Arrange が <see cref="BottomAlignWrapLayout"/> の計算結果を子要素へ委譲することを確認する。</summary>
    [AvaloniaFact]
    public void Arrange_下端揃えの配置が委譲される()
    {
        var tall = CreateChild(60, 100);
        var short1 = CreateChild(60, 50);
        var short2 = CreateChild(60, 80);
        var panel = Layout(1000, tall, short1, short2);

        // 行の高さ = 100。 各アイテムの下端（Y + Height）が行の下端（100）で揃う
        Assert.Equal(0.0, tall.Bounds.Y);
        Assert.Equal(50.0, short1.Bounds.Y);  // 100 - 50
        Assert.Equal(20.0, short2.Bounds.Y);  // 100 - 80
        Assert.Equal(100.0, tall.Bounds.Bottom);
        Assert.Equal(100.0, short1.Bounds.Bottom);
        Assert.Equal(100.0, short2.Bounds.Bottom);
    }
}


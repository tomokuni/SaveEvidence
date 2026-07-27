using app.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace app.Tests;

/// <summary>
/// BottomAlignWrapPanel（行内下端揃えの折り返しパネル）の単体テスト。
/// </summary>
/// <remarks>
/// 行の高さが「各行の最大アイテム高さ」で決まることと、<br/>
/// 行内でアイテムの下端が揃うこと（ファイル名 1 文字目の高さ位置が揃う根拠）を検証する。<br/>
/// 折り返し（幅超過・単独行・各行の独立した高さ）も検証する。
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

    [AvaloniaFact]
    public void Measure_単一の行では行の高さが最大アイテム高さになる()
    {
        var panel = Layout(1000, CreateChild(60, 100), CreateChild(60, 50), CreateChild(60, 80));

        // 幅 = アイテム幅の合計、高さ = 最大アイテム高さ（100）
        Assert.Equal(180.0, panel.DesiredSize.Width);
        Assert.Equal(100.0, panel.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void Arrange_行内でアイテムが下端揃えされる()
    {
        var tall = CreateChild(60, 100);
        var short1 = CreateChild(60, 50);
        var short2 = CreateChild(60, 80);
        var panel = Layout(1000, tall, short1, short2);

        // 行の高さ = 100。各アイテムの下端（Y + Height）が行の下端（100）で揃う
        Assert.Equal(0.0, tall.Bounds.Y);
        Assert.Equal(50.0, short1.Bounds.Y);  // 100 - 50
        Assert.Equal(20.0, short2.Bounds.Y);  // 100 - 80
        Assert.Equal(100.0, tall.Bounds.Bottom);
        Assert.Equal(100.0, short1.Bounds.Bottom);
        Assert.Equal(100.0, short2.Bounds.Bottom);
    }

    [AvaloniaFact]
    public void Arrange_幅超過で次の行に折り返される()
    {
        var c0 = CreateChild(60, 100);
        var c1 = CreateChild(60, 50);
        var c2 = CreateChild(60, 80);
        var c3 = CreateChild(60, 40);
        var panel = Layout(120, c0, c1, c2, c3);

        // 1 行目: c0, c1（高さ 100）→ 2 行目の先頭 Y = 100
        Assert.Equal(0.0, c0.Bounds.Y);
        Assert.Equal(50.0, c1.Bounds.Y);  // 100 - 50（下端揃え）
        Assert.Equal(100.0, c2.Bounds.Y);
        Assert.Equal(140.0, c3.Bounds.Y); // 100 + 80 - 40（2 行目の高さ 80 で下端揃え）

        // 各行の高さが独立して決まる（1 行目 100 + 2 行目 80 = 180）
        Assert.Equal(180.0, panel.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void Measure_複数行では高さが各行の高さの合計になる()
    {
        var panel = Layout(120, CreateChild(60, 100), CreateChild(60, 50), CreateChild(60, 80), CreateChild(60, 40));

        // 1 行目（高さ 100）+ 2 行目（高さ 80）= 180
        Assert.Equal(180.0, panel.DesiredSize.Height);
        Assert.Equal(120.0, panel.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void Arrange_アイテムが幅を超える場合は単独行に置かれる()
    {
        var wide = CreateChild(150, 100);
        var next = CreateChild(60, 50);
        var panel = Layout(100, wide, next);

        // 幅 150 のアイテムは幅 100 の行に収まらず単独行。次のアイテムは下の行に置かれる
        Assert.Equal(0.0, wide.Bounds.Y);
        Assert.Equal(100.0, next.Bounds.Y); // wide の高さ（100）の下の行
    }

    [AvaloniaFact]
    public void Measure_利用可能幅が無限の場合は折り返さない()
    {
        var panel = new BottomAlignWrapPanel();
        var c0 = CreateChild(60, 100);
        var c1 = CreateChild(60, 50);
        panel.Children.Add(c0);
        panel.Children.Add(c1);

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        // 1 行に並ぶ（幅 = 合計、高さ = 最大）
        Assert.Equal(120.0, panel.DesiredSize.Width);
        Assert.Equal(100.0, panel.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void Arrange_子要素がない場合は空のサイズを返す()
    {
        var panel = new BottomAlignWrapPanel();

        panel.Measure(new Size(100, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(0.0, panel.DesiredSize.Width);
        Assert.Equal(0.0, panel.DesiredSize.Height);
    }
}

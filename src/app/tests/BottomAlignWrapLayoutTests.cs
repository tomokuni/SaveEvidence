using Estable.SaveEvidence.Controls;
using Avalonia;
using Xunit;

namespace Estable.SaveEvidence.Tests;

/// <summary><see cref="BottomAlignWrapLayout"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 折り返し・行高さ（最大アイテム高さ）・行内の下端揃え配置の計算ロジックのみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// <see cref="Control"/> の Measure / Arrange を介した統合動作は <see cref="BottomAlignWrapPanelTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class BottomAlignWrapLayoutTests
{
    /// <summary>サイズ群のヘルパー。</summary>
    private static Size S(double w, double h) => new(w, h);

    // ====================================================================
    // Measure（折り返し・行高さ）
    // ====================================================================

    [Fact]
    public void Measure_単一の行では行の高さが最大アイテム高さになる()
    {
        var sizes = new[] { S(60, 100), S(60, 50), S(60, 80) };

        var result = BottomAlignWrapLayout.Measure(sizes, availableWidth: 1000, wrap: true);

        // 幅 = アイテム幅の合計、高さ = 最大アイテム高さ（100）
        Assert.Equal(180.0, result.Width);
        Assert.Equal(100.0, result.Height);
    }

    [Fact]
    public void Measure_複数行では高さが各行の高さの合計になる()
    {
        var sizes = new[] { S(60, 100), S(60, 50), S(60, 80), S(60, 40) };

        var result = BottomAlignWrapLayout.Measure(sizes, availableWidth: 120, wrap: true);

        // 1 行目（高さ 100）+ 2 行目（高さ 80）= 180
        Assert.Equal(180.0, result.Height);
        Assert.Equal(120.0, result.Width);
    }

    [Fact]
    public void Measure_利用可能幅が無限の場合は折り返さない()
    {
        var sizes = new[] { S(60, 100), S(60, 50) };

        var result = BottomAlignWrapLayout.Measure(sizes, availableWidth: double.PositiveInfinity, wrap: false);

        // 1 行に並ぶ（幅 = 合計、高さ = 最大）
        Assert.Equal(120.0, result.Width);
        Assert.Equal(100.0, result.Height);
    }

    [Fact]
    public void Measure_子要素がない場合は空のサイズを返す()
    {
        var result = BottomAlignWrapLayout.Measure([], availableWidth: 100, wrap: true);

        Assert.Equal(0.0, result.Width);
        Assert.Equal(0.0, result.Height);
    }

    // ====================================================================
    // Arrange（下端揃え配置）
    // ====================================================================

    [Fact]
    public void Arrange_行内でアイテムが下端揃えされる()
    {
        var sizes = new[] { S(60, 100), S(60, 50), S(60, 80) };

        var rects = BottomAlignWrapLayout.Arrange(sizes, finalWidth: 1000);

        // 行の高さ = 100。各アイテムの下端（Y + Height）が行の下端（100）で揃う
        Assert.Equal(0.0, rects[0].Y);
        Assert.Equal(50.0, rects[1].Y);  // 100 - 50
        Assert.Equal(20.0, rects[2].Y);  // 100 - 80
        Assert.Equal(100.0, rects[0].Bottom);
        Assert.Equal(100.0, rects[1].Bottom);
        Assert.Equal(100.0, rects[2].Bottom);
    }

    [Fact]
    public void Arrange_幅超過で次の行に折り返される()
    {
        var sizes = new[] { S(60, 100), S(60, 50), S(60, 80), S(60, 40) };

        var rects = BottomAlignWrapLayout.Arrange(sizes, finalWidth: 120);

        // 1 行目: 高さ 100 → 2 行目の先頭 Y = 100
        Assert.Equal(0.0, rects[0].Y);
        Assert.Equal(50.0, rects[1].Y);  // 100 - 50（下端揃え）
        Assert.Equal(100.0, rects[2].Y);
        Assert.Equal(140.0, rects[3].Y); // 100 + 80 - 40（2 行目の高さ 80 で下端揃え）
    }

    [Fact]
    public void Arrange_アイテムが幅を超える場合は単独行に置かれる()
    {
        var sizes = new[] { S(150, 100), S(60, 50) };

        var rects = BottomAlignWrapLayout.Arrange(sizes, finalWidth: 100);

        // 幅 150 のアイテムは単独行。次のアイテムは下の行に置かれる
        Assert.Equal(0.0, rects[0].Y);
        Assert.Equal(100.0, rects[1].Y); // 先頭の高さ（100）の下の行
    }

    [Fact]
    public void Arrange_子要素がない場合は空の配置を返す()
    {
        var rects = BottomAlignWrapLayout.Arrange([], finalWidth: 100);

        Assert.Empty(rects);
    }
}

using Xunit;

namespace Estable.AvaloniaUI.ZoomView.Tests;

/// <summary>
/// ZoomTransition の回帰テスト。
/// </summary>
/// <remarks>
/// ズーム遷移仕様（⊖⊕ 23 段階・Ctrl+ホイール ×1.1・Fit 基準の遷移）を検証する。<br/>
/// バグ修正（ズーム中心維持・Fit 実効倍率の表示状態依存・段階遷移の往復）の回帰防止を目的とする。
/// </remarks>
public class ZoomTransitionTests
{
    // ====================================================================
    // ButtonZoomLevels の完全性
    // ====================================================================

    [Fact]
    public void ButtonZoomLevels_仕様の23段階が昇順で定義されている()
    {
        var expected = new[] { 10, 12, 15, 20, 25, 33, 40, 50, 62, 75, 80, 90, 100, 110, 125, 150, 200, 250, 300, 350, 400, 450, 500 };
        Assert.Equal(expected, ZoomTransition.ButtonZoomLevels);
    }

    [Fact]
    public void ButtonZoomLevels_10から500の範囲で昇順に並んでいる()
    {
        var levels = ZoomTransition.ButtonZoomLevels;
        Assert.Equal(ZoomTransition.MinZoom, levels[0]);
        Assert.Equal(ZoomTransition.MaxZoom, levels[^1]);
        for (int i = 1; i < levels.Count; i++)
        {
            Assert.True(levels[i - 1] < levels[i], $"段階 {i} が昇順でない: {levels[i - 1]} -> {levels[i]}");
        }
    }

    // ====================================================================
    // NextLevel（ズームインの段階遷移）
    // ====================================================================

    [Theory]
    [InlineData(10, 12)]
    [InlineData(25, 33)]
    [InlineData(100, 110)]
    [InlineData(250, 300)]
    [InlineData(450, 500)]
    public void NextLevel_固定倍率の次の段階へ遷移する(int current, int expected)
    {
        var result = ZoomTransition.NextLevel(current, fitZoom: 33);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NextLevel_リスト末尾の500では移動不可()
    {
        var result = ZoomTransition.NextLevel(current: 500, fitZoom: 33);
        Assert.Null(result);
    }

    [Fact]
    public void NextLevel_リスト外の中間値から次に大きい段階へ遷移する()
    {
        var result = ZoomTransition.NextLevel(current: 30, fitZoom: 33);
        Assert.Equal(33, result);
    }

    [Fact]
    public void NextLevel_リスト外の500超から移動不可()
    {
        var result = ZoomTransition.NextLevel(current: 600, fitZoom: 33);
        Assert.Null(result);
    }

    [Fact]
    public void NextLevel_Fit中は実効倍率より大きい最初の段階へ遷移する()
    {
        var result = ZoomTransition.NextLevel(current: 0, fitZoom: 33);
        Assert.Equal(40, result);
    }

    [Fact]
    public void NextLevel_Fit中の実効倍率が小数でも同じ遷移になる()
    {
        // 実効倍率は小数になり得る（例: ビューポートに応じた Fit 倍率）。
        // 整数レベルとの比較（floor）と等価になることを保証する（回帰: 二分探索の正規化）。
        Assert.Equal(40, ZoomTransition.NextLevel(current: 0, fitZoom: 33.5));
        Assert.Equal(40, ZoomTransition.NextLevel(current: 0, fitZoom: 39.9));
        Assert.Equal(50, ZoomTransition.NextLevel(current: 0, fitZoom: 40.0));
    }

    [Fact]
    public void NextLevel_Fit中の実効倍率が500以上なら移動不可()
    {
        var result = ZoomTransition.NextLevel(current: 0, fitZoom: 500);
        Assert.Null(result);
    }

    // ====================================================================
    // PrevLevel（ズームアウトの段階遷移）
    // ====================================================================

    [Theory]
    [InlineData(33, 25)]
    [InlineData(100, 90)]
    [InlineData(250, 200)]
    [InlineData(500, 450)]
    public void PrevLevel_固定倍率の前の段階へ遷移する(int current, int expected)
    {
        var result = ZoomTransition.PrevLevel(current, fitZoom: 33);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PrevLevel_リスト先頭の10では移動不可()
    {
        var result = ZoomTransition.PrevLevel(current: 10, fitZoom: 33);
        Assert.Null(result);
    }

    [Fact]
    public void PrevLevel_リスト外の中間値から次に小さい段階へ遷移する()
    {
        var result = ZoomTransition.PrevLevel(current: 30, fitZoom: 33);
        Assert.Equal(25, result);
    }

    [Fact]
    public void PrevLevel_リスト外の10未満から移動不可()
    {
        var result = ZoomTransition.PrevLevel(current: 5, fitZoom: 33);
        Assert.Null(result);
    }

    [Fact]
    public void PrevLevel_Fit中は実効倍率より小さい最後の段階へ遷移する()
    {
        var result = ZoomTransition.PrevLevel(current: 0, fitZoom: 33);
        Assert.Equal(25, result);
    }

    [Fact]
    public void PrevLevel_Fit中の実効倍率が小数でも同じ遷移になる()
    {
        // 整数レベルとの比較（ceil）と等価になることを保証する（回帰: 二分探索の正規化）。
        Assert.Equal(33, ZoomTransition.PrevLevel(current: 0, fitZoom: 33.5));
        Assert.Equal(33, ZoomTransition.PrevLevel(current: 0, fitZoom: 39.9));
        Assert.Equal(33, ZoomTransition.PrevLevel(current: 0, fitZoom: 40.0));
    }

    [Fact]
    public void PrevLevel_Fit中の実効倍率が10以下なら移動不可()
    {
        var result = ZoomTransition.PrevLevel(current: 0, fitZoom: 10);
        Assert.Null(result);
    }

    // ====================================================================
    // WheelZoomIn（Ctrl+ホイール ズームイン = ×1.1）
    // ====================================================================

    [Fact]
    public void WheelZoomIn_現在の倍率の110パーセントへ遷移する()
    {
        var result = ZoomTransition.WheelZoomIn(current: 100, fitZoom: 33);
        Assert.Equal(110, result);
    }

    [Fact]
    public void WheelZoomIn_結果が500にクランプされる()
    {
        var result = ZoomTransition.WheelZoomIn(current: 500, fitZoom: 33);
        Assert.Equal(500, result);
    }

    [Fact]
    public void WheelZoomIn_10の110パーセントは11に遷移する()
    {
        var result = ZoomTransition.WheelZoomIn(current: 10, fitZoom: 33);
        Assert.Equal(11, result);
    }

    [Fact]
    public void WheelZoomIn_Fit中は実効倍率の110パーセントへ遷移する()
    {
        var result = ZoomTransition.WheelZoomIn(current: 0, fitZoom: 50);
        Assert.Equal(55, result);
    }

    [Fact]
    public void WheelZoomIn_Fit中の実効倍率が500以上なら移動不可()
    {
        var result = ZoomTransition.WheelZoomIn(current: 0, fitZoom: 500);
        Assert.Null(result);
    }

    // ====================================================================
    // WheelZoomOut（Ctrl+ホイール ズームアウト = ÷1.1）
    // ====================================================================

    [Fact]
    public void WheelZoomOut_現在の倍率の110パーセント分の1へ遷移する()
    {
        var result = ZoomTransition.WheelZoomOut(current: 110, fitZoom: 33);
        Assert.Equal(100, result);
    }

    [Fact]
    public void WheelZoomOut_結果が10にクランプされる()
    {
        var result = ZoomTransition.WheelZoomOut(current: 10, fitZoom: 33);
        Assert.Equal(10, result);
    }

    [Fact]
    public void WheelZoomOut_Fit中は実効倍率の110パーセント分の1へ遷移する()
    {
        var result = ZoomTransition.WheelZoomOut(current: 0, fitZoom: 50);
        Assert.Equal(45, result);
    }

    [Fact]
    public void WheelZoomOut_Fit中の実効倍率が10以下なら移動不可()
    {
        var result = ZoomTransition.WheelZoomOut(current: 0, fitZoom: 10);
        Assert.Null(result);
    }

    // ====================================================================
    // CanZoomIn / CanZoomOut
    // ====================================================================

    [Fact]
    public void CanZoomIn_遷移先があればtrue()
    {
        Assert.True(ZoomTransition.CanZoomIn(current: 25, fitZoom: 33));
        Assert.True(ZoomTransition.CanZoomIn(current: 0, fitZoom: 33));
    }

    [Fact]
    public void CanZoomIn_遷移先がなければfalse()
    {
        Assert.False(ZoomTransition.CanZoomIn(current: 500, fitZoom: 33));
        Assert.False(ZoomTransition.CanZoomIn(current: 0, fitZoom: 500));
    }

    [Fact]
    public void CanZoomOut_遷移先があればtrue()
    {
        Assert.True(ZoomTransition.CanZoomOut(current: 25, fitZoom: 33));
        Assert.True(ZoomTransition.CanZoomOut(current: 0, fitZoom: 33));
    }

    [Fact]
    public void CanZoomOut_遷移先がなければfalse()
    {
        Assert.False(ZoomTransition.CanZoomOut(current: 10, fitZoom: 33));
        Assert.False(ZoomTransition.CanZoomOut(current: 0, fitZoom: 10));
    }
}

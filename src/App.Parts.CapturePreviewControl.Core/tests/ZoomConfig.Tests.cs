using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary>
/// ZoomConfig（ズーム遷移の設定・検証付き不変値）の仕様テスト。
/// </summary>
/// <remarks>
/// 不変条件（MinZoom > 0 / MaxZoom >= MinZoom / WheelFactor > 1 / レベル集合の非空・昇順・範囲内）と<br/>
/// <c>ClampZoom</c>（ズーム範囲クランプ）を検証する。<br/>
/// UI 初期化を必要としないため、Avalonia ヘッドレスに依存せず並列実行できる。
/// </remarks>
public class ZoomConfigTests
{
    // ====================================================================
    // 既定値（ZoomConfig.Default）
    // ====================================================================

    [Fact]
    public void Default_従来の定数値を持つ()
    {
        var config = ZoomConfig.Default;
        Assert.Equal(10.0, config.MinZoom);
        Assert.Equal(500.0, config.MaxZoom);
        Assert.Equal(1.1, config.WheelFactor);
        Assert.Equal(ZoomTransition.DefaultButtonZoomLevels, config.ButtonZoomLevels);
    }

    [Fact]
    public void Default_レベル集合が昇順である()
    {
        var levels = ZoomConfig.Default.ButtonZoomLevels;
        for (var i = 1; i < levels.Count; i++)
        {
            Assert.True(levels[i - 1] < levels[i], $"段階 {i} が昇順でない: {levels[i - 1]} -> {levels[i]}");
        }
    }

    // ====================================================================
    // コンストラクタ（検証）
    // ====================================================================

    [Theory]
    [InlineData(0.0, 500.0, 1.1)]   // MinZoom <= 0
    [InlineData(-1.0, 500.0, 1.1)]  // MinZoom < 0
    public void コンストラクタ_最小値が0以下なら例外を投げる(double minZoom, double maxZoom, double wheelFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZoomConfig(minZoom, maxZoom, wheelFactor, [10, 50, 100]));
    }

    [Theory]
    [InlineData(500.0, 10.0, 1.1)]  // MaxZoom < MinZoom
    [InlineData(100.0, 100.0, 1.1)] // MaxZoom == MinZoom（範囲内なら許容）
    public void コンストラクタ_最大値が最小値未満なら例外を投げる(double minZoom, double maxZoom, double wheelFactor)
    {
        // MaxZoom < MinZoom のみ例外。 MaxZoom == MinZoom は許容（レベルの範囲が点になる）
        if (maxZoom < minZoom)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ZoomConfig(minZoom, maxZoom, wheelFactor, [minZoom]));
        }
        else
        {
            var config = new ZoomConfig(minZoom, maxZoom, wheelFactor, [minZoom]);
            Assert.Equal(minZoom, config.MaxZoom);
        }
    }

    [Theory]
    [InlineData(10.0, 500.0, 1.0)]  // WheelFactor == 1
    [InlineData(10.0, 500.0, 0.9)]  // WheelFactor < 1
    public void コンストラクタ_ズーム倍率が1以下なら例外を投げる(double minZoom, double maxZoom, double wheelFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZoomConfig(minZoom, maxZoom, wheelFactor, [10, 50, 100]));
    }

    [Fact]
    public void コンストラクタ_レベル集合がnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ZoomConfig(10, 500, 1.1, null!));
    }

    [Fact]
    public void コンストラクタ_レベル集合が空なら例外を投げる()
    {
        Assert.Throws<ArgumentException>(() =>
            new ZoomConfig(10, 500, 1.1, []));
    }

    [Fact]
    public void コンストラクタ_レベル集合が昇順でないなら例外を投げる()
    {
        Assert.Throws<ArgumentException>(() =>
            new ZoomConfig(10, 500, 1.1, [50, 10, 100]));
    }

    [Fact]
    public void コンストラクタ_レベル集合がズーム範囲外なら例外を投げる()
    {
        // レベル 5 は MinZoom(10) 未満
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZoomConfig(10, 500, 1.1, [10, 50, 100, 5]));
    }

    [Fact]
    public void コンストラクタ_正しい値なら正常に生成できる()
    {
        var config = new ZoomConfig(10, 500, 1.1, [10, 50, 100, 200, 500]);
        Assert.Equal(10.0, config.MinZoom);
        Assert.Equal(500.0, config.MaxZoom);
        Assert.Equal(1.1, config.WheelFactor);
        Assert.Equal(5, config.ButtonZoomLevels.Count);
    }

    // ====================================================================
    // ClampZoom（ズーム範囲クランプ）
    // ====================================================================

    [Theory]
    [InlineData(5.0, 10.0)]   // 下限未満 → MinZoom
    [InlineData(300.0, 300.0)] // 範囲内 → そのまま
    [InlineData(800.0, 500.0)] // 上限超過 → MaxZoom
    public void ClampZoom_ズーム率を範囲内にクランプする(double input, double expected)
    {
        var config = new ZoomConfig(10, 500, 1.1, [10, 50, 100]);
        Assert.Equal(expected, config.ClampZoom(input));
    }

    // ====================================================================
    // With（検証付き部分更新）
    // ====================================================================

    [Fact]
    public void With_指定したプロパティのみ差し替える()
    {
        var config = new ZoomConfig(10, 500, 1.1, [10, 50, 100]);

        var updated = config.With(wheelFactor: 1.2);

        Assert.Equal(10.0, updated.MinZoom);    // 未指定は現在値
        Assert.Equal(500.0, updated.MaxZoom);   // 未指定は現在値
        Assert.Equal(1.2, updated.WheelFactor); // 差し替え
        Assert.Equal([10, 50, 100], updated.ButtonZoomLevels); // 未指定は現在値
        // 元の config は不変（影響を受けない）
        Assert.Equal(1.1, config.WheelFactor);
    }

    [Fact]
    public void With_不正な値なら例外を投げる()
    {
        var config = new ZoomConfig(10, 500, 1.1, [10, 50, 100]);

        // minZoom を MaxZoom 超に設定 → 検証エラー
        Assert.Throws<ArgumentOutOfRangeException>(() => config.With(minZoom: 800));
        // WheelFactor を 1 以下に設定 → 検証エラー
        Assert.Throws<ArgumentOutOfRangeException>(() => config.With(wheelFactor: 1.0));
    }
}

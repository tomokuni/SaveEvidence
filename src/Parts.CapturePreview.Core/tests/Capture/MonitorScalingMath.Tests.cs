using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Capture;

/// <summary><see cref="MonitorScalingMath"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>判定点（矩形の中心）とフォールバックの規則を固定する。</remarks>
public class MonitorScalingMathTests
{
    /// <summary>検証に使うモニター構成（左 100%、右 150%）。</summary>
    private static readonly (EsRectInt Bounds, double Scale)[] s_dualMonitors =
    [
        (new EsRectInt(0, 0, 1920, 1080), 1.0),
        (new EsRectInt(1920, 0, 2560, 1440), 1.5),
    ];

    [Fact]
    internal void ResolveForRect_中心を含むモニターの倍率を返す()
    {
        var target = new EsRectInt(100, 100, 400, 300);

        Assert.Equal(1.0, MonitorScalingMath.ResolveForRect(target, s_dualMonitors, fallback: 9.9));
    }

    // ====================================================================
    // ResolveScaling（画面のスケーリングの代表値）
    // ====================================================================

    [Fact]
    internal void ResolveScaling_先頭の画面のスケーリングを返す()
    {
        Assert.Equal(1.5, MonitorScalingMath.ResolveScaling([1.5, 1.0]));
    }

    [Fact]
    internal void ResolveScaling_画面が無い場合は基準値を返す()
    {
        // ヘッドレステストやモニター未接続では画面情報を取得できない（基準値で描画を続ける）
        Assert.Equal(DpiScaler.StandardScaling, MonitorScalingMath.ResolveScaling([]));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    internal void ResolveScaling_未確定のスケーリングは基準値へ正規化する(double scaling)
    {
        Assert.Equal(DpiScaler.StandardScaling, MonitorScalingMath.ResolveScaling([scaling]));
    }

    // ====================================================================
    // Collect（判定に使うモニター一覧の構築）
    // ====================================================================

    [Fact]
    internal void Collect_倍率が未確定のモニターを除外する()
    {
        // 倍率 0 以下は未確定として扱い、判定に使うと表示倍率が 0 倍になるため除外する
        var monitors = new (EsRectInt Bounds, double Scale)[]
        {
            (new EsRectInt(0, 0, 1920, 1080), 1.0),
            (new EsRectInt(1920, 0, 2560, 1440), 0),
            (new EsRectInt(4480, 0, 2560, 1440), -1.5),
        };

        var result = MonitorScalingMath.Collect(monitors);

        Assert.Single(result);
        Assert.Equal(new EsRectInt(0, 0, 1920, 1080), result[0].Bounds);
    }

    [Fact]
    internal void Collect_入力の順序を保つ()
    {
        var monitors = new (EsRectInt Bounds, double Scale)[]
        {
            (new EsRectInt(1920, 0, 2560, 1440), 1.5),
            (new EsRectInt(0, 0, 1920, 1080), 1.0),
        };

        var result = MonitorScalingMath.Collect(monitors);

        Assert.Equal(new EsRectInt(1920, 0, 2560, 1440), result[0].Bounds);
        Assert.Equal(new EsRectInt(0, 0, 1920, 1080), result[1].Bounds);
    }

    [Fact]
    internal void Collect_空の一覧は空を返す()
    {
        Assert.Empty(MonitorScalingMath.Collect([]));
    }

    [Fact]
    internal void ResolveForRect_右側のモニターに中心がある場合はその倍率を返す()
    {
        var target = new EsRectInt(2400, 100, 400, 300);

        Assert.Equal(1.5, MonitorScalingMath.ResolveForRect(target, s_dualMonitors, fallback: 9.9));
    }

    [Fact]
    internal void ResolveForRect_境界を跨ぐ矩形は中心のモニターで決まる()
    {
        // 左上が左モニター・右下が右モニター。 中心が右モニターに入る
        var target = new EsRectInt(1900, 0, 100, 100);

        Assert.Equal(1.5, MonitorScalingMath.ResolveForRect(target, s_dualMonitors, fallback: 9.9));
    }

    [Fact]
    internal void ResolveForRect_どのモニターにも含まれない場合はフォールバックを返す()
    {
        var target = new EsRectInt(-5000, -5000, 100, 100);

        Assert.Equal(9.9, MonitorScalingMath.ResolveForRect(target, s_dualMonitors, fallback: 9.9));
    }

    [Fact]
    internal void ResolveForRect_モニターが空の場合はフォールバックを返す()
    {
        var target = new EsRectInt(0, 0, 100, 100);

        Assert.Equal(2.0, MonitorScalingMath.ResolveForRect(target, [], fallback: 2.0));
    }

    [Fact]
    internal void ResolveForRect_幅と高さが0でも中心で判定する()
    {
        // 幅・高さ 0 の矩形（点）でも判定できる
        var target = new EsRectInt(2000, 500, 0, 0);

        Assert.Equal(1.5, MonitorScalingMath.ResolveForRect(target, s_dualMonitors, fallback: 9.9));
    }

    // ====================================================================
    // ResolveForSourceRect（対象座標 + 原点 → 画面物理座標）
    // ====================================================================

    [Fact]
    internal void ResolveForSourceRect_原点を加算して対象矩形を判定する()
    {
        // 対象 (100, 100) の原点が (1920, 0) のとき、画面物理 (2020, 100) は右モニターに入る
        var source = new EsRect(100, 100, 400, 300);

        var result = MonitorScalingMath.ResolveForSourceRect(
            source, new EsPointInt(1920, 0), s_dualMonitors, fallback: 9.9);

        Assert.Equal(1.5, result);
    }

    [Fact]
    internal void ResolveForSourceRect_原点が原点なら画面物理と一致する()
    {
        var source = new EsRect(100, 100, 400, 300);

        var result = MonitorScalingMath.ResolveForSourceRect(
            source, default, s_dualMonitors, fallback: 9.9);

        Assert.Equal(1.0, result);
    }

    [Fact]
    internal void ResolveForSourceRect_どのモニターにも含まれない場合はフォールバックを返す()
    {
        var source = new EsRect(-9000, -9000, 100, 100);

        var result = MonitorScalingMath.ResolveForSourceRect(
            source, default, s_dualMonitors, fallback: 2.5);

        Assert.Equal(2.5, result);
    }

    [Theory]
    [InlineData(100.4)]
    [InlineData(100.6)]
    [InlineData(100.5)]
    internal void ResolveForSourceRect_小数座標は四捨五入して画面物理へ戻す(double x)
    {
        // 四捨五入で整数化するため、どちらへ丸めても同じモニターの判定になる（境界から離れた位置）
        var source = new EsRect(x, 100, 400, 300);

        var result = MonitorScalingMath.ResolveForSourceRect(
            source, default, s_dualMonitors, fallback: 9.9);

        Assert.Equal(1.0, result);
    }
}

using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary><see cref="ZoomLogMessage"/> / <see cref="ZoomLogFormat"/> のテスト（書式・レベル・丸め）。</summary>
/// <remarks>
/// ログの書式は両 UI で同一でなければならないため、コアが単一所有する。<br/>
/// 本テストが書式を固定し、UI 側は書式を持たない。
/// </remarks>
public class ZoomLogMessageTests
{
    /// <summary>ズーム率のログ用の丸めが四捨五入であることを検証する。</summary>
    /// <param name="value">丸める前の値。</param>
    /// <param name="expected">期待する整数。</param>
    [Theory]
    [InlineData(50.0, 50)]
    [InlineData(61.6, 62)]
    [InlineData(61.4, 61)]
    [InlineData(109.5, 110)]
    [InlineData(0.4, 0)]
    public void Percent_四捨五入する(double value, int expected)
        => Assert.Equal(expected, ZoomLogFormat.Percent(value));

    /// <summary>倍率の文字列化が不変カルチャで行われることを検証する。</summary>
    [Fact]
    public void Scale_不変カルチャで整形する()
        => Assert.Equal("1.25", ZoomLogFormat.Scale(1.25));

    /// <summary>ズームイン・ズームアウトの書式が両 UI 共通の形式であることを検証する。</summary>
    [Fact]
    public void ズーム操作の書式が期待どおりである()
    {
        var logger = new RecordingLogger();

        logger.LogZoomIn(current: 50, target: 62);
        logger.LogZoomOut(current: 62, target: 50);
        logger.LogZoomInWheel(current: 100, target: 110);
        logger.LogZoomOutWheel(current: 110, target: 100);

        Assert.Equal("[ZoomIn] 50% -> 62%", logger.Messages[0]);
        Assert.Equal("[ZoomOut] 62% -> 50%", logger.Messages[1]);
        Assert.Equal("[ZoomInWheel] 100% -> 110%", logger.Messages[2]);
        Assert.Equal("[ZoomOutWheel] 110% -> 100%", logger.Messages[3]);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも安全に実行される()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        logger.LogZoomIn(50, 62);
        logger.LogZoomOut(62, 50);
        logger.LogZoomInWheel(100, 110);
        logger.LogZoomOutWheel(110, 100);
    }
}

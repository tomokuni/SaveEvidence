using Microsoft.Extensions.Logging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Loupe.Presenter.Logging;
using EsUtil.Feature.Loupe.Presenter.Setting;

namespace EsUtil.Feature.Loupe.Presenter.Tests;

/// <summary><see cref="LoupePresenterLogMessage"/> の単体テスト（外観の変更ログ）。</summary>
/// <remarks>
/// 「値を所有する主体が記録する」規則により、提示層が単一所有する外観（<see cref="LoupeAppearance"/>）の変更を<br/>
/// 本層が記録する。<b>変化した項目だけ</b>を出力する（外観の再適用でログが溢れないため）。<br/>
/// ログは UI 初期化なしで検証できる（LoggerMessage の定義は UI フレームワークに依存しない）。
/// </remarks>
public class LoupePresenterLogMessageTests
{
    [Fact]
    public void LogAppearanceChanged_変化した項目だけを記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with { BorderThickness = 5, ShowCrosshair = false };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            [
                "[Feature.Loupe.Presenter] Setting: ShowCrosshair True → False",
                "[Feature.Loupe.Presenter] Setting: BorderThickness 2 → 5",
            ],
            logger.Messages);
    }

    [Fact]
    public void LogAppearanceChanged_同値なら記録しない()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;

        logger.LogAppearanceChanged(before, before with { });

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void LogAppearanceChanged_色はAARRGGBB形式で記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with { CrossColor = DrawingColor.FromArgb(128, 255, 0, 0) };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            "[Feature.Loupe.Presenter] Setting: CrossColor #66FF0000 → #80FF0000",
            Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogAppearanceChanged_複数項目の変更を定義順に記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with
        {
            PasteboardColor = DrawingColor.FromArgb(1, 2, 3, 4),
            ShowBorder = false,
            ShowCoordLabel = false,
        };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(3, logger.Messages.Count);
        Assert.Equal("[Feature.Loupe.Presenter] Setting: PasteboardColor #FF535353 → #01020304", logger.Messages[0]);
        Assert.Equal("[Feature.Loupe.Presenter] Setting: ShowBorder True → False", logger.Messages[1]);
        Assert.Equal("[Feature.Loupe.Presenter] Setting: ShowCoordLabel True → False", logger.Messages[2]);
    }

    [Fact]
    public void LogAppearanceChanged_実数項目をカルチャに依存せず記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with { CoordLabelFontSize = 24.5 };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            "[Feature.Loupe.Presenter] Setting: CoordLabelFontSize 12 → 24.5",
            Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogSettingChanged_定義名と出力書式を固定する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("DipToDeviceScaling", 1.0, 1.25);

        Assert.Equal("[Feature.Loupe.Presenter] Setting: DipToDeviceScaling 1 → 1.25", Assert.Single(logger.Messages));
    }
}

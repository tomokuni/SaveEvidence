using System.Globalization;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.LoupeCore.Setting;
using EsUtil.Feature.LoupeCore.Logging;

namespace EsUtil.Feature.LoupeCore.Tests.Logging;

/// <summary><see cref="LoupeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義はコアが出力するものだけを本コアが持つ（UI が出力する定義はコントロール化の層が持つ）。<br/>
/// <see cref="RecordingLogger"/>（IsEnabled=true）と <see cref="NullLogger"/>（IsEnabled=false）の両方で<br/>
/// 全メソッドを直接呼び、LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。
/// </remarks>
public class LoupeLogMessageTests
{
    /// <summary>実ロガー設定時に全ログメソッドが実行されることを検証する。</summary>
    [Fact]
    public void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(6, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が両 UI で共通の書式と一致することを検証する。</summary>
    [Fact]
    public void 出力書式が期待どおりである()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[Feature.LoupeCore] Setting: ZoomLevel 4 → 8", logger.Messages[0]);
        Assert.Equal("[Feature.LoupeCore] Setting: RequestedSize 160 → 240", logger.Messages[1]);
        Assert.Equal("[Feature.LoupeCore] Setting: BorderThickness 2 → 8", logger.Messages[2]);
        Assert.Equal("[Feature.LoupeCore] Setting: ShowBorder True → False", logger.Messages[3]);
        Assert.Equal("[Feature.LoupeCore] Setting: CenterPoint (1.5, 2.5) → null", logger.Messages[4]);
    }

    /// <summary>拡大中心のログが両 UI 共通の書式（括弧付き座標）で出力されることを検証する。</summary>
    [Fact]
    public void 拡大中心のログは括弧付き座標で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCenterPointChanged(new EsPoint(10, 20), null);
        logger.LogCenterPointChanged(null, new EsPoint(30, 40));

        Assert.Equal("[Feature.LoupeCore] Setting: CenterPoint (10, 20) → null", logger.Messages[0]);
        Assert.Equal("[Feature.LoupeCore] Setting: CenterPoint null → (30, 40)", logger.Messages[1]);
    }

    /// <summary>真偽設定値のログが変化したときだけ出力されることを検証する。</summary>
    /// <remarks>
    /// 表示可否（<c>Show*</c>）は真偽値のため、数値と同じ書式（<c>True</c> / <c>False</c>）で出力する<br/>
    /// （<c>bool</c> は <see cref="IFormattable"/> を実装しないため本体の汎用版とは別の経路になる）。
    /// </remarks>
    [Fact]
    public void 真偽設定値のログは変化したときだけ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("ShowBorder", true, false);   // 変化 → 出力
        logger.LogSettingChanged("ShowBorder", false, false);  // 同値 → 出力しない

        Assert.Equal("[Feature.LoupeCore] Setting: ShowBorder True → False", Assert.Single(logger.Messages));
    }

    /// <summary>数値設定値のログが変化したときだけ出力されることを検証する。</summary>
    [Fact]
    public void 数値設定値のログは変化したときだけ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("ZoomLevel", 4, 8);          // 変化 → 出力
        logger.LogSettingChanged("ZoomLevel", 8, 8);          // 同値 → 出力しない
        logger.LogSettingChanged("DipToDeviceScaling", 1.0, 1.5);  // 変化 → 出力
        logger.LogSettingChanged("DipToDeviceScaling", 1.5, 1.5);  // 同値 → 出力しない

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[Feature.LoupeCore] Setting: ZoomLevel 4 → 8", logger.Messages[0]);
        Assert.Equal("[Feature.LoupeCore] Setting: DipToDeviceScaling 1 → 1.5", logger.Messages[1]);
    }

    /// <summary>数値のログがカルチャに依存しないことを検証する。</summary>
    [Fact]
    public void 数値のログはカルチャに依存しない()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // 小数の区切りがコンマのカルチャでも不変の表記（ピリオド）で出力する
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var logger = new RecordingLogger();

            logger.LogSettingChanged((string)"DipToDeviceScaling", 1.0, 2.5);

            Assert.Equal("[Feature.LoupeCore] Setting: DipToDeviceScaling 1 → 2.5", logger.Messages[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>拡大中心のログが同値の再設定では出力されないことを検証する。</summary>
    /// <remarks>中心はマウス移動のたびに設定されるため、同値判定はコアが単一所有する。</remarks>
    [Fact]
    public void 拡大中心のログは同値なら出力しない()
    {
        var logger = new RecordingLogger();

        logger.LogCenterPointChanged(new EsPoint(10, 20), new EsPoint(10, 20));
        logger.LogCenterPointChanged(null, null);

        Assert.Empty(logger.Messages);
    }

    /// <summary>設定（LoupeOptions）の変更ログが変化した項目だけを出力することを検証する。</summary>
    /// <remarks>設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）はコアが単一所有する。</remarks>
    [Fact]
    public void 設定の変更ログは変化した項目だけを出力する()
    {
        var logger = new RecordingLogger();
        var oldValue = new LoupeOptions();

        logger.LogOptionsChanged(oldValue, oldValue with { RequestedSize = 240, ShowCrosshair = false });

        Assert.Equal(
            [
                "[Feature.LoupeCore] Setting: RequestedSize 160 → 240",
                "[Feature.LoupeCore] Setting: ShowCrosshair True → False",
            ],
            logger.Messages);
    }

    /// <summary>設定の変更ログが寸法・表示可否を含む全項目を対象にすることを検証する。</summary>
    /// <remarks>
    /// 各項目のログが同値判定を行うため、全項目を対象にしても変化した項目しか出力されない<br/>
    /// （対象外の項目を設けると、その項目の変更だけがログから漏れる）。
    /// </remarks>
    [Fact]
    public void 設定の変更ログは全項目を対象にする()
    {
        var logger = new RecordingLogger();
        var oldValue = new LoupeOptions();
        var newValue = oldValue with
        {
            RequestedSize = 240,
            ZoomLevel = 8,
            CheckerSize = 12,
            BorderThickness = 8,
            ShowCoordLabel = false,
            ShowCheckerboard = false,
            ShowCrosshair = false,
            ShowBorder = false,
        };

        logger.LogOptionsChanged(oldValue, newValue);

        Assert.Equal(
            [
                "[Feature.LoupeCore] Setting: RequestedSize 160 → 240",
                "[Feature.LoupeCore] Setting: ZoomLevel 4 → 8",
                "[Feature.LoupeCore] Setting: CheckerSize 6 → 12",
                "[Feature.LoupeCore] Setting: BorderThickness 2 → 8",
                "[Feature.LoupeCore] Setting: ShowCoordLabel True → False",
                "[Feature.LoupeCore] Setting: ShowCheckerboard True → False",
                "[Feature.LoupeCore] Setting: ShowCrosshair True → False",
                "[Feature.LoupeCore] Setting: ShowBorder True → False",
            ],
            logger.Messages);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    /// <remarks>出力書式の検証がメッセージの並びに依存するため、呼び出す順序を固定する。</remarks>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogSettingChanged("ZoomLevel", "4", "8");                             // 1
        logger.LogSettingChanged("RequestedSize", 160, 240);                          // 2
        logger.LogSettingChanged("BorderThickness", 2.0, 8.0);                       // 3
        logger.LogSettingChanged("ShowBorder", true, false);                         // 4
        logger.LogCenterPointChanged(new EsPoint(1.5, 2.5), null);                    // 5
        logger.LogOptionsChanged(new LoupeOptions(), new LoupeOptions { ZoomLevel = 8 });  // 6
    }
}

using System.Globalization;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。
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
        Assert.Equal(3, logger.Messages.Count);
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

        Assert.Equal("[LoupeCtrl] Setting: ZoomLevel 4 → 8", logger.Messages[0]);
        Assert.Equal("[LoupeCtrl] Setting: CenterPoint (1.5, 2.5) → null", logger.Messages[1]);
        Assert.Equal("[LoupeCtrl] Update exception", logger.Messages[2]);
    }

    /// <summary>拡大中心のログが両 UI 共通の書式（括弧付き座標）で出力されることを検証する。</summary>
    [Fact]
    public void 拡大中心のログは括弧付き座標で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCenterPointChanged(new EsPoint(10, 20), null);
        logger.LogCenterPointChanged(null, new EsPoint(30, 40));

        Assert.Equal("[LoupeCtrl] Setting: CenterPoint (10, 20) → null", logger.Messages[0]);
        Assert.Equal("[LoupeCtrl] Setting: CenterPoint null → (30, 40)", logger.Messages[1]);
    }

    /// <summary>設定項目ごとの名前付きログが同じ書式で出力されることを検証する。</summary>
    /// <remarks>設定名はコアが単一所有するため、UI 層に文字列リテラル（nameof）を置かない。</remarks>
    [Fact]
    public void 名前付きログは設定名をコアで付与する()
    {
        var logger = new RecordingLogger();

        logger.LogSourceChanged("1920x1080", "null");
        logger.LogRequestedSizeChanged(160, 240);
        logger.LogRenderScalingChanged(1.0, 2.0);
        logger.LogZoomLevelChanged(4, 8);

        Assert.Equal(4, logger.Messages.Count);
        Assert.Equal("[LoupeCtrl] Setting: Source 1920x1080 → null", logger.Messages[0]);
        Assert.Equal("[LoupeCtrl] Setting: RequestedSize 160 → 240", logger.Messages[1]);
        Assert.Equal("[LoupeCtrl] Setting: RenderScaling 1 → 2", logger.Messages[2]);
        Assert.Equal("[LoupeCtrl] Setting: ZoomLevel 4 → 8", logger.Messages[3]);
    }

    /// <summary>名前付きログも同値の再設定では出力しないことを検証する。</summary>
    [Fact]
    public void 名前付きログも同値なら出力しない()
    {
        var logger = new RecordingLogger();

        logger.LogRequestedSizeChanged(160, 160);
        logger.LogRenderScalingChanged(1.0, 1.0);
        logger.LogZoomLevelChanged(4, 4);

        Assert.Empty(logger.Messages);
    }

    /// <summary>数値設定値のログが変化したときだけ出力されることを検証する。</summary>
    [Fact]
    public void 数値設定値のログは変化したときだけ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("ZoomLevel", 4, 8);          // 変化 → 出力
        logger.LogSettingChanged("ZoomLevel", 8, 8);          // 同値 → 出力しない
        logger.LogSettingChanged("RenderScaling", 1.0, 1.5);  // 変化 → 出力
        logger.LogSettingChanged("RenderScaling", 1.5, 1.5);  // 同値 → 出力しない

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[LoupeCtrl] Setting: ZoomLevel 4 → 8", logger.Messages[0]);
        Assert.Equal("[LoupeCtrl] Setting: RenderScaling 1 → 1.5", logger.Messages[1]);
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

            logger.LogSettingChanged((string)"RenderScaling", 1.0, 2.5);

            Assert.Equal("[LoupeCtrl] Setting: RenderScaling 1 → 2.5", logger.Messages[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>例外ログに例外が渡されることを検証する。</summary>
    [Fact]
    public void 例外ログは例外を受け取る()
    {
        var logger = new RecordingLogger();
        var exception = new InvalidOperationException("失敗");

        logger.LogUpdateError(exception);

        Assert.Single(logger.Messages);
        // メッセージ本体は例外本文を含まない（例外は構造化ログのフィールドとして渡す）
        Assert.Equal("[LoupeCtrl] Update exception", logger.Messages[0]);
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

    /// <summary>拡大元画像のログが同一インスタンスの再設定では出力されないことを検証する。</summary>
    [Fact]
    public void 拡大元画像のログは同一インスタンスなら出力しない()
    {
        using var bitmap = EsBitmap.Create(4, 4);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(bitmap, bitmap);
        logger.LogSourceChanged((EsBitmap?)null, null);

        Assert.Empty(logger.Messages);
    }

    /// <summary>拡大元画像のログが寸法の等しい別インスタンスへの差し替えを出力することを検証する。</summary>
    [Fact]
    public void 拡大元画像のログは別インスタンスなら出力する()
    {
        using var oldBitmap = EsBitmap.Create(8, 6);
        using var newBitmap = EsBitmap.Create(8, 6);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(oldBitmap, newBitmap);
        logger.LogSourceChanged(oldBitmap, null);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[LoupeCtrl] Setting: Source 8x6 → 8x6", logger.Messages[0]);
        Assert.Equal("[LoupeCtrl] Setting: Source 8x6 → null", logger.Messages[1]);
    }

    /// <summary>設定（LoupeOptions）の変更ログが変化した項目だけを出力することを検証する。</summary>
    /// <remarks>設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）はコアが単一所有する。</remarks>
    [Fact]
    public void 設定の変更ログは変化した項目だけを出力する()
    {
        var logger = new RecordingLogger();

        logger.LogOptionsChanged(
            new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 },
            new LoupeOptions { RequestedSize = 240, ZoomLevel = 4 });
        logger.LogOptionsChanged(
            new LoupeOptions { RequestedSize = 240, ZoomLevel = 4 },
            new LoupeOptions { RequestedSize = 240, ZoomLevel = 8 });

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[LoupeCtrl] Setting: RequestedSize 160 → 240", logger.Messages[0]);
        Assert.Equal("[LoupeCtrl] Setting: ZoomLevel 4 → 8", logger.Messages[1]);
    }

    /// <summary>設定（LoupeOptions）の外観だけの変更ではログを出力しないことを検証する。</summary>
    /// <remarks>外観は再適用のたびに設定されるため、項目ごとのログは出力しない（設定の変更ログは 2 項目のみ）。</remarks>
    [Fact]
    public void 設定の変更ログは外観だけの変更では出力しない()
    {
        var logger = new RecordingLogger();
        var oldValue = new LoupeOptions();
        var newValue = oldValue with { Appearance = oldValue.Appearance with { FrameWidth = 8 } };

        logger.LogOptionsChanged(oldValue, newValue);

        Assert.Empty(logger.Messages);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogSettingChanged("ZoomLevel", "4", "8");
        logger.LogCenterPointChanged(new EsPoint(1.5, 2.5), null);
        logger.LogUpdateError(new InvalidOperationException("test"));
    }
}

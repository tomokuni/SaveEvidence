using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Logging;

/// <summary><see cref="CapturePreviewLogMessage"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>ログの書式（プレフィックス・項目名）と、ロガー未設定でも安全に呼べることを固定する。</remarks>
public class CapturePreviewLogMessageTests
{
    /// <summary>ログを収集するロガー。</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>出力されたメッセージ。</summary>
        public List<string> Messages { get; } = [];

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    internal void LogCaptureStart_種別を含む書式で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCaptureStart(CaptureType.AreaSelect);

        Assert.Equal("[CapturePreview] CaptureStart: type=AreaSelect", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogCaptureFinished_確定時は完了と結果の2件を出力する()
    {
        // 「結果が確定したときにどのログをどの順序で出すか」をコアが単一所有する
        var logger = new RecordingLogger();
        using var image = EsBitmap.Create(20, 10);

        logger.LogCaptureFinished(new CaptureResult(image, new EsRectInt(1, 2, 20, 10)));

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[CapturePreview] CaptureCompleted: bounds=1,2,20,10", logger.Messages[0]);
        Assert.Equal("[CapturePreview] Result: bitmap=20x10 bounds=1,2,20,10", logger.Messages[1]);
    }

    [Fact]
    internal void LogCaptureFinished_キャンセル時はキャンセル1件を出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCaptureFinished(null);

        Assert.Equal("[CapturePreview] CaptureCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogCaptureFinished_画像が無い結果もキャンセルとして扱う()
    {
        // 確定とキャンセルの区別は画像の有無で行う（0x0 の空結果を確定として記録しない）
        var logger = new RecordingLogger();

        logger.LogCaptureFinished(new CaptureResult(null, default));

        Assert.Equal("[CapturePreview] CaptureCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogCropStart_元画像のサイズを書式で出力する()
    {
        var logger = new RecordingLogger();
        using var image = EsBitmap.Create(1920, 1080);
        image.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(image);

        logger.LogCropStart(read);

        Assert.Equal("[CapturePreview] CropStart: source=1920x1080", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogCropFinished_切り出せた場合は確定を1件出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCropFinished(new EsRectInt(10, 20, 30, 40));

        Assert.Equal("[CapturePreview] CropCompleted: bounds=10,20,30,40", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogCropFinished_切り出せない場合はキャンセルとして出力する()
    {
        // 「退化した選択はキャンセルとして記録する」規則をコアが単一所有する
        // （UI が空判定とログの呼び分けを書くと、同じ操作で UI ごとに異なるログになる）
        var logger = new RecordingLogger();

        logger.LogCropFinished(default);

        Assert.Equal("[CapturePreview] CropCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogCropCancelled_プレフィックスのみ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCropCancelled();

        Assert.Equal("[CapturePreview] CropCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogSettingApplied_プレフィックスのみ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingApplied();

        Assert.Equal("[CapturePreview] SettingApplied", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void 全ログメソッドが無効ロガーでも安全に実行できる()
    {
        var logger = NullLogger.Instance;
        using var image = EsBitmap.Create(1, 1);
        image.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(image);

        logger.LogCaptureStart(CaptureType.AreaSelect);
        logger.LogCaptureFinished(new CaptureResult(image, new EsRectInt(0, 0, 1, 1)));
        logger.LogCaptureFinished(null);
        logger.LogCropStart(read);
        logger.LogCropFinished(new EsRectInt(0, 0, 1, 1));
        logger.LogCropFinished(default);
        logger.LogCropCancelled();
        logger.LogSettingApplied();
        logger.LogConfirmRequested(new EsRect(0, 0, 1, 1));
        logger.LogCancelRequested();
        logger.LogVirtualScreen(new EsPointInt(0, 0), read);
        logger.LogVirtualScreen(new EsPointInt(1, 1), null);
        logger.LogHighlight(CaptureType.AreaSelect, true, new EsRectInt(0, 0, 1, 1), "t", "c");
    }

    [Fact]
    internal void LogVirtualScreen_原点とサイズを含む書式で出力する()
    {
        var logger = new RecordingLogger();
        using var bitmap = EsBitmap.Create(3840, 2160);
        bitmap.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bitmap);

        logger.LogVirtualScreen(new EsPointInt(-1920, -200), read);

        Assert.Equal("[CapturePreview] VirtualScreen: origin=(-1920, -200) size=3840x2160", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogVirtualScreen_画像未取得はサイズ0として出力する()
    {
        // 「未取得は 0」という正規化をコアが単一所有する（呼び出し側で null 判定を書かない）
        var logger = new RecordingLogger();

        logger.LogVirtualScreen(new EsPointInt(10, 20), null);

        Assert.Equal("[CapturePreview] VirtualScreen: origin=(10, 20) size=0x0", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogConfirmRequested_矩形を含む書式で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogConfirmRequested(new EsRectInt(10, 10, 50, 40));

        Assert.Equal("[CapturePreview] ConfirmRequested: rect=10,10,50,40", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogCancelRequested_プレフィックスのみ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCancelRequested();

        Assert.Equal("[CapturePreview] CancelRequested", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogHighlight_種別と検出結果を含む書式で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogHighlight(CaptureType.WindowSelect, true, new EsRectInt(100, 50, 800, 600), "Sample", "Notepad");

        Assert.Equal(
            "[CapturePreview] Highlight: type=WindowSelect detected=True rect=100,50,800,600 title=Sample class=Notepad",
            Assert.Single(logger.Messages));
    }

    [Fact]
    internal void CapturePreviewLogMessage_Rect_論理矩形をx_y_w_hで整形する()
    {
        Assert.Equal("10,20,30,40", CapturePreviewLogMessage.Rect(new EsRect(10, 20, 30, 40)));
    }

    [Fact]
    internal void CapturePreviewLogMessage_Rect_PixelRectをx_y_w_hで整形する()
    {
        Assert.Equal("10,20,30,40", CapturePreviewLogMessage.Rect(new EsRectInt(10, 20, 30, 40)));
    }

    [Fact]
    internal void CapturePreviewLogMessage_RectOrEmpty_未検出のPixelRectは空文字にする()
    {
        // 「未検出は空文字」という正規化を単一所有する
        Assert.Equal("", CapturePreviewLogMessage.RectOrEmpty(null));
        Assert.Equal("10,20,30,40", CapturePreviewLogMessage.RectOrEmpty(new EsRectInt(10, 20, 30, 40)));
    }

    /// <summary>ズーム率のログ用の丸めが四捨五入であることを検証する。</summary>
    /// <param name="value">丸める前の値。</param>
    /// <param name="expected">期待する整数。</param>
    [Theory]
    [InlineData(50.0, 50)]
    [InlineData(61.6, 62)]
    [InlineData(61.4, 61)]
    [InlineData(109.5, 110)]
    [InlineData(0.4, 0)]
    internal void Percent_四捨五入する(double value, int expected)
        => Assert.Equal(expected, CapturePreviewLogMessage.Percent(value));

    /// <summary>ズームイン・ズームアウトの書式が両 UI 共通の形式であることを検証する。</summary>
    [Fact]
    internal void ズーム操作の書式が期待どおりである()
    {
        var logger = new RecordingLogger();

        logger.LogZoomIn(current: 50, target: 62);
        logger.LogZoomOut(current: 62, target: 50);
        logger.LogZoomInWheel(current: 100, target: 110);
        logger.LogZoomOutWheel(current: 110, target: 100);

        Assert.Equal("[CapturePreview] ZoomIn: 50% -> 62%", logger.Messages[0]);
        Assert.Equal("[CapturePreview] ZoomOut: 62% -> 50%", logger.Messages[1]);
        Assert.Equal("[CapturePreview] ZoomInWheel: 100% -> 110%", logger.Messages[2]);
        Assert.Equal("[CapturePreview] ZoomOutWheel: 110% -> 100%", logger.Messages[3]);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    internal void 無効ロガーでも安全に実行される()
    {
        var logger = NullLogger.Instance;

        logger.LogZoomIn(50, 62);
        logger.LogZoomOut(62, 50);
        logger.LogZoomInWheel(100, 110);
        logger.LogZoomOutWheel(110, 100);
    }
}


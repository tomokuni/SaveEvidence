using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CaptureSession.Tests;

/// <summary><see cref="CaptureLogMessage"/> / <see cref="CaptureLogFormat"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>ログの書式（プレフィックス・項目名）と、ロガー未設定でも安全に呼べることを固定する。</remarks>
public class CaptureLogMessageTests
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
    public void LogCaptureStart_種別を含む書式で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCaptureStart("AreaSelect");

        Assert.Equal("[CaptureSession] CaptureStart: type=AreaSelect", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogCaptureFinished_確定時は完了と結果の2件を出力する()
    {
        // 「結果が確定したときにどのログをどの順序で出すか」をコアが単一所有する
        var logger = new RecordingLogger();
        using var image = EsBitmap.Create(20, 10);

        logger.LogCaptureFinished(new CaptureResult(image, new EsRectInt(1, 2, 20, 10)));

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[CaptureSession] CaptureCompleted: bounds=1,2,20,10", logger.Messages[0]);
        Assert.Equal("[CaptureSession] Result: bitmap=20x10 bounds=1,2,20,10", logger.Messages[1]);
    }

    [Fact]
    public void LogCaptureFinished_キャンセル時はキャンセル1件を出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCaptureFinished(null);

        Assert.Equal("[CaptureSession] CaptureCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogCaptureFinished_画像が無い結果もキャンセルとして扱う()
    {
        // 確定とキャンセルの区別は画像の有無で行う（0x0 の空結果を確定として記録しない）
        var logger = new RecordingLogger();

        logger.LogCaptureFinished(new CaptureResult(null, default));

        Assert.Equal("[CaptureSession] CaptureCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogCropStart_元画像のサイズを書式で出力する()
    {
        var logger = new RecordingLogger();
        using var image = EsBitmap.Create(1920, 1080);

        logger.LogCropStart(image);

        Assert.Equal("[CaptureSession] CropStart: source=1920x1080", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogCropFinished_切り出せた場合は確定を1件出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCropFinished(new EsRectInt(10, 20, 30, 40));

        Assert.Equal("[CaptureSession] CropCompleted: bounds=10,20,30,40", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogCropFinished_切り出せない場合はキャンセルとして出力する()
    {
        // 「退化した選択はキャンセルとして記録する」規則をコアが単一所有する
        // （UI が空判定とログの呼び分けを書くと、同じ操作で UI ごとに異なるログになる）
        var logger = new RecordingLogger();

        logger.LogCropFinished(default);

        Assert.Equal("[CaptureSession] CropCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogCropCancelled_プレフィックスのみ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCropCancelled();

        Assert.Equal("[CaptureSession] CropCancelled", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogSettingApplied_プレフィックスのみ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingApplied();

        Assert.Equal("[CaptureSession] SettingApplied", Assert.Single(logger.Messages));
    }

    [Fact]
    public void 全ログメソッドが無効ロガーでも安全に実行できる()
    {
        var logger = NullLogger.Instance;
        using var image = EsBitmap.Create(1, 1);

        logger.LogCaptureStart("AreaSelect");
        logger.LogCaptureFinished(new CaptureResult(image, new EsRectInt(0, 0, 1, 1)));
        logger.LogCaptureFinished(null);
        logger.LogCropStart(image);
        logger.LogCropFinished(new EsRectInt(0, 0, 1, 1));
        logger.LogCropFinished(default);
        logger.LogCropCancelled();
        logger.LogSettingApplied();
        logger.LogConfirmRequested("0,0,1,1");
        logger.LogCancelRequested();
        logger.LogVirtualScreen(new EsPointInt(0, 0), image);
        logger.LogVirtualScreen(new EsPointInt(1, 1), null);
        logger.LogHighlight("AreaSelect", true, "0,0,1,1", "t", "c");
    }

    [Fact]
    public void LogVirtualScreen_原点とサイズを含む書式で出力する()
    {
        var logger = new RecordingLogger();
        using var bitmap = EsBitmap.Create(3840, 2160);

        logger.LogVirtualScreen(new EsPointInt(-1920, -200), bitmap);

        Assert.Equal("[CaptureSession] VirtualScreen: origin=(-1920, -200) size=3840x2160", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogVirtualScreen_画像未取得はサイズ0として出力する()
    {
        // 「未取得は 0」という正規化をコアが単一所有する（呼び出し側で null 判定を書かない）
        var logger = new RecordingLogger();

        logger.LogVirtualScreen(new EsPointInt(10, 20), null);

        Assert.Equal("[CaptureSession] VirtualScreen: origin=(10, 20) size=0x0", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogConfirmRequested_矩形を含む書式で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogConfirmRequested("10,10,50,40");

        Assert.Equal("[CaptureSession] ConfirmRequested: rect=10,10,50,40", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogCancelRequested_プレフィックスのみ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCancelRequested();

        Assert.Equal("[CaptureSession] CancelRequested", Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogHighlight_種別と検出結果を含む書式で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogHighlight("WindowSelect", true, "100,50,800,600", "Sample", "Notepad");

        Assert.Equal(
            "[CaptureSession] Highlight: type=WindowSelect detected=True rect=100,50,800,600 title=Sample class=Notepad",
            Assert.Single(logger.Messages));
    }

    [Fact]
    public void CaptureLogFormat_Rect_論理矩形をx_y_w_hで整形する()
    {
        Assert.Equal("10,20,30,40", CaptureLogFormat.Rect(new EsRect(10, 20, 30, 40)));
    }

    [Fact]
    public void CaptureLogFormat_Rect_PixelRectをx_y_w_hで整形する()
    {
        Assert.Equal("10,20,30,40", CaptureLogFormat.Rect(new EsRectInt(10, 20, 30, 40)));
    }

    [Fact]
    public void CaptureLogFormat_RectOrEmpty_未検出のPixelRectは空文字にする()
    {
        // 「未検出は空文字」という正規化を単一所有する
        Assert.Equal("", CaptureLogFormat.RectOrEmpty(null));
        Assert.Equal("10,20,30,40", CaptureLogFormat.RectOrEmpty(new EsRectInt(10, 20, 30, 40)));
    }

    [Fact]
    public void CaptureLogFormat_Text_nullは空文字にする()
    {
        Assert.Equal("", CaptureLogFormat.Text(null));
        Assert.Equal("Sample", CaptureLogFormat.Text("Sample"));
    }
}

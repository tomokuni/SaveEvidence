using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Estable.CaptureSession.Tests;

/// <summary><see cref="CaptureHighlightSession"/> / <see cref="CaptureHighlight"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「検出結果 → ハイライト状態」の決定（未検出の正規化・ドメイン座標への変換・表示名の変化）と、<br/>
/// 検出結果のログを固定する（両 UI が同じ手順・同じログになることの根拠）。
/// </remarks>
public class CaptureHighlightTests
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

    /// <summary>検出結果を生成する。</summary>
    /// <param name="bounds">検出矩形（画面物理座標）。</param>
    /// <param name="title">表示名。</param>
    /// <param name="className">クラス名。</param>
    /// <returns>検出結果。</returns>
    private static ElementFinderResults Detected(EsPixelRect bounds, string? title = "Sample", string? className = "Notepad")
        => new(bounds, IsDetected: true, Title: title, ClassName: className);

    [Fact]
    public void Update_検出時はドメイン座標の矩形と表示名を返す()
    {
        var session = new CaptureHighlightSession();

        var result = session.Update(
            CaptureType.WindowSelect, Detected(new EsPixelRect(2020, 100, 800, 600)), new EsPixelPoint(1920, 0));

        Assert.True(result.HasHighlight);
        Assert.Equal(new EsRect(100, 100, 800, 600), result.DomainRect);
        Assert.Equal("Sample", session.Title);
        Assert.Equal(new EsRect(100, 100, 800, 600), session.DomainRect);
    }

    [Fact]
    public void Update_未検出は矩形nullと空文字へ正規化する()
    {
        // 「未検出は矩形なし・空文字」という正規化をコアが単一所有する
        var session = new CaptureHighlightSession();

        var result = session.Update(CaptureType.WindowSelect, default, default);

        Assert.False(result.HasHighlight);
        Assert.Null(result.DomainRect);
        Assert.Equal("", session.Title);
        Assert.Null(session.DomainRect);
    }

    [Fact]
    public void Update_タイトルの変化を返す()
    {
        var session = new CaptureHighlightSession();

        var first = session.Update(CaptureType.WindowSelect, Detected(new EsPixelRect(0, 0, 10, 10), "A"), default);
        var same = session.Update(CaptureType.WindowSelect, Detected(new EsPixelRect(20, 20, 10, 10), "A"), default);
        var changed = session.Update(CaptureType.WindowSelect, Detected(new EsPixelRect(20, 20, 10, 10), "B"), default);

        Assert.True(first.TitleChanged);  // 初期値（空文字）からの変化
        Assert.False(same.TitleChanged);  // 同じ表示名
        Assert.True(changed.TitleChanged); // 表示名が変化
    }

    [Fact]
    public void Update_未検出でタイトルが空へ戻る変化を返す()
    {
        var session = new CaptureHighlightSession();
        session.Update(CaptureType.WindowSelect, Detected(new EsPixelRect(0, 0, 10, 10), "A"), default);

        var result = session.Update(CaptureType.WindowSelect, default, default);

        Assert.True(result.TitleChanged);
        Assert.Equal("", session.Title);
    }

    [Fact]
    public void Update_表示名が同じ未検出の連続は変化なしを返す()
    {
        var session = new CaptureHighlightSession();

        session.Update(CaptureType.WindowSelect, default, default);
        var result = session.Update(CaptureType.WindowSelect, default, default);

        Assert.False(result.TitleChanged);
    }

    [Fact]
    public void Update_検出結果をログへ記録する()
    {
        var logger = new RecordingLogger();
        var session = new CaptureHighlightSession(logger);

        session.Update(CaptureType.WindowSelect, Detected(new EsPixelRect(2020, 100, 800, 600)), new EsPixelPoint(1920, 0));

        Assert.Equal(
            "[CaptureSession] Highlight: type=WindowSelect detected=True rect=100,100,800,600 title=Sample class=Notepad",
            Assert.Single(logger.Messages));
    }

    [Fact]
    public void Update_未検出もログへ記録する()
    {
        // 未検出は矩形が空文字になる（書式はコアが単一所有する）
        var logger = new RecordingLogger();
        var session = new CaptureHighlightSession(logger);

        session.Update(CaptureType.ScreenSelect, default, default);

        Assert.Equal(
            "[CaptureSession] Highlight: type=ScreenSelect detected=False rect= title= class=",
            Assert.Single(logger.Messages));
    }

    [Fact]
    public void Title_初期値は空文字()
    {
        // 情報ラベルの文言プロバイダが未検出時に空文字を読む（null を描画側へ渡さない）
        Assert.Equal("", new CaptureHighlightSession().Title);
    }

    [Fact]
    public void DomainRect_初期値はnull()
    {
        // 未検出の初期状態（確定対象なし・ラベルは設定の明示指定を使う）
        Assert.Null(new CaptureHighlightSession().DomainRect);
    }
}

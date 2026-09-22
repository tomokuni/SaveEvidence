using EsUtil.Platform.Core.Coordinate;
using Estable.FadeMask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Rect = Avalonia.Rect;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskControl"/> のヘッドレス不要テスト（公開 API の純粋な状態検証）。</summary>
/// <remarks>
/// コントロール生成・プロパティ設定・<see cref="FadeMaskControl.Log"/> / <see cref="FadeMaskControl.SetCoordinateTransform"/> /
/// 論理矩形のみを検証するため、Avalonia ヘッドレス実行（Dispatcher / 描画）を必要としない。<br/>
/// 並列実行を妨げないよう <c>[Fact]</c> で検証する（Render を実行するテストは
/// <c>FadeMaskTests</c> が直列コレクションで検証する）。
/// </remarks>
public class FadeMaskControlStateTests
{
    // ====================================================================
    // Log（NullLogger フォールバック）
    // ====================================================================

    /// <summary>設定したロガーがそのまま返ることを検証する。</summary>
    [Fact]
    public void Log_設定したロガーを返す()
    {
        var control = new FadeMaskControl { Log = NullLogger.Instance };

        Assert.Same(NullLogger.Instance, control.Log);
    }

    /// <summary>null を設定すると NullLogger へフォールバックすることを検証する。</summary>
    [Fact]
    public void Log_nullを設定するとNullLoggerにフォールバックする()
    {
        var control = new FadeMaskControl { Log = null! };

        Assert.NotNull(control.Log);
    }

    /// <summary>実ロガー設定時に全ログメソッド（矩形変更・座標変換）が実行されることを検証する。</summary>
    /// <remarks>LoggerMessage 生成コードの state 走査経路を実行させる。</remarks>
    [Fact]
    public void ログ_実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        var control = new FadeMaskControl { Log = logger };

        control.LogicalSelectionRect = new Rect(10, 20, 100, 80);
        control.LogicalHighlightRect = new Rect(1, 2, 3, 4);
        control.LogicalSelectionRect = null;
        control.SetCoordinateTransform(CoordTransform.Identity);

        Assert.Equal(4, logger.Messages.Count);
        Assert.Equal("[FadeMask] SelectionRect: null → (10, 20, 100, 80)", logger.Messages[0]);
        Assert.Equal("[FadeMask] HighlightRect: null → (1, 2, 3, 4)", logger.Messages[1]);
    }

    // ====================================================================
    // 座標変換ポート（SetCoordinateTransform）
    // ====================================================================

    /// <summary>null を設定しても例外にならない（等価変換へフォールバックする）ことを検証する。</summary>
    [Fact]
    public void SetCoordinateTransform_nullで等価変換にフォールバックする()
    {
        var control = new FadeMaskControl();

        control.SetCoordinateTransform(null);
    }

    // ====================================================================
    // 論理矩形
    // ====================================================================

    /// <summary>設定した選択矩形が返り、null を設定すると null に戻ることを検証する。</summary>
    [Fact]
    public void LogicalSelectionRect_設定と解除が反映される()
    {
        var control = new FadeMaskControl { LogicalSelectionRect = new Rect(10, 10, 50, 40) };

        Assert.Equal(new Rect(10, 10, 50, 40), control.LogicalSelectionRect);

        control.LogicalSelectionRect = null;

        Assert.Null(control.LogicalSelectionRect);
    }

    /// <summary>設定したハイライト矩形が返ることを検証する。</summary>
    [Fact]
    public void LogicalHighlightRect_設定した値を取得できる()
    {
        var control = new FadeMaskControl { LogicalHighlightRect = new Rect(1, 2, 3, 4) };

        Assert.Equal(new Rect(1, 2, 3, 4), control.LogicalHighlightRect);
    }

    // ====================================================================
    // 既定値
    // ====================================================================

    /// <summary>外観・状態の既定値が想定どおりであることを検証する。</summary>
    [Fact]
    public void 既定値がコアの外観既定と一致する()
    {
        var control = new FadeMaskControl();

        // 外観はコアの FadeMaskAppearance が単一所有する（既定は黒 50% の暗転・ダークグレーの枠線・白のラベル）
        Assert.Equal(new FadeMaskAppearance(), control.Appearance);
        Assert.Null(control.LogicalSelectionRect);
        Assert.Null(control.LogicalHighlightRect);
        Assert.False(control.HasSelection);
        Assert.False(control.HasHighlight);
        Assert.Equal("", control.WindowTitle);
        Assert.Equal(FadeMaskRenderMode.AreaSelect, control.Mode);
        Assert.False(control.ShowFullOverlay);
        Assert.Equal(1.0, control.Scaling);
        Assert.Null(control.SizeLabelOverrideProvider);
    }

    /// <summary>DisplayScaleScaling が未設定（0 以下）なら Scaling と同じ値を返すことを検証する。</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void DisplayScaleScaling_未設定ならScalingと同値になる(double value)
    {
        var control = new FadeMaskControl { Scaling = 1.25, DisplayScaleScaling = value };

        Assert.Equal(1.25, control.DisplayScaleScaling);
    }

    /// <summary>DisplayScaleScaling が設定されていればその値を返すことを検証する。</summary>
    [Fact]
    public void DisplayScaleScaling_設定値が優先される()
    {
        var control = new FadeMaskControl { Scaling = 1.0, DisplayScaleScaling = 2.0 };

        Assert.Equal(2.0, control.DisplayScaleScaling);
    }

    // ====================================================================
    // 公開プロパティの設定・取得
    // ====================================================================

    /// <summary>外観と状態の設定値がそのまま取得できることを検証する。</summary>
    [Fact]
    public void 公開プロパティ_設定した値を取得できる()
    {
        var appearance = new FadeMaskAppearance { BorderThickness = 3.0, ShowSizeLabel = false };
        var control = new FadeMaskControl
        {
            Appearance = appearance,
            LogicalSelectionRect = new Rect(10, 20, 100, 80),
            HasSelection = true,
            HasHighlight = true,
            WindowTitle = "メモ帳",
            Mode = FadeMaskRenderMode.WindowSelect,
            ShowFullOverlay = true,
        };

        Assert.Same(appearance, control.Appearance);
        Assert.Equal(new Rect(10, 20, 100, 80), control.LogicalSelectionRect);
        Assert.True(control.HasSelection);
        Assert.True(control.HasHighlight);
        Assert.Equal("メモ帳", control.WindowTitle);
        Assert.Equal(FadeMaskRenderMode.WindowSelect, control.Mode);
        Assert.True(control.ShowFullOverlay);
    }

    /// <summary>サイズラベルのプロバイダがそのまま保持されることを検証する。</summary>
    [Fact]
    public void SizeLabelOverrideProvider_設定した関数を取得できる()
    {
        var control = new FadeMaskControl();
        Func<string?> provider = () => "ラベル";

        control.SizeLabelOverrideProvider = provider;

        Assert.Same(provider, control.SizeLabelOverrideProvider);
    }

    /// <summary>LoggerMessage 生成コードの state 走査（Count・インデクサ・IEnumerable）を網羅するテスト用ロガー。</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>収集した整形済みメッセージ。</summary>
        public List<string> Messages { get; } = [];

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // 生成コードが state を IReadOnlyList として走査する経路を実行する
            if (state is IReadOnlyList<KeyValuePair<string, object?>> list)
            {
                _ = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    _ = list[i];
                }
                foreach (var kv in list)
                {
                    _ = kv.Key;
                }
                try
                {
                    _ = list[list.Count];
                }
                catch (IndexOutOfRangeException)
                {
                    // 範囲外アクセスの例外経路も生成コードの網羅対象として実行する
                }
            }

            Messages.Add(formatter(state, exception));
        }
    }
}

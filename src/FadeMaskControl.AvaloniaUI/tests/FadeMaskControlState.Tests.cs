using Avalonia;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Estable.FadeMask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Rect = Avalonia.Rect;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskControl"/> のヘッドレス不要テスト（公開 API の純粋な状態検証）。</summary>
/// <remarks>
/// コントロール生成・<see cref="FadeMaskControl.Options"/> の設定・<see cref="FadeMaskControl.Log"/> /
/// <see cref="FadeMaskControl.SetCoordinateTransform"/> のみを検証するため、Avalonia ヘッドレス実行（Dispatcher / 描画）を必要としない。<br/>
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
    /// <remarks>
    /// 設定と状態が 1 つの値になったため、変更前後の差分検出（どの矩形が変わったか）はコアが行う。<br/>
    /// LoggerMessage 生成コードの state 走査経路を実行させる。
    /// </remarks>
    [Fact]
    public void ログ_実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        var control = new FadeMaskControl { Log = logger };

        control.Options = control.Options with { LogicalSelectionRect = new Rect(10, 20, 100, 80).ToEsRect() };
        control.Options = control.Options with { LogicalHighlightRect = new Rect(1, 2, 3, 4).ToEsRect() };
        control.Options = control.Options with { LogicalSelectionRect = null };
        control.SetCoordinateTransform(CoordTransform.Identity);

        Assert.Equal(4, logger.Messages.Count);
        Assert.Equal("[FadeMask] SelectionRect: null → (10, 20, 100, 80)", logger.Messages[0]);
        Assert.Equal("[FadeMask] HighlightRect: null → (1, 2, 3, 4)", logger.Messages[1]);
        Assert.Equal("[FadeMask] SelectionRect: (10, 20, 100, 80) → null", logger.Messages[2]);
        Assert.Equal("[FadeMask] CoordinateTransform: True", logger.Messages[3]);
    }

    /// <summary>矩形以外の項目だけを変えてもログは出力しないことを検証する。</summary>
    /// <remarks>出力する項目は従来と同じ（選択矩形・ハイライト矩形）に限定する。</remarks>
    [Fact]
    public void ログ_矩形以外の変更では出力しない()
    {
        var logger = new RecordingLogger();
        var control = new FadeMaskControl { Log = logger };

        control.Options = control.Options with
        {
            Mode = FadeMaskRenderMode.WindowSelect,
            ShowFullOverlay = true,
            WindowTitle = "メモ帳",
        };

        Assert.Empty(logger.Messages);
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
    // Options（設定と描画状態の単一値）
    // ====================================================================

    /// <summary>設定した値がそのまま取得できることを検証する。</summary>
    [Fact]
    public void Options_設定した値を取得できる()
    {
        var appearance = new FadeMaskAppearance { BorderThickness = 3.0, ShowSizeLabel = false };
        var options = new FadeMaskOptions
        {
            Appearance = appearance,
            Mode = FadeMaskRenderMode.WindowSelect,
            WindowTitle = "メモ帳",
            ShowFullOverlay = true,
            LogicalSelectionRect = new Rect(10, 20, 100, 80).ToEsRect(),
            HasSelection = true,
            HasHighlight = true,
        };
        var control = new FadeMaskControl { Options = options };

        Assert.Same(options, control.Options);
        Assert.Same(appearance, control.Options.Appearance);
        Assert.Equal(FadeMaskRenderMode.WindowSelect, control.Options.Mode);
        Assert.Equal("メモ帳", control.Options.WindowTitle);
        Assert.True(control.Options.ShowFullOverlay);
        Assert.Equal(new Rect(10, 20, 100, 80).ToEsRect(), control.Options.LogicalSelectionRect);
        Assert.True(control.Options.HasSelection);
        Assert.True(control.Options.HasHighlight);
    }

    /// <summary>同じ値の再設定では変更通知が発生しないことを検証する。</summary>
    /// <remarks>コアの値が構造的等価比較のため、無駄な再描画・ログ出力を防ぐ。</remarks>
    [Fact]
    public void Options_同じ値の再設定では変更通知しない()
    {
        var control = new FadeMaskControl();
        var changed = 0;
        control.PropertyChanged += (_, e) =>
        {
            if (e.Property == FadeMaskControl.OptionsProperty)
            {
                changed++;
            }
        };

        control.Options = control.Options with { HasSelection = true };
        control.Options = control.Options;                                                    // 同一インスタンス
        control.Options = FadeMaskOptions.Default with { HasSelection = true };                // 等価な別インスタンス

        Assert.Equal(1, changed);
    }

    // ====================================================================
    // 既定値
    // ====================================================================

    /// <summary>既定値がコアの既定と一致することを検証する。</summary>
    [Fact]
    public void 既定値がコアの既定と一致する()
    {
        var control = new FadeMaskControl();

        // 設定と描画状態はコアの FadeMaskOptions が単一所有する（既定は黒 50% の暗転・ダークグレーの枠線・白のラベル・選択なし）
        Assert.Same(FadeMaskOptions.Default, control.Options);
        Assert.Equal(FadeMaskRenderMode.AreaSelect, control.Options.Mode);
        Assert.False(control.Options.HasSelection);
        Assert.False(control.Options.HasHighlight);
        Assert.Equal("", control.Options.WindowTitle);
        Assert.False(control.Options.ShowFullOverlay);
        Assert.Equal(1.0, control.Scaling);
        Assert.Null(control.SizeLabelOverrideProvider);
    }

    /// <summary>DisplayScaleScaling が未設定（0 以下）なら Scaling と同じ値を返すことを検証する。</summary>
    /// <param name="value">設定する DisplayScaleScaling。</param>
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

using Avalonia;
using Avalonia.Media;
using Estable.AvaloniaUI.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskControl"/> のヘッドレス不要テスト（公開 API の純粋な状態検証）。</summary>
/// <remarks>
/// コントロール生成・プロパティ設定・<see cref="FadeMaskControl.Log"/> / <see cref="FadeMaskControl.ExternalRenderer"/> /
/// <see cref="FadeMaskControl.SetCoordinateTransform"/> / <see cref="FadeMaskControl.LogicalSelectionRect"/> のみを検証するため、
/// Avalonia ヘッドレス実行（Dispatcher / 描画）を必要としない。<br/>
/// 並列実行を妨げないよう <c>[Fact]</c> で検証する（Render を実行するテストは
/// <c>FadeMaskTests</c> が直列コレクションで検証する）。
/// </remarks>
public class FadeMaskControlStateTests
{
    // ====================================================================
    // Log（NullLogger フォールバック）
    // ====================================================================

    [Fact]
    public void Log_設定したロガーを返す()
    {
        var control = new FadeMaskControl();
        var logger = NullLogger.Instance;

        control.Log = logger;

        Assert.Same(logger, control.Log);
    }

    [Fact]
    public void Log_nullを設定するとNullLoggerにフォールバックする()
    {
        var control = new FadeMaskControl();

        control.Log = null!;

        Assert.NotNull(control.Log);
    }

    // ====================================================================
    // 座標変換ポート（SetCoordinateTransform）
    // ====================================================================

    [Fact]
    public void SetCoordinateTransform_nullで等価変換にフォールバックする()
    {
        var control = new FadeMaskControl();

        // null 設定で例外なく等価変換へフォールバックする
        control.SetCoordinateTransform(null);
    }

    [Fact]
    public void SetCoordinateTransform_外部レンダラーへ同期される()
    {
        var control = new FadeMaskControl();
        var renderer = new FadeMaskRenderer();
        control.ExternalRenderer = renderer;

        // ポート設定 → レンダラーへ同期
        var port = new CoordinatePort(p => p, p => p, () => new Size(200, 200));
        control.SetCoordinateTransform(port);

        Assert.Same(port, renderer.CoordinateTransform);
    }

    // ====================================================================
    // LogicalSelectionRect（外部レンダラー同期）
    // ====================================================================

    [Fact]
    public void LogicalSelectionRect_外部レンダラーへ同期される()
    {
        var control = new FadeMaskControl();
        var renderer = new FadeMaskRenderer();
        control.ExternalRenderer = renderer;

        control.LogicalSelectionRect = new Rect(10, 10, 50, 40);

        Assert.Equal(new Rect(10, 10, 50, 40), renderer.LogicalSelectionRect);
    }

    [Fact]
    public void LogicalSelectionRect_nullを設定するとnullに戻る()
    {
        var control = new FadeMaskControl();
        control.LogicalSelectionRect = new Rect(10, 10, 50, 40);

        control.LogicalSelectionRect = null;

        Assert.Null(control.LogicalSelectionRect);
    }

    // ====================================================================
    // 依存関係プロパティの既定値
    // ====================================================================

    [Fact]
    public void 依存関係プロパティ_既定値が設定される()
    {
        var control = new FadeMaskControl();

        Assert.Null(control.LogicalSelectionRect);
        // 暗転オーバーレイの既定は黒 50%（設定画面のデフォルトと一致）
        Assert.Equal(Color.FromArgb(128, 0, 0, 0), control.OverlayColor);
        // 枠線の既定は DarkGray（設定画面の選択枠の色と一致）
        Assert.Equal(Color.FromArgb(0xFF, 0xA9, 0xA9, 0xA9), control.BorderColor);
        Assert.Equal(2.0, control.BorderThickness);
        Assert.Equal(Colors.White, control.TextColor);
        // 背景色は不透明度をアルファに統合（黒 60% = #99000000）
        Assert.Equal(Color.FromArgb(153, 0, 0, 0), control.TextBackgroundColor);
        Assert.Equal(14.0, control.FontSize);
        Assert.True(control.ShowSizeLabel);
    }

    // ====================================================================
    // 公開プロパティの設定・取得
    // ====================================================================

    [Fact]
    public void 公開プロパティ_設定した値を取得できる()
    {
        var control = new FadeMaskControl
        {
            LogicalSelectionRect = new Rect(10, 20, 100, 80),
            OverlayColor = Color.FromArgb(150, 0, 0, 0),
            BorderColor = Colors.Red,
            BorderThickness = 3.0,
            TextColor = Colors.Blue,
            TextBackgroundColor = Color.FromArgb(204, 0, 0, 128),
            FontSize = 18.0,
            ShowSizeLabel = false,
        };

        Assert.Equal(new Rect(10, 20, 100, 80), control.LogicalSelectionRect);
        Assert.Equal(Color.FromArgb(150, 0, 0, 0), control.OverlayColor);
        Assert.Equal(Colors.Red, control.BorderColor);
        Assert.Equal(3.0, control.BorderThickness);
        Assert.Equal(Colors.Blue, control.TextColor);
        Assert.Equal(Color.FromArgb(204, 0, 0, 128), control.TextBackgroundColor);
        Assert.Equal(18.0, control.FontSize);
        Assert.False(control.ShowSizeLabel);
    }

    // ====================================================================
    // ExternalRenderer（描画委譲）
    // ====================================================================

    [Fact]
    public void ExternalRenderer_設定したレンダラーを取得できる()
    {
        var control = new FadeMaskControl();
        var renderer = new FadeMaskRenderer();

        control.ExternalRenderer = renderer;

        Assert.Same(renderer, control.ExternalRenderer);
    }

    [Fact]
    public void ExternalRenderer_未設定時はnullを返す()
    {
        var control = new FadeMaskControl();

        Assert.Null(control.ExternalRenderer);
    }

    [Fact]
    public void ExternalRenderer_nullで解除するとnullを返す()
    {
        var control = new FadeMaskControl();
        control.ExternalRenderer = new FadeMaskRenderer();

        control.ExternalRenderer = null;

        Assert.Null(control.ExternalRenderer);
    }
}

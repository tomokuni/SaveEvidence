using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.MewUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskControl"/> の状態変更ログのテスト。</summary>
/// <remarks>
/// ログ定義（LoggerMessage の書式）はコアが単一所有するため、本テストはコントロールの
/// 状態変更がどのログをどの引数で出力するかを検証する。<br/>
/// 描画は行わないためウィンドウを必要としない（UI 初期化なしで並列実行できる）。
/// </remarks>
public class FadeMaskControlLogTests
{
    /// <summary>選択矩形の変更が変更前後で出力されることを検証する。</summary>
    [Fact]
    public void Log_選択矩形の変更を出力する()
    {
        var control = new FadeMaskControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.LogicalSelectionRect = new EsRect(10, 20, 30, 40);

        Assert.Equal("[FadeMask] SelectionRect: null → (10, 20, 30, 40)", Assert.Single(logger.Messages));
    }

    /// <summary>同じ値の再設定では出力しないことを検証する。</summary>
    [Fact]
    public void Log_同じ値の再設定では出力しない()
    {
        var control = new FadeMaskControl { LogicalSelectionRect = new EsRect(10, 20, 30, 40) };
        var logger = new RecordingLogger();
        control.Log = logger;

        control.LogicalSelectionRect = new EsRect(10, 20, 30, 40);
        control.LogicalSelectionRect = null;
        control.LogicalSelectionRect = null;

        // 変更（null）の 1 件のみが出力される
        Assert.Equal("[FadeMask] SelectionRect: (10, 20, 30, 40) → null", Assert.Single(logger.Messages));
    }

    /// <summary>ハイライト矩形の変更が出力されることを検証する。</summary>
    [Fact]
    public void Log_ハイライト矩形の変更を出力する()
    {
        var control = new FadeMaskControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.LogicalHighlightRect = new EsRect(1, 2, 3, 4);

        Assert.Equal("[FadeMask] HighlightRect: null → (1, 2, 3, 4)", Assert.Single(logger.Messages));
    }

    /// <summary>座標変換ポートの設定が出力されることを検証する。</summary>
    [Fact]
    public void Log_座標変換ポートの設定を出力する()
    {
        var control = new FadeMaskControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(100, 100)));
        control.SetCoordinateTransform(null);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[FadeMask] CoordinateTransform: True", logger.Messages[0]);
        Assert.Equal("[FadeMask] CoordinateTransform: False", logger.Messages[1]);
    }

    /// <summary>既定（null 設定）でも例外にならないことを検証する。</summary>
    [Fact]
    public void Log_既定はNullLoggerで例外にならない()
    {
        var control = new FadeMaskControl { Log = null! };

        control.LogicalSelectionRect = new EsRect(1, 2, 3, 4);
        control.LogicalHighlightRect = new EsRect(1, 2, 3, 4);
        control.SetCoordinateTransform(null);
    }
}

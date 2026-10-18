using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="WindowLayout"/> と <see cref="WindowMenu"/> の仕様テスト。</summary>
/// <remarks>
/// タイトル バーとナビゲーション パネルの位置、および標準メニューの項目は 4 つの UI 版で共通である。
/// </remarks>
public class WindowLayoutTests
{
    [Fact]
    internal void アプリアイコン_はナビアイコンと同じ大きさでツールバーと同じ高さに揃う()
    {
        // 大きさはナビゲーションのアイコンと同じ
        Assert.Equal(NavPanelMetrics.IconSize, WindowLayout.AppIconSize);

        // 中心の高さ = ツールバーのアイコンの中心
        var appIconCenter = WindowLayout.AppIconRowHeight / 2;
        var toolbarIconCenter = WindowLayout.ToolbarRowTop + (WindowLayout.ToolbarButtonHeight / 2);
        Assert.Equal(toolbarIconCenter, appIconCenter);

        // 列の幅は開閉アイコンと同じ（同じ列に中央揃えで置ける）
        Assert.Equal(WindowLayout.NavToggleWidth, WindowLayout.AppIconColumnWidth);
    }

    [Fact]
    internal void メニュー_は標準の並びで閉じるだけ区切りの前()
    {
        var items = WindowMenu.Items(isMaximized: false);

        Assert.Equal(
            [WindowMenuAction.Restore, WindowMenuAction.Move, WindowMenuAction.Size, WindowMenuAction.Minimize, WindowMenuAction.Maximize, WindowMenuAction.Close],
            items.Select(item => item.Action));
        Assert.Single(items, item => item.IsSeparatorBefore);
        Assert.Equal(WindowMenuAction.Close, items[^1].Action);
        Assert.True(items[^1].IsSeparatorBefore);
    }

    [Fact]
    internal void メニュー_は最大化中に元へ戻すが有効で最大化が無効()
    {
        var items = WindowMenu.Items(isMaximized: true);

        Assert.True(items.Single(item => item.Action == WindowMenuAction.Restore).IsEnabled);
        Assert.False(items.Single(item => item.Action == WindowMenuAction.Maximize).IsEnabled);
    }

    [Fact]
    internal void メニュー_は通常時に元へ戻すが無効()
    {
        var items = WindowMenu.Items(isMaximized: false);

        Assert.False(items.Single(item => item.Action == WindowMenuAction.Restore).IsEnabled);
        Assert.True(items.Single(item => item.Action == WindowMenuAction.Maximize).IsEnabled);
    }
}
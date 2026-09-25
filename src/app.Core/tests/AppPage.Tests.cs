using EsUtil.SaveEvidence.Enum;
using Xunit;

namespace EsUtil.SaveEvidence.Tests;

/// <summary><see cref="AppPage"/> の単体テスト。</summary>
/// <remarks>
/// ナビゲーションページの enum 定義を検証する。
/// </remarks>
public class AppPageTests
{
    [Fact]
    public void AppPage_全ページが定義されている()
    {
        var pages = System.Enum.GetValues<AppPage>();

        Assert.Equal(4, pages.Length);
        Assert.Contains(AppPage.Capture, pages);
        Assert.Contains(AppPage.FolderView, pages);
        Assert.Contains(AppPage.Undo, pages);
        Assert.Contains(AppPage.Settings, pages);
    }

    [Fact]
    public void AppPage_定義順がナビゲーション順と一致する()
    {
        // ナビパネルの並び順（キャプチャ → フォルダビュー → Undo 一覧 → 設定）と一致させる
        Assert.Equal(0, (int)AppPage.Capture);
        Assert.Equal(1, (int)AppPage.FolderView);
        Assert.Equal(2, (int)AppPage.Undo);
        Assert.Equal(3, (int)AppPage.Settings);
    }
}

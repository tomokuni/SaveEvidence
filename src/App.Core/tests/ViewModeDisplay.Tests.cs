using EsUtil.SaveEvidence.App.Core.ViewModels;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ViewModeDisplay"/> の単体テスト（表示モードの名前・範囲補正・件数テキスト）。</summary>
/// <remarks>
/// 表示モード（特大 / 大 / 中）の名前と範囲補正は FolderView と Undo 一覧で共有するため、<br/>
/// 境界値（負値・上限超過・int の最小最大）を含めて仕様を固定する。
/// </remarks>
public class ViewModeDisplayTests
{
    [Theory]
    [InlineData(-1, "")]
    [InlineData(0, "特大")]
    [InlineData(1, "大")]
    [InlineData(2, "中")]
    [InlineData(3, "")]
    [InlineData(int.MaxValue, "")]
    [InlineData(int.MinValue, "")]
    public void GetText_表示モードの名前を返す(int index, string expected)
        => Assert.Equal(expected, ViewModeDisplay.GetText(index));

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    [InlineData(int.MaxValue, 1)]
    public void ClampIndex_有効範囲外は既定の大へフォールバックする(int index, int expected)
        => Assert.Equal(expected, ViewModeDisplay.ClampIndex(index));

    [Theory]
    [InlineData(0, "0 個のアイテム")]
    [InlineData(1, "1 個のアイテム")]
    [InlineData(10, "10 個のアイテム")]
    public void GetItemCountText_アイテム数テキストを生成する(int count, string expected)
        => Assert.Equal(expected, ViewModeDisplay.GetItemCountText(count));

    [Fact]
    public void Count_有効なインデックスの個数と一致する()
    {
        Assert.Equal(3, ViewModeDisplay.Count);

        // 0 〜 Count-1 はすべて有効な名前を持ち、範囲補正でも変更されない
        for (var index = 0; index < ViewModeDisplay.Count; index++)
        {
            Assert.NotEqual("", ViewModeDisplay.GetText(index));
            Assert.Equal(index, ViewModeDisplay.ClampIndex(index));
        }

        // Count 以上は無効（名前なし・範囲補正で既定値へ）
        Assert.Equal("", ViewModeDisplay.GetText(ViewModeDisplay.Count));
        Assert.Equal(1, ViewModeDisplay.ClampIndex(ViewModeDisplay.Count));
    }
}

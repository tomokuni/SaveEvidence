using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="StatusBarTexts"/> の仕様テスト（ステータス バーの文言）。</summary>
/// <remarks>幅の基準（<see cref="StatusBarMetrics"/>）と実際の表示がずれないよう、接頭辞と値を 1 箇所で組み立てる。</remarks>
public class StatusBarTextsTests
{
    /// <summary>ポインタ位置の表示が接頭辞と座標になることを検証する。</summary>
    [Fact]
    internal void FormatPointer_接頭辞と座標を返す()
        => Assert.Equal("pt: 1920, 1080", StatusBarTexts.FormatPointer(1920, 1080));

    /// <summary>画像サイズの表示が接頭辞とサイズになることを検証する。</summary>
    [Fact]
    internal void FormatImageSize_接頭辞とサイズを返す()
        => Assert.Equal("size: 1920 x 1080", StatusBarTexts.FormatImageSize(1920, 1080));

    /// <summary>画像外の表示が接頭辞を含むことを検証する（画像サイズは空欄）。</summary>
    [Fact]
    internal void 値が無いときの表示は接頭辞を含む()
    {
        Assert.Equal("pt: -", StatusBarTexts.PointerUnavailableText);
        Assert.True(string.IsNullOrEmpty(StatusBarTexts.ImageSizeUnavailableText));
    }
}

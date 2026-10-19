using EsUtil.SaveEvidence.App.WinUI3.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="ThumbnailBackground"/> の単体テスト（UI スレッドへの反映の保留）。</summary>
/// <remarks>
/// <b>背景スレッドのまま UI を触らない</b>: ディスパッチャ キューを設定する前に生成が終わった場合、
/// その場で実行すると XAML の要素を別スレッドから触って例外になる。 反映は保留され、設定後に流れることを固定する。
/// </remarks>
public class ThumbnailBackgroundTests
{
    /// <summary>ディスパッチャ キューの設定前に届いた反映は、その場で実行せず保留する。</summary>
    [Fact]
    public void ディスパッチャの設定前は反映を保留する()
    {
        var applied = false;

        ThumbnailBackground.Post(() => applied = true);

        Assert.False(applied);
    }
}

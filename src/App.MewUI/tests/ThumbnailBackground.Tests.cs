using EsUtil.SaveEvidence.App.MewUI.Views;
using Xunit;

namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary><see cref="ThumbnailBackground"/> の単体テスト（UI スレッドへの反映の保留）。</summary>
/// <remarks>
/// <b>背景スレッドのまま UI を触らない</b>: 起動前（ディスパッチャ未初期化）に生成が終わった場合、
/// その場で実行すると UI を別スレッドから触って例外になる。 反映は保留され、起動後に流れることを固定する。
/// </remarks>
public class ThumbnailBackgroundTests
{
    /// <summary>ディスパッチャの初期化前に届いた反映は、その場で実行せず保留する。</summary>
    [Fact]
    public void ディスパッチャの初期化前は反映を保留する()
    {
        var applied = false;

        ThumbnailBackground.Post(() => applied = true);

        Assert.False(applied);
    }
}

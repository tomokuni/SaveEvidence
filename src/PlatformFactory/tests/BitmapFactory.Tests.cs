using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="BitmapFactory"/> の単体テスト。</summary>
public class BitmapFactoryTests
{
    [Fact]
    public void CreateEmpty_常にnullを返す()
    {
        // 0x0 の WriteableBitmap は生成できないため、キャンセル意図の null を返す
        Assert.Null(BitmapFactory.CreateEmpty());
    }
}

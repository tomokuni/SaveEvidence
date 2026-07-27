using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="BitmapFactory"/> の単体テスト。</summary>
[Collection("AvaloniaHeadless")]
public class BitmapFactoryTests
{
    [AvaloniaFact]
    public void CreateEmpty_例外なく実行され0x0の空Bitmapまたはnullを返す()
    {
        // 0x0 の WriteableBitmap が生成できる環境では空 Bitmap、できない環境では null を返す
        var result = BitmapFactory.CreateEmpty();

        if (result is not null)
        {
            Assert.Equal(0, result.PixelSize.Width);
            Assert.Equal(0, result.PixelSize.Height);
        }
    }
}

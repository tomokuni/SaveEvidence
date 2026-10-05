using Avalonia.Media;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Tests;

/// <summary>
/// <see cref="EsBitmap"/> の Avalonia 向け拡張（<see cref="EsBitmapAvaloniaExtensions"/>）の単体テスト。
/// </summary>
/// <remarks>
/// <c>Size()</c> / <c>Clear(AvaloniaColor)</c> は Avalonia の値型変換のみで完結し、<br/>
/// <c>WriteableBitmap</c> を生成しないためヘッドレスプラットフォームを必要としない<br/>
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// <c>WriteableBitmap</c> を伴う変換は <see cref="EsBitmapAvaloniaTests"/> で検証する。
/// </remarks>
public class EsBitmapAvaloniaExtensionsTests
{
    [Fact]
    internal void Size_ピクセルサイズを返す()
    {
        using var bmp = EsBitmap.Create(6, 8);

        Assert.Equal(new Avalonia.PixelSize(6, 8), bmp.Size());
    }

    [Fact]
    internal void Clear_AvaloniaColorで塗りつぶす()
    {
        using var bmp = EsBitmap.Create(4, 4);

        bmp.Clear(new Color(255, 255, 0, 0)); // 赤・不透明

        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    internal void Clear_部分透明アルファを保持する()
    {
        using var bmp = EsBitmap.Create(4, 4);

        // 50% アルファの赤（Avalonia Color は乗算して SKColor へ変換される）
        bmp.Clear(new Color(128, 255, 0, 0));

        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)128, px.A);
    }
}

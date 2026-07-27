using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="AppPixelData"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ピクセル配列操作のみを検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず<br/>
/// <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// Bitmap 変換（<see cref="AppPixelData.ToBitmap"/> / <see cref="AppPixelData.ToPixelData"/>）は<br/>
/// Avalonia ヘッドレスが必要なため直列コレクション側（AppPixelDataConversionTests）で検証する。
/// </remarks>
public class AppPixelDataTests
{
    [Fact]
    public void コンストラクタ_幅と高さとストライドとピクセルを保持する()
    {
        var data = new AppPixelData(10, 20, 40, new byte[40 * 20]);

        Assert.Equal(10, data.Width);
        Assert.Equal(20, data.Height);
        Assert.Equal(40, data.Stride);
        Assert.Equal(40 * 20, data.PixelData.Length);
    }

    [Fact]
    public void ToBitmap_サイズ0はnullを返す()
    {
        var data = new AppPixelData(0, 0, 0, []);

        Assert.Null(data.ToBitmap());
    }
}

using Estable.AvaloniaUI.Platform.Coordinate;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="DpiScaler"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// DIP 値 → 物理ピクセル変換のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <see cref="LoupeControl"/> で共用する。
/// </remarks>
public class DpiScalerTests
{
    [Fact]
    public void ScaleToPixel_スケーリングで端数は切り上げる()
    {
        // 100 DIP × 1.5 = 150（切り上げ不要）→ 150
        Assert.Equal(150, DpiScaler.ScaleToPixel(100, 1.5));
    }

    [Fact]
    public void ScaleToPixel_浮動小数誤差で誤切り上げしない()
    {
        // 100 × 1.1 = 110.00000000000001（浮動小数誤差）→ 誤らず 110
        Assert.Equal(110, DpiScaler.ScaleToPixel(100, 1.1));
    }

    [Fact]
    public void ScaleToPixel_端数を切り上げて物理ピクセルへ変換する()
    {
        // 33 × 1.1 = 36.3 → 37（切り上げ）
        Assert.Equal(37, DpiScaler.ScaleToPixel(33, 1.1));
    }

    [Fact]
    public void ScaleToPixel_0以下の値は1に正規化する()
    {
        // 0 × 1.0 = 0 → 最低 1
        Assert.Equal(1, DpiScaler.ScaleToPixel(0, 1.0));
        Assert.Equal(1, DpiScaler.ScaleToPixel(0.0, 1.0));
    }

    [Fact]
    public void ScaleToPixel_浮動小数のDIP値も正しく変換する()
    {
        // 160.4 × 1.0 = 160.4 → 161（切り上げ）
        Assert.Equal(161, DpiScaler.ScaleToPixel(160.4, 1.0));
    }
}

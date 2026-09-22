using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeAppearanceScaler"/> の角丸換算のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 座標ラベルの角丸半径（DIP）の物理ピクセル換算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 半径 0（角丸なし）は 0 のまま返す（最低 1 に切り上げない。四角形として描画するため）。
/// </remarks>
public class LoupeAppearanceScalerCornerRadiusTests
{
    /// <summary>角丸半径がスケーリング係数に追従する（切り上げ・最低 1）ことを検証する。</summary>
    /// <param name="cornerRadius">角丸半径（DIP）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="expected">期待する物理ピクセル半径。</param>
    [Theory]
    [InlineData(2, 1.0, 2)]
    [InlineData(2, 1.25, 3)]
    [InlineData(2, 2.0, 4)]
    [InlineData(1, 1.5, 2)]
    [InlineData(8, 1.0, 8)]
    public void ScaleCoordLabelCornerRadius_スケーリング係数に追従する(int cornerRadius, double scaling, int expected)
        => Assert.Equal(
            expected,
            LoupeAppearanceScaler.ScaleCoordLabelCornerRadius(new LoupeAppearance { CoordLabelCornerRadius = cornerRadius }, scaling));

    /// <summary>半径 0（角丸なし）は最低 1 へ切り上げず 0 のまま返すことを検証する。</summary>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void ScaleCoordLabelCornerRadius_半径0は0のまま返す(double scaling)
        => Assert.Equal(
            0,
            LoupeAppearanceScaler.ScaleCoordLabelCornerRadius(new LoupeAppearance { CoordLabelCornerRadius = 0 }, scaling));

    /// <summary>負の半径は外観の生成時に 0 へ補正され、角丸なしとして扱われることを検証する。</summary>
    [Fact]
    public void ScaleCoordLabelCornerRadius_負の値は補正済みの0として扱う()
        => Assert.Equal(
            0,
            LoupeAppearanceScaler.ScaleCoordLabelCornerRadius(new LoupeAppearance { CoordLabelCornerRadius = -5 }, 2.0));
}

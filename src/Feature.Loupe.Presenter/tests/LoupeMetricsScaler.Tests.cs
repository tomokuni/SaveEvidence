using Xunit;

namespace EsUtil.Feature.Loupe.Presenter.Tests;

/// <summary><see cref="LoupeMetricsScaler"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 寸法値（DIP）の物理ピクセル換算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 両 UI（Avalonia / MewUI）は本換算結果を使う（UI ごとに換算すると DPI で見た目がずれる）。<br/>
/// 中心座標ラベルは DIP のまま UI 要素として配置するため、本クラスの対象外である。<br/>
/// 安全域への補正は外観（<c>LoupeAppearance</c>）が生成時に適用済みであり、本クラスは換算だけを行う。
/// </remarks>
public class LoupeMetricsScalerTests
{
    /// <summary>枠線の太さがスケーリング係数に追従する（切り上げ・最低 1）ことを検証する。</summary>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="expected">期待する物理ピクセルサイズ。</param>
    [Theory]
    [InlineData(1.0, 2)]
    [InlineData(1.25, 3)]
    [InlineData(2.0, 4)]
    public void ScaleBorderThickness_スケーリング係数に追従する(double scaling, int expected)
        => Assert.Equal(expected, LoupeMetricsScaler.ScaleBorderThickness(2, scaling));

    /// <summary>枠線の太さ 0 でも最低 1 ピクセルになることを検証する。</summary>
    [Fact]
    public void ScaleBorderThickness_0は最低1ピクセルになる()
        => Assert.Equal(1, LoupeMetricsScaler.ScaleBorderThickness(0, 1.0));
}

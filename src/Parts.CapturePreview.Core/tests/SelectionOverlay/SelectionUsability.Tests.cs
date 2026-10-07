using Xunit;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.SelectionOverlay;

/// <summary><see cref="SelectionUsability"/> の単体テスト（選択矩形の可否判定）。</summary>
/// <remarks>UI 非依存の判定のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class SelectionUsabilityTests
{
    /// <summary>幅と高さが正の場合のみ選択として成立することを検証する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="expected">選択として成立する場合は true。</param>
    [Theory]
    [InlineData(30, 40, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 10, false)]
    [InlineData(10, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(-1, 10, false)]
    [InlineData(10, -1, false)]
    [InlineData(-10, -20, false)]
    internal void 判定_幅と高さがともに正の場合のみ選択として成立する(double width, double height, bool expected)
        => Assert.Equal(expected, SelectionUsability.IsUsable(width, height));

    /// <summary>端数のある正の寸法が選択として成立することを検証する。</summary>
    [Fact]
    internal void 判定_端数のある正の寸法は選択として成立する()
        => Assert.True(SelectionUsability.IsUsable(0.5, 0.5));
}

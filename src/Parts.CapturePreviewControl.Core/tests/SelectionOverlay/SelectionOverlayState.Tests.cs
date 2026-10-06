using Xunit;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOverlayState"/> の単体テスト（モードから導出される状態）。</summary>
/// <remarks>純粋な分岐のみのため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class SelectionOverlayStateTests
{
    /// <summary>選択矩形・全面暗転・破線の可否がモードに対応することを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">選択矩形を使用する場合は true。</param>
    [Theory]
    [InlineData(SelectionOverlayMode.Crop, true)]
    [InlineData(SelectionOverlayMode.AreaSelect, true)]
    [InlineData(SelectionOverlayMode.ScreenSelect, false)]
    [InlineData(SelectionOverlayMode.WindowSelect, false)]
    internal void 解決_選択矩形を使うモードでは選択矩形と全面暗転と破線が有効になる(SelectionOverlayMode mode, bool expected)
    {
        var state = SelectionOverlayState.Resolve(mode);

        Assert.Equal(expected, state.UsesSelection);
        Assert.Equal(expected, state.ShowFullOverlay);
        Assert.Equal(expected, state.ShowSelectionOverlay);
    }

    /// <summary>選択矩形を使うモードでのみルーペを表示することを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">ルーペを表示する場合は true。</param>
    [Theory]
    [InlineData(SelectionOverlayMode.Crop, true)]
    [InlineData(SelectionOverlayMode.AreaSelect, true)]
    [InlineData(SelectionOverlayMode.ScreenSelect, false)]
    [InlineData(SelectionOverlayMode.WindowSelect, false)]
    internal void ルーペ表示_選択矩形を使うモードでは画像と中心の有無に応じて可否が決まる(SelectionOverlayMode mode, bool expected)
        => Assert.Equal(expected, SelectionOverlayState.Resolve(mode).IsLoupeVisible(hasSource: true, hasCenter: true));

    /// <summary>拡大元画像または中心が未設定の場合はルーペを表示しないことを検証する。</summary>
    /// <param name="hasSource">拡大元画像が設定されているかどうか。</param>
    /// <param name="hasCenter">ルーペ中心が設定されているかどうか。</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    internal void ルーペ表示_画像または中心が未設定では選択矩形を使うモードでも表示しない(bool hasSource, bool hasCenter)
        => Assert.False(SelectionOverlayState
            .Resolve(SelectionOverlayMode.AreaSelect)
            .IsLoupeVisible(hasSource, hasCenter));

    /// <summary>未定義の値でも例外にならず選択矩形を使わない扱いになることを検証する。</summary>
    [Fact]
    internal void 解決_未定義の値は選択矩形を使わない扱いになる()
    {
        var state = SelectionOverlayState.Resolve((SelectionOverlayMode)999);

        Assert.False(state.UsesSelection);
        Assert.False(state.ShowFullOverlay);
    }
}

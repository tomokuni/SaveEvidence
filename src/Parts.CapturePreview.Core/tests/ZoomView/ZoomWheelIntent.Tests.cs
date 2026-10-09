using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.ZoomView;

/// <summary><see cref="ZoomWheelIntent"/> の単体テスト（ホイール入力の解釈）。</summary>
/// <remarks>純粋な分岐のみのため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class ZoomWheelIntentTests
{
    /// <summary>Ctrl 併用はズームとして解釈することを検証する。</summary>
    /// <param name="deltaY">縦方向の回転量。</param>
    /// <param name="expectedZoomIn">拡大の場合は true。</param>
    [Theory]
    [InlineData(1.0, true)]
    [InlineData(-1.0, false)]
    [InlineData(0.0, false)]
    internal void 解決_Ctrl併用はズームとして解釈する(double deltaY, bool expectedZoomIn)
    {
        var intent = ZoomWheelIntent.Resolve(isControlPressed: true, isShiftPressed: false, deltaY);

        Assert.True(intent.IsZoom);
        Assert.Equal(expectedZoomIn, intent.ZoomIn);
        // ズームでは横スクロールの指定は使わない
        Assert.False(intent.ScrollHorizontal);
    }

    /// <summary>Ctrl なしはパンとして解釈し、Shift で横方向になることを検証する。</summary>
    /// <param name="isShiftPressed">Shift が押されているかどうか。</param>
    /// <param name="expectedHorizontal">横方向の場合は true。</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    internal void 解決_Ctrlなしはパンとして解釈する(bool isShiftPressed, bool expectedHorizontal)
    {
        var intent = ZoomWheelIntent.Resolve(isControlPressed: false, isShiftPressed, deltaY: 1.0);

        Assert.False(intent.IsZoom);
        Assert.False(intent.ZoomIn);
        Assert.Equal(expectedHorizontal, intent.ScrollHorizontal);
    }

    /// <summary>Ctrl と Shift の同時押しではズームを優先することを検証する。</summary>
    [Fact]
    internal void 解決_Ctrlが優先される()
    {
        var intent = ZoomWheelIntent.Resolve(isControlPressed: true, isShiftPressed: true, deltaY: 1.0);

        Assert.True(intent.IsZoom);
        Assert.False(intent.ScrollHorizontal);
    }
}

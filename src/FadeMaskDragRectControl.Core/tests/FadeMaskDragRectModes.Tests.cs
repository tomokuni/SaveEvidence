using Estable.DragRect.Decoration;
using Xunit;

namespace Estable.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectModes"/> の単体テスト（モードから導出される状態）。</summary>
/// <remarks>純粋な分岐のみのため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class FadeMaskDragRectModesTests
{
    /// <summary>各モードが対応する暗転マスクの描画モードへ変換されることを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">期待する描画モード。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, OuterDecoRenderMode.Crop)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, OuterDecoRenderMode.AreaSelect)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, OuterDecoRenderMode.ScreenSelect)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, OuterDecoRenderMode.WindowSelect)]
    public void 解決_モードに対応する描画モードを返す(FadeMaskDragRectMode mode, OuterDecoRenderMode expected)
        => Assert.Equal(expected, FadeMaskDragRectModes.Resolve(mode).RenderMode);

    /// <summary>選択矩形・全面暗転・破線の可否がモードに対応することを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">選択矩形を使用する場合は true。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, false)]
    public void 解決_選択矩形を使うモードでは選択矩形と全面暗転と破線が有効になる(FadeMaskDragRectMode mode, bool expected)
    {
        var state = FadeMaskDragRectModes.Resolve(mode);

        Assert.Equal(expected, state.UsesSelection);
        Assert.Equal(expected, state.ShowFullOverlay);
        Assert.Equal(expected, state.ShowSelectionOverlay);
    }

    /// <summary>選択矩形を使うモードでのみルーペを表示することを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">ルーペを表示する場合は true。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, false)]
    public void ルーペ表示_選択矩形を使うモードでは画像と中心の有無に応じて可否が決まる(FadeMaskDragRectMode mode, bool expected)
        => Assert.Equal(expected, FadeMaskDragRectModes.Resolve(mode).IsLoupeVisible(hasSource: true, hasCenter: true));

    /// <summary>拡大元画像または中心が未設定の場合はルーペを表示しないことを検証する。</summary>
    /// <param name="hasSource">拡大元画像が設定されているかどうか。</param>
    /// <param name="hasCenter">ルーペ中心が設定されているかどうか。</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ルーペ表示_画像または中心が未設定では選択矩形を使うモードでも表示しない(bool hasSource, bool hasCenter)
        => Assert.False(FadeMaskDragRectModes
            .Resolve(FadeMaskDragRectMode.AreaSelect)
            .IsLoupeVisible(hasSource, hasCenter));

    /// <summary>未定義の値でも例外にならないことを検証する。</summary>
    [Fact]
    public void 解決_未定義の値はWindowSelectとして扱う()
    {
        var state = FadeMaskDragRectModes.Resolve((FadeMaskDragRectMode)999);

        Assert.Equal(OuterDecoRenderMode.WindowSelect, state.RenderMode);
        Assert.False(state.UsesSelection);
    }
}

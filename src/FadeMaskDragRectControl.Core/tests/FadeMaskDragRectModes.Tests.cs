using Estable.FadeMask;
using Xunit;

namespace Estable.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectModes"/> の単体テスト（モードと描画設定の対応）。</summary>
/// <remarks>純粋な分岐のみのため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class FadeMaskDragRectModesTests
{
    /// <summary>各モードが対応する暗転マスクの描画モードへ変換されることを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">期待する描画モード。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, FadeMaskRenderMode.Crop)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, FadeMaskRenderMode.AreaSelect)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, FadeMaskRenderMode.ScreenSelect)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, FadeMaskRenderMode.WindowSelect)]
    public void 変換_モードに対応する描画モードを返す(FadeMaskDragRectMode mode, FadeMaskRenderMode expected)
        => Assert.Equal(expected, FadeMaskDragRectModes.ToRenderMode(mode));

    /// <summary>選択矩形とルーペを使用するモードを判定することを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">選択矩形を使用する場合は true。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, false)]
    public void 判定_選択矩形とルーペを使用するモードを返す(FadeMaskDragRectMode mode, bool expected)
        => Assert.Equal(expected, FadeMaskDragRectModes.UsesDragRect(mode));

    /// <summary>未定義の値でも例外にならないことを検証する。</summary>
    [Fact]
    public void 変換_未定義の値はWindowSelectとして扱う()
    {
        var undefined = (FadeMaskDragRectMode)999;

        Assert.Equal(FadeMaskRenderMode.WindowSelect, FadeMaskDragRectModes.ToRenderMode(undefined));
        Assert.False(FadeMaskDragRectModes.UsesDragRect(undefined));
    }
}

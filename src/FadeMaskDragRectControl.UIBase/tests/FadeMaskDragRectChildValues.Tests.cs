using EsUtil.Feature.LoupeCore;
using EsUtil.Feature.MarqueeCore;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.MarqueeCore.Settings;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.UIBase.Tests;

/// <summary><see cref="FadeMaskDragRectChildValues"/> の単体テスト（設定とモードから子の値への解決）。</summary>
/// <remarks>UI 非依存の値の解決のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class FadeMaskDragRectChildValuesTests
{
    /// <summary>検証用の設定を生成する（部分変更の基点）。</summary>
    /// <returns>既定の設定。</returns>
    private static FadeMaskDragRectOptions CreateOptions() => FadeMaskDragRectOptions.Default;

    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop)]
    [InlineData(FadeMaskDragRectMode.AreaSelect)]
    public void 解決_選択矩形を使うモードでは全面暗転とハンドルと入力を有効にする(FadeMaskDragRectMode mode)
    {
        var values = FadeMaskDragRectChildValues.Resolve(CreateOptions(), mode, null);

        Assert.True(values.Deco.OuterShowFullOverlay);
        Assert.True(values.Marquee.ShowHandles);
        Assert.True(values.IsHitTestVisible);
    }

    [Theory]
    [InlineData(FadeMaskDragRectMode.ScreenSelect)]
    [InlineData(FadeMaskDragRectMode.WindowSelect)]
    public void 解決_選択矩形を使わないモードではハンドルと入力を無効にする(FadeMaskDragRectMode mode)
    {
        var values = FadeMaskDragRectChildValues.Resolve(CreateOptions(), mode, null);

        // ハイライトのみのモードでも暗転マスクは描画する（全面暗転は行わない）
        Assert.False(values.Deco.OuterShowFullOverlay);
        Assert.False(values.Marquee.ShowHandles);
        Assert.False(values.IsHitTestVisible);
    }

    /// <summary>未定義のモードが選択矩形を使わない扱いになることを検証する。</summary>
    /// <remarks>モードの解釈はコアが単一所有するため、本テストはその結果が値へ伝わることを確認する。</remarks>
    [Fact]
    public void 解決_未定義のモードは選択矩形を使わない扱いになる()
    {
        var values = FadeMaskDragRectChildValues.Resolve(CreateOptions(), (FadeMaskDragRectMode)999, null);

        Assert.False(values.Marquee.ShowHandles);
        Assert.False(values.IsHitTestVisible);
    }

    [Fact]
    public void 解決_ハイライト矩形は引数の値が反映される()
    {
        var highlight = new EsRect(10, 20, 30, 40);

        Assert.Equal(highlight, FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, highlight).Deco.OuterHighlightRect);
        Assert.Null(FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, null).Deco.OuterHighlightRect);
    }

    /// <summary>設定の値がそのまま写されることを検証する。</summary>
    /// <remarks>設定の差し替えで実行時状態（ハイライト矩形）以外は失われないことを確認する。</remarks>
    [Fact]
    public void 解決_設定の値はそのまま写される()
    {
        var options = CreateOptions() with
        {
            Marquee = MarqueeOptions.Default with { HandleSize = 14.0, MinSelectionSize = 9.0 },
            Loupe = LoupeOptions.Default with { RequestedSize = 240 },
            LoupeOffset = 32.0,
        };

        var values = FadeMaskDragRectChildValues.Resolve(options, FadeMaskDragRectMode.Crop, null);

        // 寸法は設定の値を保ち、モードから導出する項目（ハンドルの表示可否）だけが上書きされる
        Assert.Equal(14.0, values.Marquee.HandleSize);
        Assert.Equal(9.0, values.Marquee.MinSelectionSize);
        Assert.Equal(240, values.Loupe.RequestedSize);
        Assert.Equal(32.0, values.LoupeOffset);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, null);
        var b = FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, null);

        Assert.Equal(a, b);
        Assert.NotEqual(a, FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.WindowSelect, null));
    }
}

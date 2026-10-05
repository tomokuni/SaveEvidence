using EsUtil.Feature.Loupe;
using EsUtil.Feature.Marquee;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CaptureSession.UIBase.Tests;

/// <summary><see cref="FadeMaskDragRectChildValues"/> の単体テスト（設定とモードから子の値への解決）。</summary>
/// <remarks>UI 非依存の値の解決のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class FadeMaskDragRectChildValuesTests
{
    /// <summary>検証用の設定を生成する（部分変更の基点）。</summary>
    /// <returns>既定の設定。</returns>
    private static FadeMaskDragRectOptions CreateOptions() => FadeMaskDragRectOptions.Default;

    /// <summary>検証用の外観を生成する（ホストが与えた現在値の想定）。</summary>
    /// <returns>既定の外観。</returns>
    private static MarqueeAppearance CreateAppearance() => MarqueeAppearance.Default;

    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop)]
    [InlineData(FadeMaskDragRectMode.AreaSelect)]
    internal void 解決_選択矩形を使うモードでは全面暗転とハンドルと入力を有効にする(FadeMaskDragRectMode mode)
    {
        var values = FadeMaskDragRectChildValues.Resolve(CreateOptions(), mode, CreateAppearance());

        Assert.True(values.MarqueeAppearance.ShowFullOverlay);
        Assert.True(values.MarqueeAppearance.ShowHandles);
        Assert.True(values.IsHitTestVisible);
    }

    [Theory]
    [InlineData(FadeMaskDragRectMode.ScreenSelect)]
    [InlineData(FadeMaskDragRectMode.WindowSelect)]
    internal void 解決_選択矩形を使わないモードではハンドルと入力を無効にする(FadeMaskDragRectMode mode)
    {
        var values = FadeMaskDragRectChildValues.Resolve(CreateOptions(), mode, CreateAppearance());

        // ハイライトのみのモードでも暗転マスクは描画する（全面暗転は行わない）
        Assert.False(values.MarqueeAppearance.ShowFullOverlay);
        Assert.False(values.MarqueeAppearance.ShowHandles);
        Assert.False(values.IsHitTestVisible);
    }

    /// <summary>未定義のモードが選択矩形を使わない扱いになることを検証する。</summary>
    /// <remarks>モードの解釈はコアが単一所有するため、本テストはその結果が値へ伝わることを確認する。</remarks>
    [Fact]
    internal void 解決_未定義のモードは選択矩形を使わない扱いになる()
    {
        var values = FadeMaskDragRectChildValues.Resolve(CreateOptions(), (FadeMaskDragRectMode)999, CreateAppearance());

        Assert.False(values.MarqueeAppearance.ShowHandles);
        Assert.False(values.IsHitTestVisible);
    }

    /// <summary>ホストが与えた外観を保ち、モードから導出する項目だけを上書きすることを検証する。</summary>
    /// <remarks>ハイライト矩形は実行時状態のため本値に含めない（子コントロールが現在値を持ち続ける）。</remarks>
    [Fact]
    internal void 解決_外観は指定した値を保つ()
    {
        var appearance = MarqueeAppearance.Default with { OverlayColor = System.Drawing.Color.Red, ShowSizeLabel = false };

        var values = FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, appearance);

        Assert.Equal(System.Drawing.Color.Red, values.MarqueeAppearance.OverlayColor);
        Assert.False(values.MarqueeAppearance.ShowSizeLabel);
    }

    /// <summary>外観が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void 解決_外観がnullなら例外()
        => Assert.Throws<ArgumentNullException>(
            () => FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, null!));

    /// <summary>設定の値がそのまま写されることを検証する。</summary>
    /// <remarks>設定の差し替えで実行時状態（外観・ハイライト矩形）を失わないことを確認する。</remarks>
    [Fact]
    internal void 解決_設定の値はそのまま写される()
    {
        var options = CreateOptions() with
        {
            Marquee = MarqueeOptions.Default with { HandleSize = 14.0, MinSelectionSize = 9.0 },
            Loupe = LoupeOptions.Default with { RequestedSize = 240 },
            LoupeOffset = 32.0,
        };

        var values = FadeMaskDragRectChildValues.Resolve(options, FadeMaskDragRectMode.Crop, CreateAppearance());

        // 寸法は設定の値を保ち、モードから導出する項目（ハンドルの表示可否）だけが上書きされる
        Assert.Equal(14.0, values.Marquee.HandleSize);
        Assert.Equal(9.0, values.Marquee.MinSelectionSize);
        Assert.Equal(240, values.Loupe.RequestedSize);
        Assert.Equal(32.0, values.LoupeOffset);
    }

    [Fact]
    internal void 等価性は値で判定される()
    {
        var a = FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, CreateAppearance());
        var b = FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.Crop, CreateAppearance());

        Assert.Equal(a, b);
        Assert.NotEqual(a, FadeMaskDragRectChildValues.Resolve(CreateOptions(), FadeMaskDragRectMode.WindowSelect, CreateAppearance()));
    }
}


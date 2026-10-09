using EsUtil.Feature.Loupe;
using EsUtil.Feature.Marquee;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOverlayOptions"/> の単体テスト（既定値・部分変更）。</summary>
/// <remarks>UI 非依存の値型のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class SelectionOverlayOptionsTests
{
    /// <summary>既定値が内包コントロールの既定値と一致することを検証する。</summary>
    [Fact]
    internal void 既定値_内包コントロールの既定値を用いる()
    {
        var options = SelectionOverlayOptions.Default;

        // 外側の装飾の外観も選択矩形の設定が単一所有する
        Assert.Equal(MarqueeOptions.Default, options.Marquee);
        Assert.Equal(LoupeOptions.Default, options.Loupe);
        // ホスト固有の付加情報（操作・情報ラベル）の外観は専用の値が単一所有する
        Assert.Equal(SelectionOverlayLabelOptions.Default, options.Labels);
        Assert.Equal(DrawingColor.White, options.SelectionRectColorA);
        Assert.Equal(DrawingColor.Black, options.SelectionRectColorB);
        Assert.Equal(20.0, options.LoupeOffset);
    }

    /// <summary>付加情報ラベルの既定値が要件（表示し、ホストの配色を使う）を満たすことを検証する。</summary>
    [Fact]
    internal void 既定値_付加情報ラベルは表示して配色と寸法を持つ()
    {
        var labels = SelectionOverlayLabelOptions.Default;

        Assert.True(labels.ShowOperationLabel);
        Assert.Equal(12.0, labels.OperationFontSize);
        Assert.Equal(DrawingColor.FromArgb(255, 220, 220, 220), labels.OperationColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), labels.OperationBackgroundColor);
        Assert.True(labels.ShowInfoLabel);
        Assert.Equal(14.0, labels.InfoFontSize);
        Assert.Equal(DrawingColor.White, labels.InfoColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), labels.InfoBackgroundColor);
    }

    /// <summary>新しいインスタンスも既定値に一致することを検証する。</summary>
    [Fact]
    internal void 既定値_新規生成はDefaultと等しい()
        => Assert.Equal(SelectionOverlayOptions.Default, new SelectionOverlayOptions());

    /// <summary>部分変更が他の項目を維持することを検証する。</summary>
    [Fact]
    internal void 部分変更_with式は指定した項目だけを差し替える()
    {
        var changed = SelectionOverlayOptions.Default with
        {
            LoupeOffset = 32.0,
            SelectionRectColorA = DrawingColor.Lime,
        };

        Assert.Equal(32.0, changed.LoupeOffset);
        Assert.Equal(DrawingColor.Lime, changed.SelectionRectColorA);
        // 指定していない項目は既定値を維持する
        Assert.Equal(SelectionOverlayOptions.Default.Loupe, changed.Loupe);
        Assert.Equal(DrawingColor.Black, changed.SelectionRectColorB);
    }

    /// <summary>装飾の設定である項目が本値に含まれないことを検証する。</summary>
    /// <remarks>暗転マスク・外枠の外観と表示可否は、提示層の <c>MarqueeAppearance</c> が単一所有する。</remarks>
    [Fact]
    internal void 装飾_選択矩形の設定とは別の値が単一所有する()
    {
        var options = SelectionOverlayOptions.Default with
        {
            Marquee = MarqueeOptions.Default with
            {
                MinSelectionSize = 12.0,
                ClampToSourceBounds = false,
            },
        };

        // 本値が持つのは選択の判断に必要な設定（寸法・対話パラメータ）とルーペ・ラベル・破線の値だけである
        Assert.Equal(12.0, options.Marquee.MinSelectionSize);
        Assert.False(options.Marquee.ClampToSourceBounds);
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.Marquee.HandleSize);
    }

    /// <summary>等価性が値で判定されることを検証する。</summary>
    [Fact]
    internal void 等価性_同じ値の設定は等しい()
    {
        var a = SelectionOverlayOptions.Default with { LoupeOffset = 24.0 };
        var b = SelectionOverlayOptions.Default with { LoupeOffset = 24.0 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, SelectionOverlayOptions.Default);
    }
}

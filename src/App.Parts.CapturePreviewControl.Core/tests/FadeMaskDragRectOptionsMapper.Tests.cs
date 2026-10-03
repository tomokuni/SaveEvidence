using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary><see cref="FadeMaskDragRectOptionsMapper"/> の単体テスト（設定項目と外観の対応）。</summary>
/// <remarks>UI 非依存の変換のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class FadeMaskDragRectOptionsMapperTests
{
    /// <summary>全メンバー non-null の既定設定（ToNonNull 済み）を生成する。</summary>
    /// <returns>既定の設定。</returns>
    private static CaptureSettings CreateSettings() => new CaptureSettings().ToNonNull();

    [Fact]
    public void 変換_選択矩形の設定が反映される()
    {
        var settings = CreateSettings() with
        {
            MinSelectionSize = 5,
            HandleSize = 12.0,
            HandleHitRadius = 15.0,
            ClampToSourceBounds = false,
            MarkerLength = 20.0,
        };

        var options = settings.ToFadeMaskDragRectOptions();
        var marquee = options.Marquee;

        Assert.Equal(5, marquee.MinSelectionSize);
        Assert.Equal(12.0, marquee.HandleSize);
        Assert.Equal(15.0, marquee.HandleHitRadius);
        Assert.False(marquee.ClampToSourceBounds);
        Assert.Equal(20.0, marquee.MarkerLength);
    }

    /// <summary>枠線の太さ・表示可否は本変換の対象外であることを検証する。</summary>
    /// <remarks>
    /// 描画にだけ必要な設定（色・枠線の太さ・表示可否）は、提示層の <c>MarqueeAppearance</c> が単一所有する<br/>
    /// （本変換はコアの設定だけを組み立て、Core → UIBase の逆流を作らない）。
    /// </remarks>
    [Fact]
    public void 変換_枠線と表示可否は本変換の対象外である()
    {
        var settings = CreateSettings() with
        {
            BorderColor = DrawingColor.Red,
            BorderThickness = 1.0,
            SizeLabelVisible = false,
            ShowCoordLabel = false,
        };

        var options = settings.ToFadeMaskDragRectOptions();

        // 本変換が組み立てるのはコアの設定（選択の判断・ルーペの計算に必要な値）だけである
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.Marquee.HandleSize);
        Assert.Equal(20.0, options.LoupeOffset);
    }

    [Fact]
    public void 変換_操作ラベルと情報ラベルの外観がホストの描画設定へ反映される()
    {
        var settings = CreateSettings() with
        {
            ShowSubLines = false,
            SubLineFontSize = 13.0,
            SubLineColor = DrawingColor.Yellow,
            SubLineBackgroundColor = DrawingColor.FromArgb(179, 1, 2, 3),
            InfoLabelVisible = false,
            InfoLabelFontSize = 15.0,
            InfoLabelColor = DrawingColor.Cyan,
            InfoLabelBackgroundColor = DrawingColor.FromArgb(179, 4, 5, 6),
        };

        var labels = settings.ToFadeMaskDragRectOptions().Labels;

        // 操作（サブライン）・情報ラベルはホストが描くため、表示可否と外観を写像する（文言は写像しない）
        Assert.False(labels.ShowOperationLabel);
        Assert.Equal(13.0, labels.OperationFontSize);
        Assert.Equal(DrawingColor.Yellow, labels.OperationColor);
        Assert.Equal(DrawingColor.FromArgb(179, 1, 2, 3), labels.OperationBackgroundColor);
        Assert.False(labels.ShowInfoLabel);
        Assert.Equal(15.0, labels.InfoFontSize);
        Assert.Equal(DrawingColor.Cyan, labels.InfoColor);
        Assert.Equal(DrawingColor.FromArgb(179, 4, 5, 6), labels.InfoBackgroundColor);
    }

    [Fact]
    public void 変換_ルーペの設定が反映される()
    {
        var settings = CreateSettings() with
        {
            LoupeSize = 150,
            LoupeZoomLevel = 3,
            LoupeCrossColor = DrawingColor.Red,
            LoupePasteboardColor = DrawingColor.Black,
            LoupeFrameColor = DrawingColor.Gray,
            LoupeFrameThickness = 4,
            LoupeShowCoordLabel = false,
            LoupeCoordLabelColor = DrawingColor.Cyan,
            LoupeCoordLabelBackgroundColor = DrawingColor.FromArgb(153, 100, 0, 0),
            LoupeCoordLabelFontSize = 13.0,
        };

        var loupe = settings.ToFadeMaskDragRectOptions().Loupe;

        Assert.Equal(150, loupe.RequestedSize);
        Assert.Equal(3, loupe.ZoomLevel);
        // 計算に必要な値のみを写像する（色・枠線の太さ・表示可否は描画にだけ必要な設定のため、
        // 描画側（UI 層のホスト）が外観として設定する）
    }

    [Fact]
    public void 変換_破線の色とルーペのオフセットが反映される()
    {
        var settings = CreateSettings() with
        {
            LoupeSelectionRectColorA = DrawingColor.Lime,
            LoupeSelectionRectColorB = DrawingColor.Maroon,
            LoupeOffset = 32.0,
        };

        var options = settings.ToFadeMaskDragRectOptions();

        Assert.Equal(DrawingColor.Lime, options.SelectionRectColorA);
        Assert.Equal(DrawingColor.Maroon, options.SelectionRectColorB);
        Assert.Equal(32.0, options.LoupeOffset);
    }

    [Fact]
    public void 変換_設定がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => FadeMaskDragRectOptionsMapper.ToFadeMaskDragRectOptions(null!));
}

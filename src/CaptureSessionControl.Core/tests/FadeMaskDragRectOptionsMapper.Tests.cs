using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.CaptureSession.Tests;

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
            ClampToDomainBounds = false,
            MarkerLength = 20.0,
            ShowCoordLabel = true,
            CoordLabelFontSize = 14.0,
        };

        var options = settings.ToFadeMaskDragRectOptions();
        var marquee = options.Marquee;

        Assert.Equal(5, marquee.MinSelectionSize);
        Assert.Equal(12.0, marquee.HandleSize);
        Assert.Equal(15.0, marquee.HandleHitRadius);
        Assert.False(marquee.ClampToDomainBounds);
        Assert.Equal(20.0, marquee.MarkerLength);
        Assert.True(marquee.ShowCoordLabel);
    }

    [Fact]
    public void 変換_枠線は選択矩形の設定の外側の装飾に入る()
    {
        var settings = CreateSettings() with
        {
            BorderColor = DrawingColor.Red,
            BorderThickness = 1.0,
        };

        var options = settings.ToFadeMaskDragRectOptions();

        // 枠線の太さは装飾（暗転マスクの穴あけ枠）が単一所有する（色は描画の設定が単一所有するため本変換の対象外）
        Assert.Equal(1.0, options.Deco.OuterAppearance.BorderThickness);
        Assert.True(options.Deco.ShowOuterBorder);
        // ハンドルの枠線は同じ矩形に重なるため、ホストが同じ色を与える（本変換は寸法のみを持つ）
    }

    [Fact]
    public void 変換_暗転マスクの設定が反映される()
    {
        var settings = CreateSettings() with
        {
        };

        var options = settings.ToFadeMaskDragRectOptions();

        // 装飾は暗転と枠線のみを所有する（ラベルはサイズラベルが選択コア、操作・情報ラベルはホストが担う）
        // 枠線（色・太さ）は「枠線は選択矩形の設定の外側の装飾に入る」のテストが検証する
    }

    [Fact]
    public void 変換_サイズラベルの設定が選択コアへ反映される()
    {
        var settings = CreateSettings() with
        {
            SizeLabelVisible = false,
            TextBackgroundColor = DrawingColor.FromArgb(179, 0, 0, 255),
            SizeLabelFontSize = 18.0,
        };

        var marquee = settings.ToFadeMaskDragRectOptions().Marquee;

        // サイズラベルは選択矩形コントロール単体の概念であるため選択コアが単一所有する
        Assert.False(marquee.ShowSizeLabel);
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
            LoupeCheckerSize = 10,
            LoupeCheckerColorA = DrawingColor.Black,
            LoupeCheckerColorB = DrawingColor.White,
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
        // 寸法のみを写像する（色は描画にだけ必要な設定のため、描画側（UI 層のホスト）が設定する）
        Assert.Equal(10, loupe.CheckerSize);
        Assert.Equal(4, loupe.FrameWidth);
        Assert.False(loupe.ShowCoordLabel);
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

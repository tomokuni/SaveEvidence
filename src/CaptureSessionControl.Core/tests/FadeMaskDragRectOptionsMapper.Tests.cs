using Estable.FadeMaskDragRect;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.CaptureSession.Tests;

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
            HandleColor = DrawingColor.Blue,
            HandleSize = 12.0,
            HandleHitRadius = 15.0,
            ClampToDomainBounds = false,
            MarkerColor = DrawingColor.Green,
            MarkerLength = 20.0,
            ShowCoordLabel = true,
            CoordLabelColor = DrawingColor.Yellow,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0),
            CoordLabelFontSize = 14.0,
        };

        var options = settings.ToFadeMaskDragRectOptions();
        var dragRect = options.DragRect;

        Assert.Equal(5, dragRect.MinSelectionSize);
        Assert.Equal(DrawingColor.Blue, dragRect.HandleColor);
        Assert.Equal(12.0, dragRect.HandleSize);
        Assert.Equal(15.0, dragRect.HandleHitRadius);
        Assert.False(dragRect.ClampToDomainBounds);
        Assert.Equal(DrawingColor.Green, dragRect.MarkerColor);
        Assert.Equal(20.0, dragRect.MarkerLength);
        Assert.True(dragRect.ShowCoordLabel);
        Assert.Equal(DrawingColor.Yellow, dragRect.CoordLabelColor);
        Assert.Equal(14.0, dragRect.CoordLabelFontSize);
    }

    [Fact]
    public void 変換_枠線は暗転マスクの外観へ入り選択枠線へ導出される()
    {
        var settings = CreateSettings() with
        {
            BorderColor = DrawingColor.Red,
            BorderThickness = 1.0,
        };

        var options = settings.ToFadeMaskDragRectOptions();

        // 枠線は暗転マスクの外観が単一所有し、選択枠線へ導出される（同じ矩形に重なるため）
        Assert.Equal(DrawingColor.Red, options.Overlay.BorderColor);
        Assert.Equal(1.0, options.Overlay.BorderThickness);
        Assert.Equal(DrawingColor.Red, options.ResolveDragRectOptions().BorderColor);
        Assert.Equal(1.0, options.ResolveDragRectOptions().BorderThickness);
    }

    [Fact]
    public void 変換_暗転マスクの設定が反映される()
    {
        var settings = CreateSettings() with
        {
            OverlayColor = DrawingColor.FromArgb(128, 0, 0, 255),
            SizeLabelVisible = false,
            SizeLabelColor = DrawingColor.Yellow,
            TextBackgroundColor = DrawingColor.FromArgb(179, 0, 0, 255),
            SizeLabelFontSize = 18.0,
            ShowSubLines = false,
            SubLineFontSize = 13.0,
            SubLineColor = DrawingColor.Orange,
            SubLineBackgroundColor = DrawingColor.FromArgb(102, 0, 100, 0),
            InfoLabelVisible = false,
            InfoLabelColor = DrawingColor.Cyan,
            InfoLabelBackgroundColor = DrawingColor.FromArgb(128, 100, 0, 0),
            InfoLabelFontSize = 12.0,
        };

        var appearance = settings.ToFadeMaskDragRectOptions().Overlay;

        Assert.Equal(DrawingColor.FromArgb(128, 0, 0, 255), appearance.OverlayColor);
        Assert.False(appearance.ShowSizeLabel);
        Assert.Equal(DrawingColor.Yellow, appearance.SizeLabelColor);
        Assert.Equal(DrawingColor.FromArgb(179, 0, 0, 255), appearance.SizeLabelBackgroundColor);
        Assert.Equal(18.0, appearance.SizeLabelFontSize);
        Assert.False(appearance.ShowSubLines);
        Assert.Equal(13.0, appearance.SubLineFontSize);
        Assert.Equal(DrawingColor.Orange, appearance.SubLineColor);
        Assert.Equal(DrawingColor.FromArgb(102, 0, 100, 0), appearance.SubLineBackgroundColor);
        Assert.False(appearance.InfoLabelVisible);
        Assert.Equal(DrawingColor.Cyan, appearance.InfoLabelColor);
        Assert.Equal(DrawingColor.FromArgb(128, 100, 0, 0), appearance.InfoLabelBackgroundColor);
        Assert.Equal(12.0, appearance.InfoLabelFontSize);
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
        Assert.Equal(DrawingColor.Red, loupe.Appearance.CrossColor);
        Assert.Equal(10, loupe.Appearance.CheckerSize);
        Assert.Equal(DrawingColor.Black, loupe.Appearance.CheckerColorA);
        Assert.Equal(DrawingColor.White, loupe.Appearance.CheckerColorB);
        Assert.Equal(DrawingColor.Gray, loupe.Appearance.FrameColor);
        Assert.Equal(4, loupe.Appearance.FrameWidth);
        Assert.False(loupe.Appearance.ShowCoordLabel);
        Assert.Equal(DrawingColor.Cyan, loupe.Appearance.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 100, 0, 0), loupe.Appearance.CoordLabelBackgroundColor);
        Assert.Equal(13.0, loupe.Appearance.CoordLabelFontSize);
    }

    [Fact]
    public void 変換_市松模様の下地色は引数で与える()
    {
        var settings = CreateSettings();

        // 既定（null）は透明のまま（背後の要素を透かす）
        Assert.Equal(DrawingColor.Transparent, settings.ToFadeMaskDragRectOptions().Loupe.Appearance.BackgroundColor);
        // ホストが決めた色（撮影オーバーレイは白・プレビューの切り出しはテーマ背景色）を使う
        Assert.Equal(DrawingColor.White, settings.ToFadeMaskDragRectOptions(DrawingColor.White).Loupe.Appearance.BackgroundColor);
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

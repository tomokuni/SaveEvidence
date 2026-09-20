using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Estable.AvaloniaUI.DragRect;
using Estable.AvaloniaUI.FadeMask;
using Estable.AvaloniaUI.Loupe;
using Xunit;

namespace Estable.AvaloniaUI.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectControl"/> の単体テスト（生成・設定レコードの検証）。</summary>
/// <remarks>
/// <see cref="FadeMaskDragRectControl"/> は LoupeControl（SolidColorBrush 等のスレッドアフィニティ）を内包するため、
/// Avalonia ヘッドレス実行（Dispatcher）を必要とする。<c>[AvaloniaFact]</c> で直列コレクションに属して実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class FadeMaskDragRectControlStateTests
{
    [AvaloniaFact]
    public void コンストラクタ_内包コントロールが3つ配置される()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Equal(3, control.Children.Count);
        Assert.IsType<FadeMaskControl>(control.Children[0]);
        Assert.IsType<DragRectControl>(control.Children[1]);
        Assert.IsType<LoupeControl>(control.Children[2]);
    }

    // ====================================================================
    // ToNonNull（null → 既定値の解決）
    // ====================================================================

    [AvaloniaFact]
    public void ToNonNull_全nullの設定を既定値で埋める()
    {
        // 全メンバー null の設定 → ToNonNull で既定値に置き換えられる（?? の右側分岐）
        var resolved = new FadeMaskDragRectSettings().ToNonNull();

        Assert.NotNull(resolved.MinSelectionSize);
        Assert.NotNull(resolved.BorderColor);
        Assert.NotNull(resolved.BorderThickness);
        Assert.NotNull(resolved.HandleColor);
        Assert.NotNull(resolved.HandleSize);
        Assert.NotNull(resolved.HandleHitRadius);
        Assert.NotNull(resolved.ClampToDomainBounds);
        Assert.NotNull(resolved.MarkerColor);
        Assert.NotNull(resolved.MarkerLength);
        Assert.NotNull(resolved.ShowCoordLabel);
        Assert.NotNull(resolved.CoordLabelColor);
        Assert.NotNull(resolved.CoordLabelBackgroundColor);
        Assert.NotNull(resolved.CoordLabelFontSize);
        Assert.NotNull(resolved.OverlayColor);
        Assert.NotNull(resolved.TextColor);
        Assert.NotNull(resolved.FontSize);
        Assert.NotNull(resolved.ShowSizeLabel);
        Assert.NotNull(resolved.InfoLabelVisible);
        Assert.NotNull(resolved.InfoLabelColor);
        Assert.NotNull(resolved.InfoLabelFontSize);
        Assert.NotNull(resolved.ShowSubLines);
        Assert.NotNull(resolved.SubLineFontSize);
        Assert.NotNull(resolved.SubLineColor);
        Assert.NotNull(resolved.SubLineBackgroundColor);
        Assert.NotNull(resolved.InfoLabelBackgroundColor);
        Assert.NotNull(resolved.TextBackgroundColor);
        Assert.NotNull(resolved.RequestedSize);
        Assert.NotNull(resolved.ZoomLevel);
        Assert.NotNull(resolved.CrossColor);
        Assert.NotNull(resolved.CheckerSize);
        Assert.NotNull(resolved.CheckerColorA);
        Assert.NotNull(resolved.CheckerColorB);
        Assert.NotNull(resolved.FrameColor);
        Assert.NotNull(resolved.FrameWidth);
        Assert.NotNull(resolved.SelectionRectColorA);
        Assert.NotNull(resolved.SelectionRectColorB);
        Assert.NotNull(resolved.PositionOffset);
        Assert.NotNull(resolved.LoupeShowCoordLabel);
        Assert.NotNull(resolved.LoupeCoordLabelColor);
        Assert.NotNull(resolved.LoupeCoordLabelBackgroundColor);
        Assert.NotNull(resolved.LoupeCoordLabelFontSize);
    }

    [AvaloniaFact]
    public void ToNonNull_設定済みの値は維持される()
    {
        // 全メンバー値設定 → ToNonNull で値がそのまま維持される（?? の左側分岐）
        var settings = new FadeMaskDragRectSettings
        {
            MinSelectionSize = 1.0,
            BorderColor = Colors.Red,
            BorderThickness = 3.0,
            HandleColor = Colors.Blue,
            HandleSize = 14.0,
            HandleHitRadius = 20.0,
            ClampToDomainBounds = false,
            MarkerColor = Colors.Yellow,
            MarkerLength = 24.0,
            ShowCoordLabel = true,
            CoordLabelColor = Colors.Cyan,
            CoordLabelBackgroundColor = Colors.DarkBlue,
            CoordLabelFontSize = 16.0,
            OverlayColor = Colors.DarkGray,
            TextColor = Colors.Lime,
            FontSize = 18.0,
            ShowSizeLabel = false,
            InfoLabelVisible = false,
            InfoLabelColor = Colors.Orange,
            InfoLabelFontSize = 20.0,
            ShowSubLines = false,
            SubLineFontSize = 22.0,
            SubLineColor = Colors.Purple,
            SubLineBackgroundColor = Colors.DarkGreen,
            InfoLabelBackgroundColor = Colors.DarkRed,
            TextBackgroundColor = Colors.DarkCyan,
            RequestedSize = 200,
            ZoomLevel = 8,
            CrossColor = Colors.Magenta,
            CheckerSize = 10,
            CheckerColorA = Colors.Gray,
            CheckerColorB = Colors.Silver,
            FrameColor = Colors.DarkOrange,
            FrameWidth = 4,
            SelectionRectColorA = Colors.White,
            SelectionRectColorB = Colors.Black,
            PositionOffset = 25.0,
            LoupeShowCoordLabel = true,
            LoupeCoordLabelColor = Colors.Pink,
            LoupeCoordLabelBackgroundColor = Colors.DarkViolet,
            LoupeCoordLabelFontSize = 15.0,
        };

        var resolved = settings.ToNonNull();

        Assert.Equal(1.0, resolved.MinSelectionSize);
        Assert.Equal(Colors.Red, resolved.BorderColor);
        Assert.Equal(3.0, resolved.BorderThickness);
        Assert.Equal(Colors.Blue, resolved.HandleColor);
        Assert.Equal(14.0, resolved.HandleSize);
        Assert.Equal(20.0, resolved.HandleHitRadius);
        Assert.False(resolved.ClampToDomainBounds);
        Assert.Equal(Colors.Yellow, resolved.MarkerColor);
        Assert.Equal(24.0, resolved.MarkerLength);
        Assert.True(resolved.ShowCoordLabel);
        Assert.Equal(Colors.Cyan, resolved.CoordLabelColor);
        Assert.Equal(Colors.DarkBlue, resolved.CoordLabelBackgroundColor);
        Assert.Equal(16.0, resolved.CoordLabelFontSize);
        Assert.Equal(Colors.DarkGray, resolved.OverlayColor);
        Assert.Equal(Colors.Lime, resolved.TextColor);
        Assert.Equal(18.0, resolved.FontSize);
        Assert.False(resolved.ShowSizeLabel);
        Assert.False(resolved.InfoLabelVisible);
        Assert.Equal(Colors.Orange, resolved.InfoLabelColor);
        Assert.Equal(20.0, resolved.InfoLabelFontSize);
        Assert.False(resolved.ShowSubLines);
        Assert.Equal(22.0, resolved.SubLineFontSize);
        Assert.Equal(Colors.Purple, resolved.SubLineColor);
        Assert.Equal(Colors.DarkGreen, resolved.SubLineBackgroundColor);
        Assert.Equal(Colors.DarkRed, resolved.InfoLabelBackgroundColor);
        Assert.Equal(Colors.DarkCyan, resolved.TextBackgroundColor);
        Assert.Equal(200, resolved.RequestedSize);
        Assert.Equal(8, resolved.ZoomLevel);
        Assert.Equal(Colors.Magenta, resolved.CrossColor);
        Assert.Equal(10, resolved.CheckerSize);
        Assert.Equal(Colors.Gray, resolved.CheckerColorA);
        Assert.Equal(Colors.Silver, resolved.CheckerColorB);
        Assert.Equal(Colors.DarkOrange, resolved.FrameColor);
        Assert.Equal(4, resolved.FrameWidth);
        Assert.Equal(Colors.White, resolved.SelectionRectColorA);
        Assert.Equal(Colors.Black, resolved.SelectionRectColorB);
        Assert.Equal(25.0, resolved.PositionOffset);
        Assert.True(resolved.LoupeShowCoordLabel);
        Assert.Equal(Colors.Pink, resolved.LoupeCoordLabelColor);
        Assert.Equal(Colors.DarkViolet, resolved.LoupeCoordLabelBackgroundColor);
        Assert.Equal(15.0, resolved.LoupeCoordLabelFontSize);
    }
}

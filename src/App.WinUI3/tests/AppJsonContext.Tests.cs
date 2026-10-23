using System.Text.Json;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Window;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="AppJsonContext"/> の単体テスト（設定の JSON ソース生成）。</summary>
/// <remarks>
/// <b>本テストが必要な理由</b>: ソース生成コンテキストが <c>Settings</c> のプロパティを取りこぼすと、<br/>
/// 設定を保存しても空（<c>{}</c>）になる（トリミングで起きる）。 生成された項目を実測で固定する。<br/>
/// <b>ファイルへは書き込まない</b>（利用者の実際の設定を壊さない）。
/// </remarks>
public class AppJsonContextTests
{
    /// <summary>設定が往復して同じ値になることを検証する。</summary>
    [Fact]
    internal void 設定が往復して同じ値になる()
    {
        var source = new Settings
        {
            ThemeMode = Core.Enums.ThemeMode.Dark,
            SaveFolderPath = @"C:\images",
            FileNameTemplate = "shot_{date}-{time}.png",
            FolderViewModeIndex = 2,
            FolderViewSortAscending = false,
            UndoViewModeIndex = 0,
            CenterAlign = true,
            HideMainWindowOnCapture = false,
            ClampToSourceBounds = false,
            LoupeSize = 256,
            LoupeZoomLevel = 8,
            CaptureBorderColor = "#80112233",
            SizeLabelFontSize = 17.5,
            IsNavigationExpanded = false,
            MainWindowState = EsWindowState.Maximized,
            MainWindowBounds = new EsRectInt(10, 20, 800, 600),
        };

        var json = JsonSerializer.Serialize(source, AppJsonContext.Default.Settings);
        var restored = JsonSerializer.Deserialize(json, AppJsonContext.Default.Settings);

        Assert.NotNull(restored);
        Assert.Equal(source.ThemeMode, restored.ThemeMode);
        Assert.Equal(source.SaveFolderPath, restored.SaveFolderPath);
        Assert.Equal(source.FileNameTemplate, restored.FileNameTemplate);
        Assert.Equal(source.FolderViewModeIndex, restored.FolderViewModeIndex);
        Assert.Equal(source.FolderViewSortAscending, restored.FolderViewSortAscending);
        Assert.Equal(source.CenterAlign, restored.CenterAlign);
        Assert.Equal(source.LoupeSize, restored.LoupeSize);
        Assert.Equal(source.CaptureBorderColor, restored.CaptureBorderColor);
        Assert.Equal(source.SizeLabelFontSize, restored.SizeLabelFontSize);
        Assert.Equal(source.MainWindowState, restored.MainWindowState);
        Assert.Equal(source.MainWindowBounds, restored.MainWindowBounds);
    }

    /// <summary>ソース生成がプロパティを取りこぼしていないことを検証する。</summary>
    /// <remarks>既定値のままでも全項目が JSON へ現れる（空の <c>{}</c> にならない）。</remarks>
    [Fact]
    internal void ソース生成が全プロパティを書き出す()
    {
        var json = JsonSerializer.Serialize(new Settings(), AppJsonContext.Default.Settings);

        foreach (var name in new[]
        {
            "SelectScreenHotKey", "WindowSelectHotKey", "AreaSelectHotKey", "FileNameTemplate", "SaveFolderPath",
            "MainWindowBounds", "MainWindowState", "IsNavigationExpanded", "ThemeMode",
            "FolderExtraLargeIconSize", "FolderLargeIconSize", "FolderMediumIconSize", "FolderViewModeIndex",
            "FolderViewSortAscending", "UndoViewModeIndex", "CenterAlign",
            "CaptureBorderColor", "CaptureTextColor", "CaptureBorderThickness", "CaptureOverlayColor",
            "MinSelectionSize", "HandleColor", "HandleSize", "HandleHitRadius", "ClampToSourceBounds",
            "MarkerColor", "MarkerLength", "ShowCoordLabel", "CoordLabelColor", "CoordLabelBackgroundColor",
            "CoordLabelFontSize", "LoupeCrossColor", "LoupeFrameColor", "LoupeFrameWidth", "LoupeZoomLevel",
            "LoupeSize", "LoupeOffset", "LoupePasteboardColor", "LoupeSelectionRectColorA", "LoupeSelectionRectColorB",
            "LoupeShowCoordLabel", "LoupeCoordLabelColor", "LoupeCoordLabelBackgroundColor", "LoupeCoordLabelFontSize",
            "SizeLabelVisible", "SizeLabelBackgroundColor", "SizeLabelFontSize", "InfoLabelVisible", "InfoLabelColor",
            "InfoLabelBackgroundColor", "InfoLabelFontSize", "ShowSubLines", "SubLineFontSize", "SubLineColor",
            "SubLineBackgroundColor", "HideMainWindowOnCapture",
        })
        {
            Assert.Contains($"\"{name}\"", json, StringComparison.Ordinal);
        }
    }
}
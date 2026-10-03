using EsUtil.SaveEvidence.App.AvaloniaUI.Icons;
using Avalonia.Headless.XUnit;
using Xunit;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Tests;

/// <summary>
/// IconLoader（埋め込み SVG からアイコン生成）の単体テスト。
/// </summary>
/// <remarks>
/// EsUtil.SaveEvidence.csproj の <c>AvaloniaResource</c> で埋め込まれた SVG が<br/>
/// <c>avares://</c> で正しく読み込めることを検証する（アセンブリ名の解決を含む）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class IconLoaderTests
{
    [AvaloniaFact]
    public void LoadGeometry_保存アイコンが読み込める()
    {
        var geometry = IconLoader.LoadGeometry("ic_fluent_save_24_regular.svg");
        Assert.NotNull(geometry);
        Assert.True(geometry.Bounds.Width > 0 && geometry.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void LoadGeometry_ウィンドウアイコンが読み込める()
    {
        var geometry = IconLoader.LoadGeometry("ic_fluent_window_24_regular.svg");
        Assert.NotNull(geometry);
        Assert.True(geometry.Bounds.Width > 0 && geometry.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void LoadGeometry_スキャンテーブルアイコンが読み込める()
    {
        var geometry = IconLoader.LoadGeometry("ic_fluent_scan_table_24_regular.svg");
        Assert.NotNull(geometry);
        Assert.True(geometry.Bounds.Width > 0 && geometry.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void LoadGeometry_存在しないファイルは空ジオメトリを返す()
    {
        // フェールセーフ: 存在しない SVG は空ジオメトリ（M0,0）を返し例外を投げない
        var geometry = IconLoader.LoadGeometry("ic_fluent_not_exist_24_regular.svg");
        Assert.NotNull(geometry);
        Assert.Equal(0.0, geometry.Bounds.Width);
        Assert.Equal(0.0, geometry.Bounds.Height);
    }

    [AvaloniaFact]
    public void LoadWindowIcon_ウィンドウアイコンを生成できる()
    {
        var icon = IconLoader.LoadWindowIcon("ic_fluent_scan_table_24_regular.svg", 32);
        Assert.NotNull(icon);
    }
}

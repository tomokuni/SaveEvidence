using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EsUtil.SaveEvidence.LineDetection.Sample.ViewModels;

/// <summary>ナビゲーションのページ。</summary>
public enum AppPage
{
    /// <summary>線分検出（クリップボードの画像を検出）。</summary>
    LineDetection,

    /// <summary>データ検証（選択した dataset 画像を照合）。</summary>
    VerifyLines,

    /// <summary>データ作成（Marquee キャプチャ → 正解選択 → 保存）。</summary>
    DatasetCreate,

    /// <summary>データ全検証（dataset の全画像を一括照合）。</summary>
    VerifyAll,
}

/// <summary>ナビゲーションのページ表示オプション。</summary>
/// <param name="Page">ページ値。</param>
/// <param name="Label">表示ラベル。</param>
/// <param name="Icon">アイコン（SVG のジオメトリ）。</param>
public sealed record PageItem(AppPage Page, string Label, Geometry Icon)
{
    /// <summary>表示名を返す（ナビゲーション用）。</summary>
    public override string ToString() => Label;
}

/// <summary>dataset のエントリ（JSON 1 ファイル分）。</summary>
/// <param name="Name">画像ファイル名（拡張子なし・表示名・ドロップダウン表示用）。</param>
/// <param name="PngPath">PNG 画像ファイルの絶対パス。</param>
/// <param name="Bounds">検証対象のウィンドウ境界（ドメイン座標・排他座標）。</param>
public sealed record DatasetEntry(string Name, string PngPath, DatasetBounds Bounds)
{
    /// <summary>表示名を返す（ドロップダウン用）。</summary>
    public override string ToString() => Name;
}

/// <summary>dataset のウィンドウ境界（ドメイン座標・排他座標）。</summary>
/// <param name="X">左端 X（排他座標）。</param>
/// <param name="Y">上端 Y（排他座標）。</param>
/// <param name="Right">右端 X（排他座標）。</param>
/// <param name="Bottom">下端 Y（排他座標）。</param>
public readonly record struct DatasetBounds(int X, int Y, int Right, int Bottom);

/// <summary>dataset JSON のルート構造。</summary>
internal sealed class DatasetRoot
{
    /// <summary>GroundTruthWindows（正解のウィンドウ群）。</summary>
    public List<DatasetWindow> GroundTruthWindows { get; set; } = [];
}

/// <summary>dataset JSON の正解ウィンドウ。</summary>
internal sealed class DatasetWindow
{
    /// <summary>ウィンドウの境界。</summary>
    public DatasetBoundsJson? Bounds { get; set; }
}

/// <summary>dataset JSON の境界（X/Y/Width/Height/Right/Bottom）。</summary>
internal sealed class DatasetBoundsJson
{
    /// <summary>左端 X。</summary>
    public int X { get; set; }

    /// <summary>上端 Y。</summary>
    public int Y { get; set; }

    /// <summary>幅。</summary>
    public int Width { get; set; }

    /// <summary>高さ。</summary>
    public int Height { get; set; }

    /// <summary>右端 X。</summary>
    public int Right { get; set; }

    /// <summary>下端 Y。</summary>
    public int Bottom { get; set; }
}

/// <summary>
/// メイン画面の ViewModel。ナビゲーションのページ選択と、各ページの ViewModel を統括する。
/// </summary>
/// <remarks>
/// ナビゲーション（<see cref="Pages"/> / <see cref="SelectedPageItem"/>）で選択されたページを管理し、<br/>
/// 対応するページ ViewModel（<see cref="DetectPage"/> / <see cref="VerifyPage"/> / <see cref="CreatePage"/> / <see cref="VerifyAllPage"/>）へ委譲する。<br/>
/// 各ページの状態はページ ViewModel が独立して保持する（ページ間で共有しない）。
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>ナビゲーションのページ一覧。</summary>
    public IReadOnlyList<PageItem> Pages { get; } =
    [
        new PageItem(AppPage.LineDetection, "線分検出", SvgIcon(
            "M16.338 2.99956C16.1215 3.45423 16.0003 3.96294 16 4.49994L5.75 4.50027C5.05964 4.50027 4.5 5.05991 4.5 5.75027V18.2503C4.5 18.9406 5.05964 19.5003 5.75 19.5003H18.25C18.9404 19.5003 19.5 18.9406 19.5 18.2503V8.00195C20.0372 8.00195 20.5461 7.88092 21.001 7.66464L21 18.2503C21 19.7691 19.7688 21.0003 18.25 21.0003H5.75C4.23122 21.0003 3 19.7691 3 18.2503V5.75027C3 4.23149 4.23122 3.00027 5.75 3.00027L16.338 2.99956ZM19.5 2.00195C20.8807 2.00195 22 3.12124 22 4.50195C22 5.88266 20.8807 7.00195 19.5 7.00195C18.1193 7.00195 17 5.88266 17 4.50195C17 3.12124 18.1193 2.00195 19.5 2.00195Z")),
        new PageItem(AppPage.VerifyAll, "データ全検証", SvgIcon(
            "M17.75 3C18.9926 3 20 4.00736 20 5.25V18.75C20 19.9926 18.9926 21 17.75 21H6.25C5.00736 21 4 19.9926 4 18.75V5.25C4 4.00736 5.00736 3 6.25 3H17.75ZM6.25 4.5C5.83579 4.5 5.5 4.83579 5.5 5.25V18.75C5.5 19.1642 5.83579 19.5 6.25 19.5H17.75C18.1642 19.5 18.5 19.1642 18.5 18.75V5.25C18.5 4.83579 18.1642 4.5 17.75 4.5H6.25ZM9 7.75C9 7.33579 9.33579 7 9.75 7H14.25C14.6642 7 15 7.33579 15 7.75C15 8.16421 14.6642 8.5 14.25 8.5H9.75C9.33579 8.5 9 8.16421 9 7.75ZM7.78033 7.71967C8.07322 8.01256 8.07322 8.48744 7.78033 8.78033L7.03033 9.53033C6.73744 9.82322 6.26256 9.82322 5.96967 9.53033C5.67678 9.23744 5.67678 8.76256 5.96967 8.46967L6.71967 7.71967C7.01256 7.42678 7.48744 7.42678 7.78033 7.71967ZM9 11.75C9 11.3358 9.33579 11 9.75 11H14.25C14.6642 11 15 11.3358 15 11.75C15 12.1642 14.6642 12.5 14.25 12.5H9.75C9.33579 12.5 9 12.1642 9 11.75ZM7.78033 11.7197C8.07322 12.0126 8.07322 12.4874 7.78033 12.7803L7.03033 13.5303C6.73744 13.8232 6.26256 13.8232 5.96967 13.5303C5.67678 13.2374 5.67678 12.7626 5.96967 12.4697L6.71967 11.7197C7.01256 11.4268 7.48744 11.4268 7.78033 11.7197ZM9 15.75C9 15.3358 9.33579 15 9.75 15H11.25C11.6642 15 12 15.3358 12 15.75C12 16.1642 11.6642 16.5 11.25 16.5H9.75C9.33579 16.5 9 16.1642 9 15.75Z")),
        new PageItem(AppPage.VerifyLines, "データ検証", SvgIcon(
            "M17.0303 11.0303C17.3232 10.7374 17.3232 10.2626 17.0303 9.96967C16.7374 9.67678 16.2626 9.67678 15.9697 9.96967L11 14.9393L9.03033 12.9697C8.73744 12.6768 8.26256 12.6768 7.96967 12.9697C7.67678 13.2626 7.67678 13.7374 7.96967 14.0303L10.4697 16.5303C10.7626 16.8232 11.2374 16.8232 11.5303 16.5303L17.0303 11.0303ZM15.994 4.08449C15.9093 2.91913 14.937 2 13.75 2H10.25C9.09205 2 8.13841 2.87472 8.01379 3.99944L6.25 4C5.00736 4 4 5.00736 4 6.25V19.75C4 20.9926 5.00736 22 6.25 22H17.75C18.9926 22 20 20.9926 20 19.75V6.25C20 5.00736 18.9926 4 17.75 4L15.9862 3.99944C15.9893 4.02764 15.9919 4.05599 15.994 4.08449ZM15.9948 4.09595L16 4.25C16 4.19822 15.9983 4.14685 15.9948 4.09595ZM10.25 6.5H13.75C14.53 6.5 15.2174 6.10307 15.621 5.50016L17.75 5.5C18.1642 5.5 18.5 5.83579 18.5 6.25V19.75C18.5 20.1642 18.1642 20.5 17.75 20.5H6.25C5.83579 20.5 5.5 20.1642 5.5 19.75V6.25C5.5 5.83579 5.83579 5.5 6.25 5.5L8.37902 5.5002C8.78267 6.1031 9.46997 6.5 10.25 6.5ZM10.25 3.5H13.75C14.1642 3.5 14.5 3.83579 14.5 4.25C14.5 4.66421 14.1642 5 13.75 5H10.25C9.83579 5 9.5 4.66421 9.5 4.25C9.5 3.83579 9.83579 3.5 10.25 3.5Z")),
        new PageItem(AppPage.DatasetCreate, "データ作成", SvgIcon(
            "M13.9862 3.99944C13.8616 2.87472 12.9079 2 11.75 2H8.25C7.09205 2 6.13841 2.87472 6.01379 3.99944L4.25 4C3.00736 4 2 5.00736 2 6.25V19.75C2 20.9926 3.00736 22 4.25 22H12.8096C12.3832 21.5557 12.0194 21.051 11.7322 20.5H4.25C3.83579 20.5 3.5 20.1642 3.5 19.75V6.25C3.5 5.83579 3.83579 5.5 4.25 5.5L6.379 5.50016C6.78264 6.10307 7.46997 6.5 8.25 6.5H11.75C12.53 6.5 13.2173 6.1031 13.621 5.5002L15.75 5.5C16.1642 5.5 16.5 5.83579 16.5 6.25V11.0764C16.826 11.0261 17.1599 11 17.5 11C17.6682 11 17.835 11.0064 18 11.0189V6.25C18 5.00736 16.9926 4 15.75 4L13.9862 3.99944ZM13.9948 4.09595L14 4.25C14 4.19822 13.9983 4.14685 13.9948 4.09595ZM8.25 3.5H11.75C12.1642 3.5 12.5 3.83579 12.5 4.25C12.5 4.66421 12.1642 5 11.75 5H8.25C7.83579 5 7.5 4.66421 7.5 4.25C7.5 3.83579 7.83579 3.5 8.25 3.5ZM11.9565 14.1041C12.493 13.2303 13.2303 12.493 14.1041 11.9565L15.0303 11.0303C15.3232 10.7374 15.3232 10.2626 15.0303 9.96967C14.7374 9.67678 14.2626 9.67678 13.9697 9.96967L9 14.9393L7.03033 12.9697C6.73744 12.6768 6.26256 12.6768 5.96967 12.9697C5.67678 13.2626 5.67678 13.7374 5.96967 14.0303L8.46967 16.5303C8.76256 16.8232 9.23744 16.8232 9.53033 16.5303L11.9565 14.1041ZM23 17.5C23 14.4624 20.5376 12 17.5 12C14.4624 12 12 14.4624 12 17.5C12 20.5376 14.4624 23 17.5 23C20.5376 23 23 20.5376 23 17.5ZM18.0006 18L18.0011 20.5035C18.0011 20.7797 17.7773 21.0035 17.5011 21.0035C17.225 21.0035 17.0011 20.7797 17.0011 20.5035L17.0006 18H14.4956C14.2197 18 13.9961 17.7762 13.9961 17.5C13.9961 17.2239 14.2197 17 14.4956 17H17.0005L17 14.4993C17 14.2231 17.2239 13.9993 17.5 13.9993C17.7761 13.9993 18 14.2231 18 14.4993L18.0005 17H20.4966C20.7725 17 20.9961 17.2239 20.9961 17.5C20.9961 17.7762 20.7725 18 20.4966 18H18.0006Z")),
    ];

    /// <summary>ナビゲーションパネルの開閉アイコン（矢印・点線）。</summary>
    public Geometry NavigationToggleIcon { get; } = SvgIcon(
        "M6.78016 18.7198C7.07298 19.0126 7.07298 19.4874 6.78016 19.7803C6.48729 20.0732 6.01251 20.0731 5.71961 19.7803L3.21961 17.2803C3.07895 17.1396 2.99988 16.9489 2.99988 16.75C2.99988 16.5511 3.07895 16.3604 3.21961 16.2197L5.71961 13.7197C6.01251 13.4269 6.48729 13.4269 6.78016 13.7197C7.07298 14.0126 7.07298 14.4874 6.78016 14.7803L4.81043 16.75L6.78016 18.7198ZM19.9999 11.25C19.9999 11.6642 19.6641 12 19.2499 12C18.8357 12 18.4999 11.6642 18.4999 11.25V6.25C18.4999 5.83583 18.1641 5.50007 17.7499 5.5H6.24989C5.83567 5.5 5.49989 5.83579 5.49989 6.25V11.25C5.49989 11.6642 5.16404 12 4.74988 12C4.33567 12 3.99988 11.6642 3.99988 11.25V6.25C3.99988 5.00736 5.00724 4 6.24989 4H17.7499C18.9925 4.00006 19.9999 5.0074 19.9999 6.25V11.25ZM9.4999 16.75C9.4999 17.1642 9.16405 17.5 8.74989 17.5H7.74989C7.33568 17.5 6.99989 17.1642 6.99989 16.75C6.99989 16.3358 7.33568 16 7.74989 16H8.74989C9.16405 16.0001 9.4999 16.3358 9.4999 16.75ZM12.9999 16.75C12.9999 17.1642 12.6641 17.5 12.2499 17.5H11.7499C11.3357 17.5 10.9999 17.1642 10.9999 16.75C10.9999 16.3358 11.3357 16 11.7499 16H12.2499C12.6641 16.0001 12.9999 16.3358 12.9999 16.75ZM16.9999 16.75C16.9999 17.1642 16.6641 17.5 16.2499 17.5H15.2499C14.8357 17.5 14.4999 17.1642 14.4999 16.75C14.4999 16.3358 14.8357 16 15.2499 16H16.2499C16.6641 16.0001 16.9999 16.3358 16.9999 16.75ZM20.832 16.2764C21.0722 16.5709 21.0547 17.0057 20.7802 17.2803L18.2802 19.7803C17.9873 20.0732 17.5125 20.0731 17.2196 19.7803C16.9268 19.4874 16.9268 19.0126 17.2196 18.7198L19.1894 16.75L17.2196 14.7803C16.9268 14.4874 16.9268 14.0126 17.2196 13.7197C17.5125 13.4269 17.9873 13.4269 18.2802 13.7197L20.7802 16.2197L20.832 16.2764Z");

    /// <summary>ナビゲーションパネルの展開時の幅（DIP）。app と同様に 180。</summary>
    private const double ExpandedNavigationWidth = 120;

    /// <summary>ナビゲーションパネルの折りたたみ時の幅（DIP）。アイコンのみ表示する。app と同様に 40。</summary>
    private const double CollapsedNavigationWidth = 40;

    /// <summary>ナビゲーションパネルが展開されているかどうか。</summary>
    [ObservableProperty]
    private bool _isNavigationExpanded = true;

    /// <summary>ナビゲーションパネルの現在の幅（DIP）。</summary>
    public double NavigationPanelWidth => IsNavigationExpanded ? ExpandedNavigationWidth : CollapsedNavigationWidth;

    /// <summary>ナビゲーションパネルの展開/折りたたみを切り替える。</summary>
    [RelayCommand]
    private void ToggleNavigation()
    {
        IsNavigationExpanded = !IsNavigationExpanded;
        OnPropertyChanged(nameof(NavigationPanelWidth));
    }

    /// <summary>選択中のナビゲーションページ表示オプション（ListBox の SelectedItem と双方向バインド用）。</summary>
    [ObservableProperty]
    private PageItem? _selectedPageItem;

    /// <summary>選択中のナビゲーションページ。</summary>
    [ObservableProperty]
    private AppPage _selectedPage = AppPage.LineDetection;

    /// <summary>「線分検出」ページの表示状態。</summary>
    public bool IsLineDetectionPage => SelectedPage == AppPage.LineDetection;

    /// <summary>「データ検証」ページの表示状態。</summary>
    public bool IsVerifyLinesPage => SelectedPage == AppPage.VerifyLines;

    /// <summary>「データ作成」ページの表示状態。</summary>
    public bool IsDatasetCreatePage => SelectedPage == AppPage.DatasetCreate;

    /// <summary>「データ全検証」ページの表示状態。</summary>
    public bool IsVerifyAllPage => SelectedPage == AppPage.VerifyAll;

    /// <summary>「線分検出」ページの ViewModel。</summary>
    public DetectPageViewModel DetectPage { get; }

    /// <summary>「データ検証」ページの ViewModel。</summary>
    public VerifyPageViewModel VerifyPage { get; }

    /// <summary>「データ作成」ページの ViewModel。</summary>
    public CreatePageViewModel CreatePage { get; }

    /// <summary>「データ全検証」ページの ViewModel。</summary>
    public VerifyAllPageViewModel VerifyAllPage { get; }

    /// <summary>MainWindowViewModel を初期化する。</summary>
    public MainWindowViewModel()
    {
        DetectPage = new DetectPageViewModel();
        VerifyPage = new VerifyPageViewModel();
        CreatePage = new CreatePageViewModel();
        VerifyAllPage = new VerifyAllPageViewModel();

        _selectedPageItem = Pages[0];

        // データ作成ページで保存されたら、データ検証・データ全検証ページの一覧を再読み込みする
        CreatePage.DatasetSaved += _ =>
        {
            VerifyPage.ReloadDatasetItems();
            VerifyAllPage.ReloadDatasetItems();
        };
    }

    /// <summary>選択中のナビゲーションページ表示オプションが変わったとき、ページを反映する。</summary>
    /// <param name="value">選択されたページ表示オプション。</param>
    partial void OnSelectedPageItemChanged(PageItem? value)
    {
        if (value is not null)
        {
            SelectedPage = value.Page;
        }
    }

    /// <summary>選択中のナビゲーションページが変わったとき、表示切替を反映する。</summary>
    /// <param name="value">新しいページ。</param>
    partial void OnSelectedPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(IsLineDetectionPage));
        OnPropertyChanged(nameof(IsVerifyLinesPage));
        OnPropertyChanged(nameof(IsDatasetCreatePage));
        OnPropertyChanged(nameof(IsVerifyAllPage));
    }

    /// <summary>SVG のパスデータ（<c>d</c> 属性）からアイコンのジオメトリを生成する。</summary>
    /// <param name="pathData">SVG のパスデータ。</param>
    /// <returns>パスデータを解析した <see cref="StreamGeometry"/>。</returns>
    /// <remarks>Fluent System Icons の 24px 正規アイコンのパスデータを <see cref="StreamGeometry.Parse"/> で変換する。</remarks>
    private static StreamGeometry SvgIcon(string pathData)
        => StreamGeometry.Parse(pathData);
}

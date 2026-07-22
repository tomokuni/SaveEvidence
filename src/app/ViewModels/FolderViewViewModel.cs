using CommunityToolkit.Mvvm.ComponentModel;

namespace app.ViewModels;

/// <summary>
/// FolderViewForm のステータスバー表示のための ViewModel。
/// </summary>
/// <remarks>
/// CommunityToolkit.Mvvm の <see cref="ObservableObject"/> を継承し、
/// <c>[ObservableProperty]</c> 属性により自動的に変更通知を発行する。<br/>
/// アイテム数（ItemCountText）、フォルダパス（FolderPathText）、
/// 表示モード（ViewModeText）、ソート状態（SortStatusText）に加え、
/// コンテキストメニューのチェック状態（SelectedViewModeIndex, IsSortAscending）を保持し、
/// View の DataBindings およびイベントハンドラーを介して反映する。<br/>
/// </remarks>
public sealed partial class FolderViewViewModel : ObservableObject
{
    /// <summary>現在の表示モードテキスト（例：「表示: 大」）</summary>
    [ObservableProperty]
    private string _viewModeText = "表示: 大";

    /// <summary>現在のソート状態テキスト（例：「並替: 名前 ↑」）</summary>
    [ObservableProperty]
    private string _sortStatusText = "並替: 名前 ↑";

    /// <summary>現在選択されている表示モードのインデックス（0=特大, 1=大, 2=中, 3=一覧, 4=詳細）</summary>
    [ObservableProperty]
    private int _selectedViewModeIndex = 1;

    /// <summary>ソートが昇順かどうか</summary>
    [ObservableProperty]
    private bool _isSortAscending = true;

    /// <summary>アイテム数テキスト（例：「10 個のアイテム」）</summary>
    [ObservableProperty]
    private string _itemCountText = "";

    /// <summary>フォルダパステキスト</summary>
    [ObservableProperty]
    private string _folderPathText = "";
}

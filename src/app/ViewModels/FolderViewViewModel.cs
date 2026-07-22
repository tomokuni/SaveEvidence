using CommunityToolkit.Mvvm.ComponentModel;

namespace app.ViewModels;

/// <summary>
/// FolderViewForm のステータスバー表示のための ViewModel。
/// </summary>
/// <remarks>
/// CommunityToolkit.Mvvm の <see cref="ObservableObject"/> を継承し、
/// <c>[ObservableProperty]</c> 属性により自動的に変更通知を発行する。<br/>
/// アイテム数（ItemCount）、表示モード（SelectedViewModeIndex）、
/// ソート状態（IsSortAscending）などのプリミティブな状態を保持し、
/// ItemCountText / ViewModeText / SortStatusText は自動計算される。<br/>
/// View はプリミティブなプロパティのみを操作し、表示テキストは
/// DataBinding により自動反映される。<br/>
/// </remarks>
public sealed partial class FolderViewViewModel : ObservableObject
{
    private static readonly string[] s_viewModeNames = ["特大", "大", "中", "一覧", "詳細"];

    /// <summary>アイテム数</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemCountText))]
    private int _itemCount;

    /// <summary>現在選択されている表示モードのインデックス（0=特大, 1=大, 2=中, 3=一覧, 4=詳細）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewModeText))]
    private int _selectedViewModeIndex = 1;

    /// <summary>ソートが昇順かどうか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortStatusText))]
    private bool _isSortAscending = true;

    /// <summary>フォルダパステキスト</summary>
    [ObservableProperty]
    private string _folderPathText = "";

    /// <summary>アイテム数テキスト（例：「10 個のアイテム」）を取得する。</summary>
    public string ItemCountText => $"{ItemCount} 個のアイテム";

    /// <summary>現在の表示モードテキスト（例：「大」）を取得する。</summary>
    public string ViewModeText => (uint)SelectedViewModeIndex < s_viewModeNames.Length ? s_viewModeNames[SelectedViewModeIndex] : "";

    /// <summary>現在のソート状態テキスト（例：「名前の昇順(▲)」）を取得する。</summary>
    public string SortStatusText => IsSortAscending ? "名前の昇順(▲)" : "名前の降順(▼)";
}

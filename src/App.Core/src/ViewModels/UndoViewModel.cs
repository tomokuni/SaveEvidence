using CommunityToolkit.Mvvm.ComponentModel;

namespace EsUtil.SaveEvidence.App.Core.ViewModels;

/// <summary>Undo 一覧ページのステータスバー表示のための ViewModel。</summary>
/// <remarks>
/// CommunityToolkit.Mvvm の <see cref="ObservableObject"/> を継承し、
/// <c>[ObservableProperty]</c> 属性により自動的に変更通知を発行する。<br/>
/// アイテム数（ItemCount）、表示モード（SelectedViewModeIndex）などの
/// プリミティブな状態を保持し、ItemCountText / ViewModeText は自動計算される。<br/>
/// View はプリミティブなプロパティのみを操作し、表示テキストは
/// DataBinding により自動反映される。<br/>
/// </remarks>
public sealed partial class UndoViewModel : ObservableObject
{
    /// <summary>アイテム数</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemCountText))]
    private int _itemCount;

    /// <summary>現在選択されている表示モードのインデックス（0=特大, 1=大, 2=中）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewModeText))]
    private int _selectedViewModeIndex = 1;

    /// <summary>アイテム数テキスト（例：「5 個のアイテム」）を取得する。</summary>
    public string ItemCountText => ViewModeDisplay.GetItemCountText(ItemCount);

    /// <summary>現在の表示モードテキスト（例：「大」）を取得する。</summary>
    public string ViewModeText => ViewModeDisplay.GetText(SelectedViewModeIndex);
}

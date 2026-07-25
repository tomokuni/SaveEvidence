using CommunityToolkit.Mvvm.ComponentModel;

namespace app.ViewModels;

/// <summary>
/// メインウィンドウのナビゲーション状態を管理する ViewModel。
/// </summary>
/// <remarks>
/// WinUI3 の NavigationView に相当する左ペインのナビゲーションを制御する。<br/>
/// SelectedIndex の値に対応するページ:<br/>
/// - 0: キャプチャ（画像プレビュー）<br/>
/// - 1: フォルダビュー<br/>
/// - 2: 設定<br/>
/// </remarks>
public sealed partial class NavigationViewModel : ObservableObject
{
    /// <summary>ナビゲーション選択インデックスを取得または設定する。</summary>
    [ObservableProperty]
    private int _selectedIndex;
}

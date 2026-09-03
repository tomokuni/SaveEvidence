using Avalonia.Controls;
using LineDetectionSample.ViewModels;

namespace LineDetectionSample.Views;

/// <summary>「データ全検証」ページのコードビハインド。ページ表示時に全画像の検証を自動実行する。</summary>
/// <remarks>
/// <see cref="VerifyAllPageViewModel.VerifyAll"/> はページが表示されたときに自動で呼び出し、<br/>
/// 全画像の検証結果（一致/不一致・差分）を一覧へ反映する。
/// </remarks>
public partial class VerifyAllPage : UserControl
{
    /// <summary>ビューモデル。</summary>
    private VerifyAllPageViewModel? _vm;

    /// <summary>VerifyAllPage を初期化する。</summary>
    public VerifyAllPage()
    {
        InitializeComponent();
    }

    /// <summary>DataContext が設定されたとき、ビューモデルを保持して全検証を自動実行する。</summary>
    /// <param name="e">依存プロパティ変更イベント。</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _vm = DataContext as VerifyAllPageViewModel;
        _vm?.VerifyAll();
    }
}

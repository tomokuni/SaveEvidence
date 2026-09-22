using Avalonia.Controls;
using FadeMaskControl.Sample.ViewModels;

namespace FadeMaskControl.Sample.Views;

/// <summary>メインウィンドウ。</summary>
/// <remarks>
/// 外観（<c>Appearance</c>）と描画状態は AXAML のバインディングで <c>FadeMaskControl</c> へ直接伝わるため、<br/>
/// コードビハインドは再描画要求を含めて不要になった。<br/>
/// 例外はサイズラベルの文言プロバイダ（<see cref="Func{TResult}"/>）で、バインディングできないため本クラスで設定する。
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>メインウィンドウを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>DataContext 設定時にサイズラベルの文言プロバイダをコントロールへ設定する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// プロバイダはデリゲートのため AXAML でバインドできない（コードビハインドで設定する）。<br/>
    /// ViewModel 側で再生成されるため、変更通知のたびに設定し直す。
    /// </remarks>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        Overlay.SizeLabelOverrideProvider = vm.SizeLabelOverrideProvider;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.SizeLabelOverrideProvider))
            {
                Overlay.SizeLabelOverrideProvider = vm.SizeLabelOverrideProvider;
            }
        };
    }
}

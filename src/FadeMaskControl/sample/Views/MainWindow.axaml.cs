using Avalonia.Controls;
using Estable.AvaloniaUI.Platform.Geometry;
using FadeMaskControl.Sample.ViewModels;

namespace FadeMaskControl.Sample.Views;

/// <summary>メインウィンドウ。</summary>
/// <remarks>
/// AXAML で定義されたレイアウトを表示する。コードビハインドは最小限。<br/>
/// ViewModel のプロパティ変更（モード切替・選択矩形等）でレンダラー（ExternalRenderer）の
/// 描画状態が変わった際に、FadeMaskControl の再描画（InvalidateVisual）を要求する。
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>メインウィンドウを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>DataContext 設定時に ViewModel のプロパティ変更を購読し、レンダラーを FadeMaskControl へ設定する。</summary>
    /// <remarks>
    /// <see cref="FadeMaskControl.ExternalRenderer"/> は CLR プロパティのため XAML バインドできない。<br/>
    /// そのためコードビハインドで ViewModel の <see cref="MainWindowViewModel.Renderer"/> を直接設定する。<br/>
    /// レンダラーのプロパティ変更は自動で再描画されないため、描画に影響する変更を検知して
    /// <see cref="Overlay"/> の再描画を要求する（モード切替・選択矩形・外観）。
    /// </remarks>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // 外部レンダラーを設定（モード別描画の切り替えに使用）
            Overlay.ExternalRenderer = vm.Renderer;

            vm.PropertyChanged += (_, args) =>
            {
                // 選択矩形の変更を FadeMaskControl へ反映する（LogicalSelectionRect は CLR プロパティのため AXAML バインド不可）
                if (args.PropertyName == nameof(MainWindowViewModel.SelectionRect))
                {
                    var r = vm.SelectionRect;
                    Overlay.LogicalSelectionRect = r.Width > 0 && r.Height > 0 ? r.ToRectD() : null;
                }
                // 描画に影響するプロパティ（モード・選択矩形・外観）の変更で再描画を要求する
                if (args.PropertyName is nameof(MainWindowViewModel.SelectedModeIndex)
                    or nameof(MainWindowViewModel.SelectionRect)
                    or nameof(MainWindowViewModel.OverlayColor)
                    or nameof(MainWindowViewModel.BorderColor)
                    or nameof(MainWindowViewModel.BorderThickness)
                    or nameof(MainWindowViewModel.TextColor)
                    or nameof(MainWindowViewModel.TextBackgroundColor)
                    or nameof(MainWindowViewModel.FontSize)
                    or nameof(MainWindowViewModel.ShowSizeLabel)
                    or nameof(MainWindowViewModel.SizeLabelOverride)
                    or nameof(MainWindowViewModel.RendererScaling)
                    or nameof(MainWindowViewModel.DisplayScaleScaling)
                    or nameof(MainWindowViewModel.InfoLabelVisible)
                    or nameof(MainWindowViewModel.InfoLabelColor)
                    or nameof(MainWindowViewModel.InfoLabelBackgroundColor)
                    or nameof(MainWindowViewModel.InfoLabelFontSize))
                {
                    Overlay.InvalidateVisual();
                }
            };
        }
    }
}

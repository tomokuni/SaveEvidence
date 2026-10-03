using Avalonia.Controls;
using Avalonia.Input;
using EsUtil.SaveEvidence.LineDetection.Sample.ViewModels;

namespace EsUtil.SaveEvidence.LineDetection.Sample.Views;

/// <summary>「データ検証」ページのコードビハインド。 検出結果リストのホバー・クリックを担う。</summary>
/// <remarks>
/// 検出結果リストの行のホバー/クリックは <see cref="DetectionContext.SetLineHover"/> / <see cref="DetectionContext.ToggleSelect"/> へ委譲する。
/// </remarks>
public partial class VerifyPage : UserControl
{
    /// <summary>ビューモデル。</summary>
    private VerifyPageViewModel? _vm;

    /// <summary>VerifyPage を初期化する。</summary>
    public VerifyPage()
    {
        InitializeComponent();
    }

    /// <summary>DataContext が設定されたとき、ビューモデルを保持する。</summary>
    /// <param name="e">依存プロパティ変更イベント。</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _vm = DataContext as VerifyPageViewModel;
    }

    /// <summary>検出結果リストのホバー開始で、対応する線分のホバー状態を有効にする。</summary>
    /// <param name="sender">イベント送信元（DataTemplate 内の StackPanel）。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnLinePointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: LineItem line })
        {
            _vm?.Context.SetLineHover(line, true);
        }
    }

    /// <summary>検出結果リストのホバー終了で、対応する線分のホバー状態を解除する。</summary>
    /// <param name="sender">イベント送信元（DataTemplate 内の StackPanel）。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnLinePointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: LineItem line })
        {
            _vm?.Context.SetLineHover(line, false);
        }
    }

    /// <summary>検出結果リストのクリックで、対応する線分の選択状態をトグルする。</summary>
    /// <param name="sender">イベント送信元（DataTemplate 内の StackPanel）。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnLinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: LineItem line })
        {
            _vm?.Context.ToggleSelect(line);
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using EsUtil.SaveEvidence.Parts.LineDetection.Sample.Imaging;
using EsUtil.SaveEvidence.Parts.LineDetection.Sample.ViewModels;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.Views;

/// <summary>「線分検出」ページのコードビハインド。 検出結果リストのホバー・クリックと、クリップボード貼り付けを担う。</summary>
/// <remarks>
/// 検出結果リストの行のホバー/クリックは <see cref="DetectionContext.SetLineHover"/> / <see cref="DetectionContext.ToggleSelect"/> へ委譲する。<br/>
/// Ctrl+V でクリップボードの画像を貼り付け、選択中の検出方式で再検出する。
/// </remarks>
public partial class DetectPage : UserControl
{
    /// <summary>ビューモデル。</summary>
    private DetectPageViewModel? _vm;

    /// <summary>DetectPage を初期化する。</summary>
    public DetectPage()
    {
        InitializeComponent();
    }

    /// <summary>DataContext が設定されたとき、ビューモデルを保持する。</summary>
    /// <param name="e">依存プロパティ変更イベント。</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _vm = DataContext as DetectPageViewModel;
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

    /// <summary>Ctrl+V でクリップボードの画像を貼り付ける（ショートカットキー）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// Ctrl+V が押された場合のみ処理し、貼り付け後の画像で選択中の方式により検出する。
    /// </remarks>
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            await PasteImageFromClipboardAsync();
            e.Handled = true;
        }
    }

    /// <summary>クリップボードの画像を取得して ViewModel へ渡す。</summary>
    /// <remarks>
    /// 1. Avalonia のクリップボード API（<see cref="IClipboard.TryGetDataAsync"/> + <c>TryGetBitmapAsync</c>）で取得を試みる<br/>
    /// 2. 取得できない場合は Win32 の CF_DIB（<see cref="ClipboardImageReader"/>）で取得する（Windows 専用）
    /// </remarks>
    private async Task PasteImageFromClipboardAsync()
    {
        var image = await TryReadBitmapFromClipboardAsync();
        image ??= ClipboardImageReader.ReadImageFromDib();
        _vm?.SetPastedImage(image);
    }

    /// <summary>Avalonia クリップボードから画像を読み取る。</summary>
    /// <returns>取得した Bitmap。 画像がクリップボードに無い場合は null。</returns>
    /// <remarks>
    /// <see cref="IClipboard.TryGetDataAsync"/> でクリップボードのデータ転送を取得し、<br/>
    /// 各アイテムを <c>TryGetBitmapAsync</c> で画像に変換する。
    /// </remarks>
    private async Task<Avalonia.Media.Imaging.Bitmap?> TryReadBitmapFromClipboardAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return null;
        }

        var dataTransfer = await clipboard.TryGetDataAsync();
        if (dataTransfer is null)
        {
            return null;
        }

        foreach (var item in dataTransfer.Items)
        {
            var bitmap = await item.TryGetBitmapAsync();
            if (bitmap is not null)
            {
                return bitmap;
            }
        }

        return null;
    }
}

using app.Enum;
using app.Helpers;
using app.Models;
using app.Services;
using app.ViewModels;
using app.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace app.Views.Pages;

/// <summary>
/// アプリケーション設定ページ。
/// </summary>
/// <remarks>
/// キャプチャ設定、保存設定、ルーペ設定、ホットキー設定、画像設定、色設定を
/// 一画面で編集する。ホットキー設定は従来の HotkeyWindow の機能を統合した。<br/>
/// 全設定は変更即座に自動保存される。ホットキーの有効化はキャプチャページ表示時に行う。<br/>
/// </remarks>
public sealed partial class SettingsPage : UserControl
{
    private MainViewModel _viewModel = null!;
    private SettingsService _settingsService = null!;
    private TextBox? _currentHotkeyTextBox;
    private HotkeyTarget? _currentHotkeyTarget;

    // 各キャプチャタイプに対応する設定プロパティ
    private enum HotkeyTarget { SelectScreen, WindowSelect, AreaSelect }

    /// <summary>
    /// SettingsPage を初期化する。
    /// </summary>
    public SettingsPage()
    {
        InitializeComponent();

        if (Avalonia.Controls.Design.IsDesignMode) return;

        _viewModel = Program.ServiceProvider.GetRequiredService<MainViewModel>();
        _settingsService = (SettingsService)Program.ServiceProvider.GetRequiredService<ISettingsService>();
        DataContext = _viewModel;

        // 全設定変更を即時ファイル保存する
        _settingsService.Current.PropertyChanged += (_, _) => _settingsService.Save();

        // ホットキー入力のキーボードフックを各 TextBox に直接登録
        // （UserControl レベルだとイベントが到達しないケースがあるため）
        foreach (var tb in new[] { SelectScreenTextBox, WindowSelectTextBox, AreaSelectTextBox })
        {
            tb.AddHandler(InputElement.KeyDownEvent, OnHotkeyTextBoxKeyDown,
                RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }

    /// <summary>色選択ボタンクリック — カラーピッカーダイアログを開いて選択色を反映する。</summary>
    private async void OnColorPickerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string propertyName)
        {
            // 現在の色値を取得
            var currentColor = propertyName switch
            {
                "CaptureBorderColor" => _viewModel.Settings.CaptureBorderColor,
                "LoupeCrossColor" => _viewModel.Settings.LoupeCrossColor,
                "LoupeFrameColor" => _viewModel.Settings.LoupeFrameColor,
                _ => "#FFFFFF"
            };

            // TopLevel から親ウィンドウを取得して Win32 標準カラーピッカーを表示
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var selectedColor = await ColorPickerDialog.ShowAsync(topLevel, currentColor);

            if (selectedColor is not null)
            {
                switch (propertyName)
                {
                    case "CaptureBorderColor":
                        _viewModel.Settings.CaptureBorderColor = selectedColor;
                        break;
                    case "LoupeCrossColor":
                        _viewModel.Settings.LoupeCrossColor = selectedColor;
                        break;
                    case "LoupeFrameColor":
                        _viewModel.Settings.LoupeFrameColor = selectedColor;
                        break;
                }
            }
        }
    }

    /// <summary>フォルダ選択ダイアログを開く。</summary>
    private async void OnBrowseFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // TopLevel を取得して StorageProvider を使用
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "保存先フォルダを選択",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            _viewModel.SaveFolderPath = folders[0].Path.LocalPath;
        }
    }

    /// <summary>ホットキー設定用テキストボックスがクリックされた時の処理。</summary>
    private void OnHotkeyTextBoxPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            EnterHotkeyCaptureMode(tb);
        }
    }

    /// <summary>指定されたテキストボックスをホットキー入力受付状態にする。</summary>
    private void EnterHotkeyCaptureMode(TextBox tb)
    {
        // 以前のテキストボックスのハイライトを解除
        if (_currentHotkeyTextBox is not null && _currentHotkeyTextBox != tb)
        {
            _currentHotkeyTextBox.ClearValue(TextBlock.BackgroundProperty);
        }

        _currentHotkeyTextBox = tb;
        _currentHotkeyTextBox.Background = Avalonia.Media.Brushes.LightYellow;
        _currentHotkeyTextBox.Focus();
        _currentHotkeyTarget = GetHotkeyTarget(tb);
    }

    /// <summary>キー押下をキャプチャしてホットキーとして設定する（各 TextBox の KeyDown）。</summary>
    private void OnHotkeyTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        // 修飾キーのみの入力を無視
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        // 修飾キーを取得
        var modifiers = HotKeyModifiers.None;
        if ((e.KeyModifiers & KeyModifiers.Control) != 0) modifiers |= HotKeyModifiers.Control;
        if ((e.KeyModifiers & KeyModifiers.Shift) != 0) modifiers |= HotKeyModifiers.Shift;
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0) modifiers |= HotKeyModifiers.Alt;

        // 修飾キーなし → 通常のテキスト入力として扱う
        if (modifiers == HotKeyModifiers.None) return;

        // 修飾キーあり → ホットキーとして設定
        var target = GetHotkeyTarget(tb);
        if (!target.HasValue) return;

        var setting = new HotKeySetting(modifiers, e.Key);
        SetHotkey(target.Value, setting);
        tb.Text = setting.ToString();
        e.Handled = true;

        // キャプチャ状態を解除
        tb.ClearValue(TextBlock.BackgroundProperty);
        _currentHotkeyTextBox = null;
        _currentHotkeyTarget = null;
    }

    /// <summary>テキストボックスからフォーカスが外れた時にテキスト入力をホットキーとして解釈する。</summary>
    private void OnHotkeyTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;

        var text = tb.Text;
        if (string.IsNullOrWhiteSpace(text) || text == "(未設定)")
        {
            var target = GetHotkeyTarget(tb);
            if (target.HasValue && GetCurrentHotkey(target.Value).Key != Key.None)
            {
                ClearHotkey(target.Value, tb);
            }
            return;
        }

        if (TryParseHotKeyText(text, out var setting))
        {
            var target = GetHotkeyTarget(tb);
            if (target.HasValue)
            {
                SetHotkey(target.Value, setting);
                // 表示を正規化
                tb.Text = setting.ToString();
            }
        }
        else
        {
            // 解釈できない場合は現在の設定値に戻す
            var target = GetHotkeyTarget(tb);
            if (target.HasValue)
            {
                var current = GetCurrentHotkey(target.Value);
                tb.Text = current.Key == Key.None ? "(未設定)" : current.ToString();
            }
        }

        // キャプチャ状態を解除
        tb.ClearValue(TextBlock.BackgroundProperty);
        if (_currentHotkeyTextBox == tb)
        {
            _currentHotkeyTextBox = null;
            _currentHotkeyTarget = null;
        }
    }

    /// <summary>TextBox に対応するホットキーターゲットを取得する。</summary>
    private HotkeyTarget? GetHotkeyTarget(TextBox tb)
    {
        if (tb == SelectScreenTextBox) return HotkeyTarget.SelectScreen;
        if (tb == WindowSelectTextBox) return HotkeyTarget.WindowSelect;
        if (tb == AreaSelectTextBox) return HotkeyTarget.AreaSelect;
        return null;
    }

    /// <summary>指定されたターゲットにホットキーを設定する。</summary>
    private void SetHotkey(HotkeyTarget target, HotKeySetting setting)
    {
        switch (target)
        {
            case HotkeyTarget.SelectScreen: _viewModel.Settings.SelectScreenHotKey = setting; break;
            case HotkeyTarget.WindowSelect: _viewModel.Settings.WindowSelectHotKey = setting; break;
            case HotkeyTarget.AreaSelect: _viewModel.Settings.AreaSelectHotKey = setting; break;
        }
    }

    /// <summary>指定されたターゲットの現在のホットキーを取得する。</summary>
    private HotKeySetting GetCurrentHotkey(HotkeyTarget target) => target switch
    {
        HotkeyTarget.SelectScreen => _viewModel.Settings.SelectScreenHotKey,
        HotkeyTarget.WindowSelect => _viewModel.Settings.WindowSelectHotKey,
        HotkeyTarget.AreaSelect => _viewModel.Settings.AreaSelectHotKey,
        _ => new HotKeySetting(HotKeyModifiers.None, Key.None)
    };

    /// <summary>ホットキー文字列を解釈して HotKeySetting に変換する。</summary>
    /// <remarks>
    /// "Ctrl + Shift + Q", "Ctrl+Shift+Q" 等の形式に対応する。大文字小文字は区別しない。<br/>
    /// </remarks>
    /// <param name="text">解釈する文字列</param>
    /// <param name="setting">変換結果</param>
    /// <returns>解釈に成功した場合は true</returns>
    private static bool TryParseHotKeyText(string text, out HotKeySetting setting)
    {
        setting = new HotKeySetting(HotKeyModifiers.None, Key.None);
        if (string.IsNullOrWhiteSpace(text)) return false;

        // "Ctrl + Shift + Q" → ["Ctrl ", " Shift ", " Q"] → trim
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;

        var modifiers = HotKeyModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotKeyModifiers.Control,
                "shift" => HotKeyModifiers.Shift,
                "alt" => HotKeyModifiers.Alt,
                "win" or "windows" => HotKeyModifiers.Windows,
                _ => HotKeyModifiers.None
            };
        }

        // 最低1つの修飾キーが必要
        if (modifiers == HotKeyModifiers.None) return false;

        var keyName = parts[^1];
        if (System.Enum.TryParse<Key>(keyName, true, out var key) && key != Key.None)
        {
            setting = new HotKeySetting(modifiers, key);
            return true;
        }

        return false;
    }

    /// <summary>色 ComboBox のフォーカス喪失時、値が無効なら最終有効値に戻す。</summary>
    private void OnColorComboBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ComboBox { Tag: string propertyName })
        {
            _viewModel.Settings.RevertColorToLastValid(propertyName);
        }
    }

    /// <summary>色選択ドロップダウン選択後にテキストの選択状態を解除する。</summary>
    private void OnColorComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;

        // テキストが自動選択されるのを解除（選択後のイベント完了後に実行）
        Dispatcher.UIThread.Post(() =>
        {
            var textBox = FindTextBoxInVisualTree(comboBox);
            if (textBox is not null)
            {
                var len = textBox.Text?.Length ?? 0;
                textBox.SelectionStart = len;
                textBox.SelectionEnd = len;
            }
        });
    }

    /// <summary>ビジュアルツリーから TextBox を再帰検索する。</summary>
    private static TextBox? FindTextBoxInVisualTree(Avalonia.Visual visual)
    {
        if (visual is TextBox tb) return tb;
        foreach (var child in visual.GetVisualChildren())
        {
            var result = FindTextBoxInVisualTree(child);
            if (result is not null) return result;
        }
        return null;
    }

    private void OnClearSelectScreenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ClearHotkey(HotkeyTarget.SelectScreen, SelectScreenTextBox);

    private void OnClearWindowSelectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ClearHotkey(HotkeyTarget.WindowSelect, WindowSelectTextBox);

    private void OnClearAreaSelectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ClearHotkey(HotkeyTarget.AreaSelect, AreaSelectTextBox);

    /// <summary>指定されたホットキーをクリアする。</summary>
    private void ClearHotkey(HotkeyTarget target, TextBox textBox)
    {
        var noneSetting = new HotKeySetting(HotKeyModifiers.None, Key.None);
        switch (target)
        {
            case HotkeyTarget.SelectScreen:
                _viewModel.Settings.SelectScreenHotKey = noneSetting;
                break;
            case HotkeyTarget.WindowSelect:
                _viewModel.Settings.WindowSelectHotKey = noneSetting;
                break;
            case HotkeyTarget.AreaSelect:
                _viewModel.Settings.AreaSelectHotKey = noneSetting;
                break;
        }
        textBox.Text = "(未設定)";
    }

    /// <summary>色名の並び順（色名順／色相順）。</summary>
    private ColorSortMode _colorSortMode = ColorSortMode.ByName;

    /// <summary>色の並び順変更時、全カラーComboBoxのItemsSourceを差し替える。</summary>
    private void OnColorSortModeChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo)
        {
            _colorSortMode = combo.SelectedIndex switch
            {
                0 => ColorSortMode.ByName,
                1 => ColorSortMode.ByHue,
                _ => ColorSortMode.ByName
            };
            UpdateColorComboItemsSource();
        }
    }

    /// <summary>現在の並び順に従って全カラーComboBoxのItemsSourceを更新する。</summary>
    /// <remarks>XAML初期化中は各 ComboBox が null の場合があるため null チェックを行う。</remarks>
    private void UpdateColorComboItemsSource()
    {
        var items = _colorSortMode == ColorSortMode.ByHue ? ColorEntry.AllByHue : ColorEntry.All;
        if (CaptureBorderColorCombo is not null)
            CaptureBorderColorCombo.ItemsSource = items;
        if (LoupeCrossColorCombo is not null)
            LoupeCrossColorCombo.ItemsSource = items;
        if (LoupeFrameColorCombo is not null)
            LoupeFrameColorCombo.ItemsSource = items;
    }
}



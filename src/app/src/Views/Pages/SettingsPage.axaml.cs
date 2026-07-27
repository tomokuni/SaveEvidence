using Estable.AvaloniaUI.Platform;
using app.Services;
using app.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
// Enum は名前空間 app.Enum と衝突するため、System.Enum を SystemEnum 別名で解決する
using SystemEnum = System.Enum;

namespace app.Views.Pages;

/// <summary>
/// アプリケーション設定ページ。
/// </summary>
/// <remarks>
/// キャプチャ設定、保存設定、ルーペ設定、ホットキー設定、画像設定、色設定を
/// 一画面で編集する。<br/>
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

        if (Design.IsDesignMode) return;

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

        // 保存先フォルダの ToolTip（{MyPictures} はマークアップ拡張と衝突するためコード側で構築）
        ToolTip.SetTip(SaveFolderTextBox, BuildSaveFolderToolTip());
    }

    /// <summary>保存先フォルダの ToolTip（プレースホルダの説明）を構築する。</summary>
    /// <returns>ToolTip として表示するパネル。</returns>
    /// <remarks>
    /// <c>{MyPictures}</c> は XAML のマークアップ拡張（<c>{...}</c>）と解釈されるため、<br/>
    /// コードビハインドで構築する（AXAML の属性/要素構文では表現できない）。<br/>
    /// <c>{MyPictures}</c> はピクチャフォルダ（<see cref="Environment.SpecialFolder.MyPictures"/>）へ展開される。
    /// </remarks>
    private static object BuildSaveFolderToolTip()
    {
        var panel = new StackPanel { MaxWidth = 380 };

        // 見出し
        panel.Children.Add(new TextBlock
        {
            Text = "保存先フォルダ",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 4),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "保存先フォルダのパスを入力します。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 6),
        });

        // プレースホルダ
        panel.Children.Add(new TextBlock
        {
            Text = "プレースホルダ",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "{MyPictures} → ピクチャフォルダ",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"（{Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)}）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 6),
        });

        // 例
        panel.Children.Add(new TextBlock
        {
            Text = "例",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $@"{{MyPictures}}\SaveEvidence →",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)}\\\\SaveEvidence",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 6),
        });

        // バリデーション
        panel.Children.Add(new TextBlock
        {
            Text = "バリデーション",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "存在しないパスは枠が赤くなります。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 0),
        });

        return panel;
    }

    /// <summary>フォルダ選択ダイアログを開く。</summary>
    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
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

    /// <summary>外観設定をデフォルト値へ戻す。</summary>
    /// <remarks>
    /// <see cref="Settings.ResetAppearanceToDefaults"/> で外観設定（選択枠・暗転・ハンドル・マーカー・
    /// ルーペ・市松模様・ラベル）をデフォルト値へ戻す。<br/>
    /// コンストラクタで <see cref="SettingsService.Current"/> の <c>PropertyChanged</c> を購読しているため、
    /// 変更は自動保存される。
    /// </remarks>
    private void OnResetAppearanceToDefaultsClick(object? sender, RoutedEventArgs e)
    {
        _settingsService.Current.ResetAppearanceToDefaults();
    }

    /// <summary>ホットキー設定用テキストボックスがクリックされた時の処理。</summary>
    private void OnHotkeyTextBoxPointerPressed(object? sender, PointerPressedEventArgs e)
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
        _currentHotkeyTextBox.Background = Brushes.LightYellow;
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
    private void OnHotkeyTextBoxLostFocus(object? sender, RoutedEventArgs e)
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
        if (SystemEnum.TryParse<Key>(keyName, true, out var key) && key != Key.None)
        {
            setting = new HotKeySetting(modifiers, key);
            return true;
        }

        return false;
    }

    private void OnClearSelectScreenClick(object? sender, RoutedEventArgs e)
        => ClearHotkey(HotkeyTarget.SelectScreen, SelectScreenTextBox);

    private void OnClearWindowSelectClick(object? sender, RoutedEventArgs e)
        => ClearHotkey(HotkeyTarget.WindowSelect, WindowSelectTextBox);

    private void OnClearAreaSelectClick(object? sender, RoutedEventArgs e)
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

}



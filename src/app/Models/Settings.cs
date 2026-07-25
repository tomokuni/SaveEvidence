using app.Enum;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace app.Models;

/// <summary>
/// アプリケーションの設定を表すデータモデル（変更通知対応）。
/// </summary>
/// <remarks>
/// CommunityToolkit.Mvvm の <see cref="ObservableObject"/> を継承し、
/// <c>[ObservableProperty]</c> 属性により自動的に変更通知を発行する。<br/>
/// 永続化ロジックは一切含まず、読み書きは <see cref="Services.SettingsService"/> を介して行う。<br/>
/// </remarks>
public sealed partial class Settings : ObservableObject
{
    /// <summary>スクリーン選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _selectScreenHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Key.Q);

    /// <summary>ウィンドウ選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _windowSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Key.W);

    /// <summary>範囲選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _areaSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Key.E);

    /// <summary>ファイル名テンプレート文字列を取得または設定する。{date}や{time}のプレースホルダを使用可能。</summary>
    [ObservableProperty]
    private string _fileNameTemplate = "screenshot_{date}_{time}.png";

    /// <summary>キャプチャ画像の保存先フォルダパスを取得または設定する。</summary>
    [ObservableProperty]
    private string _saveFolderPath = string.Empty;

    /// <summary>MainWindow のウィンドウ位置・サイズを取得または設定する。</summary>
    [ObservableProperty]
    private WindowBounds? _mainWindowBounds;

    /// <summary>MainWindow のウィンドウ状態を取得または設定する。</summary>
    [ObservableProperty]
    private WindowStateValue _mainWindowState = WindowStateValue.Normal;

    /// <summary>FolderViewForm の特大アイコンサイズ（ピクセル）。</summary>
    [ObservableProperty]
    private int _folderExtraLargeIconSize = 320;

    /// <summary>FolderViewForm の大アイコンサイズ（ピクセル）。</summary>
    [ObservableProperty]
    private int _folderLargeIconSize = 240;

    /// <summary>FolderViewForm の中アイコンサイズ（ピクセル）。</summary>
    [ObservableProperty]
    private int _folderMediumIconSize = 160;

    /// <summary>プレビュー画像の中央寄せ（true）／左上寄せ（false）を取得または設定する。</summary>
    [ObservableProperty]
    private bool _centerAlign = true;

    /// <summary>キャプチャ時（SelectionWindow）の選択枠境界線色を取得または設定する。</summary>
    [ObservableProperty]
    private string _captureBorderColor = "White";

    /// <summary>キャプチャ時（SelectionWindow）の選択枠不透明度（0.0〜1.0）を取得または設定する。</summary>
    [ObservableProperty]
    private double _captureBorderOpacity = 0.5;

    /// <summary>キャプチャ時のテキスト背景不透明度（0.0〜1.0）を取得または設定する。</summary>
    [ObservableProperty]
    private double _captureOverlayOpacity = 0.5;

    /// <summary>ルーペ十字線の色を取得または設定する。</summary>
    [ObservableProperty]
    private string _loupeCrossColor = "Red";

    /// <summary>ルーペ外枠の色を取得または設定する。</summary>
    [ObservableProperty]
    private string _loupeFrameColor = "White";

    /// <summary>ルーペ外枠の太さ（ピクセル）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeFrameWidth = 2;

    /// <summary>ルーペ十字線の不透明度（0.0〜1.0）を取得または設定する。</summary>
    [ObservableProperty]
    private double _loupeCrossOpacity = 0.5;

    /// <summary>ルーペの拡大率（4〜32、偶数推奨）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeZoomLevel = 8;

    /// <summary>ルーペの一辺のサイズ（ピクセル、64〜512）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeSize = 170;

    /// <summary>プレビュー市松模様の1タイルサイズ（ピクセル、1〜20、デフォルト2）を取得または設定する。</summary>
    [ObservableProperty]
    private int _previewCheckerSize = 2;

    /// <summary>ウィンドウキャプチャ時の取得方式。</summary>
    [ObservableProperty]
    private WindowCaptureMode _captureMode = WindowCaptureMode.PrintWindow;

    /// <summary>ComboBox SelectedIndex バインド用のキャプチャモードインデックス。</summary>
    /// <remarks>
    /// Avalonia の ComboBox SelectedIndex は int型のみを受け付けるため、<br/>
    /// <see cref="WindowCaptureMode"/> enum との変換用に <c>int</c> プロパティを別途用意する。<br/>
    /// 値が変更されると <see cref="CaptureMode"/> にも反映される。<br/>
    /// </remarks>
#pragma warning disable MVVMTK0034 // バッキングフィールド直接参照（再帰防止のため意図的）
    public int CaptureModeIndex
    {
        get => (int)_captureMode;
        set
        {
            if ((int)_captureMode != value)
            {
                _captureMode = (WindowCaptureMode)value;
                OnPropertyChanged(nameof(CaptureModeIndex));
                OnPropertyChanged(nameof(CaptureMode));
            }
        }
    }
    /// <summary>キャプチャ時にメインウィンドウを非表示にするかどうかを取得または設定する。</summary>
    [ObservableProperty]
    private bool _hideMainWindowOnCapture = true;

    // ────────────────────────────────
    // 色設定のバリデーションとフォーカス喪失時の復元
    // ────────────────────────────────

    /// <summary>各色プロパティの直近の有効値を保持する辞書（フォーカス喪失時の復元用）。</summary>
    private readonly Dictionary<string, string> _lastValidValues = new();

    /// <summary>色文字列が有効か検証する。</summary>
    private static bool IsValidColor(string? value)
        => !string.IsNullOrWhiteSpace(value) && Color.TryParse(value, out _);

    partial void OnCaptureBorderColorChanged(string value)
    {
        if (IsValidColor(value))
            _lastValidValues[nameof(CaptureBorderColor)] = value;
    }

    partial void OnLoupeCrossColorChanged(string value)
    {
        if (IsValidColor(value))
            _lastValidValues[nameof(LoupeCrossColor)] = value;
    }

    partial void OnLoupeFrameColorChanged(string value)
    {
        if (IsValidColor(value))
            _lastValidValues[nameof(LoupeFrameColor)] = value;
    }

    /// <summary>指定された色プロパティの値が無効な場合に、最終有効値に戻す。</summary>
    /// <remarks>
    /// 設定画面の ComboBox の LostFocus イベントから呼び出される。<br/>
    /// </remarks>
    public void RevertColorToLastValid(string propertyName)
    {
        if (!_lastValidValues.TryGetValue(propertyName, out var lastValid))
            return;

        switch (propertyName)
        {
            case nameof(CaptureBorderColor):
                if (!IsValidColor(CaptureBorderColor))
                    CaptureBorderColor = lastValid;
                break;
            case nameof(LoupeCrossColor):
                if (!IsValidColor(LoupeCrossColor))
                    LoupeCrossColor = lastValid;
                break;
            case nameof(LoupeFrameColor):
                if (!IsValidColor(LoupeFrameColor))
                    LoupeFrameColor = lastValid;
                break;
        }
    }
}
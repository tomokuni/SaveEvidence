using app.Enum;
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
    private HotKeySetting _selectScreenHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Keys.Q);

    /// <summary>ウィンドウ選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _windowSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Keys.W);

    /// <summary>範囲選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _areaSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Keys.E);

    /// <summary>ファイル名テンプレート文字列を取得または設定する。{date}や{time}のプレースホルダを使用可能。</summary>
    [ObservableProperty]
    private string _fileNameTemplate = "screenshot_{date}_{time}.png";

    /// <summary>キャプチャ画像の保存先フォルダパスを取得または設定する。</summary>
    [ObservableProperty]
    private string _saveFolderPath = string.Empty;

    /// <summary>FolderViewForm の表示モードインデックス（0=特大,1=大,2=中,3=小）を取得または設定する。</summary>
    [ObservableProperty]
    private int _folderViewModeIndex = 1;

    /// <summary>FolderViewForm でファイル名ソートの昇順（true）／降順（false）を取得または設定する。</summary>
    [ObservableProperty]
    private bool _folderSortAscending = true;

    /// <summary>MainForm のウィンドウ位置・サイズを取得または設定する。次回起動時に復元に使用。</summary>
    [ObservableProperty]
    private Rectangle? _mainFormBounds;

    /// <summary>MainForm のウィンドウ状態（最大化/最小化/通常）を取得または設定する。</summary>
    [ObservableProperty]
    private FormWindowState _mainFormWindowState = FormWindowState.Normal;

    /// <summary>FolderViewForm のウィンドウ位置・サイズを取得または設定する。次回起動時に復元に使用。</summary>
    [ObservableProperty]
    private Rectangle? _folderViewFormBounds;

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

    /// <summary>キャプチャ時（SelectionForm）の選択枠境界線色を取得または設定する。</summary>
    [ObservableProperty]
    private string _captureBorderColor = "White";

    /// <summary>プレビュー範囲選択（MainForm crop）の境界線色を取得または設定する。</summary>
    [ObservableProperty]
    private string _cropBorderColor = "White";

    /// <summary>ルーペ十字線の色を取得または設定する。</summary>
    [ObservableProperty]
    private string _loupeCrossColor = "Red";

    /// <summary>ルーペ外枠の色を取得または設定する。</summary>
    [ObservableProperty]
    private string _loupeFrameColor = "White";

    /// <summary>ルーペ外枠の太さ（ピクセル）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeFrameWidth = 2;

    /// <summary>ルーペの拡大率（4〜32、偶数推奨）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeZoomLevel = 8;

    /// <summary>ルーペの一辺のサイズ（ピクセル、64〜512）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeSize = 170;

    /// <summary>ルーペの表示モード（表示/非表示/自動）を取得または設定する。</summary>
    [ObservableProperty]
    private LoupeMode _loupeModeValue = LoupeMode.Hide;

    /// <summary>ウィンドウキャプチャ時の取得方式。</summary>
    [ObservableProperty]
    private WindowCaptureMode _captureMode = WindowCaptureMode.PrintWindow;
}

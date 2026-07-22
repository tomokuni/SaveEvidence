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
    /// <summary>選択スクリーンキャプチャのホットキー</summary>
    [ObservableProperty]
    private HotKeySetting _selectScreenHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Keys.Q);

    /// <summary>ウィンドウ選択キャプチャのホットキー</summary>
    [ObservableProperty]
    private HotKeySetting _windowSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Keys.W);

    /// <summary>範囲選択キャプチャのホットキー</summary>
    [ObservableProperty]
    private HotKeySetting _areaSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, Keys.E);

    /// <summary>ファイル名テンプレート</summary>
    [ObservableProperty]
    private string _fileNameTemplate = "screenshot_{date}_{time}.png";

    /// <summary>保存先フォルダパス</summary>
    [ObservableProperty]
    private string _saveFolderPath = string.Empty;

    /// <summary>FolderViewForm の表示モードインデックス</summary>
    [ObservableProperty]
    private int _folderViewModeIndex = 1;

    /// <summary>FolderViewForm のソート昇順フラグ</summary>
    [ObservableProperty]
    private bool _folderSortAscending = true;

    /// <summary>MainForm のウィンドウ位置・サイズ</summary>
    [ObservableProperty]
    private Rectangle? _mainFormBounds;

    /// <summary>MainForm のウィンドウ状態</summary>
    [ObservableProperty]
    private FormWindowState _mainFormWindowState = FormWindowState.Normal;

    /// <summary>FolderViewForm のウィンドウ位置・サイズ</summary>
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

    /// <summary>中央寄せ（true）／左上寄せ（false）</summary>
    [ObservableProperty]
    private bool _centerAlign = true;

    /// <summary>キャプチャ時（SelectionForm）の境界線色</summary>
    [ObservableProperty]
    private string _captureBorderColor = "White";

    /// <summary>プレビュー範囲選択（MainForm crop）の境界線色</summary>
    [ObservableProperty]
    private string _cropBorderColor = "White";

    /// <summary>虫眼鏡十字線の色</summary>
    [ObservableProperty]
    private string _loupeCrossColor = "Red";

    /// <summary>虫眼鏡外枠の色</summary>
    [ObservableProperty]
    private string _loupeFrameColor = "White";

    /// <summary>虫眼鏡外枠の太さ</summary>
    [ObservableProperty]
    private int _loupeFrameWidth = 2;

    /// <summary>ルーペの拡大率（4〜32、偶数推奨）。</summary>
    [ObservableProperty]
    private int _loupeZoomLevel = 8;

    /// <summary>ルーペの一辺のサイズ（ピクセル、64〜512）。</summary>
    [ObservableProperty]
    private int _loupeSize = 170;

    /// <summary>ルーペの表示モード</summary>
    [ObservableProperty]
    private LoupeMode _loupeModeValue = LoupeMode.Hide;

    /// <summary>ウィンドウキャプチャ時の取得方式。</summary>
    [ObservableProperty]
    private WindowCaptureMode _captureMode = WindowCaptureMode.PrintWindow;
}

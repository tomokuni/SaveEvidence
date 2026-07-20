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
    private HotKeySetting _selectScreenHotKey = new() { Modifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift, Key = Keys.Q };

    /// <summary>ウィンドウ選択キャプチャのホットキー</summary>
    [ObservableProperty]
    private HotKeySetting _windowSelectHotKey = new() { Modifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift, Key = Keys.W };

    /// <summary>範囲選択キャプチャのホットキー</summary>
    [ObservableProperty]
    private HotKeySetting _areaSelectHotKey = new() { Modifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift, Key = Keys.E };

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

/// <summary>
/// ホットキーの組合せ（修飾キー＋キー）を表す設定値オブジェクト。
/// </summary>
/// <remarks>
/// <see cref="Modifiers"/> と <see cref="Key"/> の組合せで一意のホットキーを定義する。<br/>
/// <see cref="ToString()"/> で "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す。<br/>
/// </remarks>
public sealed class HotKeySetting
{
    /// <summary>修飾キー（Alt / Control / Shift / Windows の組合せ）</summary>
    public HotKeyModifiers Modifiers { get; set; }

    /// <summary>ホットキーとして割り当てるキー</summary>
    public Keys Key { get; set; }

    /// <summary>ホットキーのユーザー可読な文字列表現（例: "Ctrl + Shift + Q"）を取得する。</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotKeyModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyToSymbolString(Key));
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// キーをユーザー可読なシンボル文字列に変換する。
    /// </summary>
    private static string KeyToSymbolString(Keys key) => key switch
    {
        Keys.D0 => "0", Keys.D1 => "1", Keys.D2 => "2", Keys.D3 => "3", Keys.D4 => "4",
        Keys.D5 => "5", Keys.D6 => "6", Keys.D7 => "7", Keys.D8 => "8", Keys.D9 => "9",
        Keys.Oemtilde => "`", Keys.OemMinus => "-", Keys.Oemplus => "=",
        Keys.OemOpenBrackets => "[", Keys.OemCloseBrackets => "]", Keys.OemPipe => "\\",
        Keys.OemSemicolon => ";", Keys.OemQuotes => "'",
        Keys.Oemcomma => ",", Keys.OemPeriod => ".", Keys.OemQuestion => "/",
        _ => key.ToString()
    };
}

/// <summary>ルーペの表示モードを指定する列挙型。</summary>
public enum LoupeMode
{
    /// <summary>常に表示</summary>
    Show = 0,

    /// <summary>常に非表示</summary>
    Hide = 1,

    /// <summary>範囲選択中のみ自動表示</summary>
    Auto = 2,
}

/// <summary>ホットキーの修飾キーを指定するためのフラグ列挙型。</summary>
/// <remarks>
/// ビットフラグとして設計されており、複数の修飾キーを組み合わせて使用する。<br/>
/// <see cref="HotKeySetting.Modifiers"/> プロパティで使用される。<br/>
/// </remarks>
[Flags]
public enum HotKeyModifiers
{
    /// <summary>修飾キーなし</summary>
    None = 0,

    /// <summary>Alt キー</summary>
    Alt = 0x0001,

    /// <summary>Ctrl キー</summary>
    Control = 0x0002,

    /// <summary>Shift キー</summary>
    Shift = 0x0004,

    /// <summary>Windows キー</summary>
    Windows = 0x0008
}

/// <summary>ウィンドウキャプチャ時の画像取得方式を指定する列挙型。</summary>
/// <remarks>
/// <see cref="PrintWindow"/>: DWM から直接描画内容を取得するため1px枠線の写り込みが発生しない。<br/>
/// ただし 枠の色がクラシック風になり、DirectX/GPU 描画コンテンツでは正しく取得できない場合がある。<br/>
/// <see cref="CopyFromScreen"/>: 従来の画面矩形コピー方式。1px枠線部分に背景が映り込む可能性がある。<br/>
/// ただし、他の Window が前面に重なっている場合は、見た目通りの重なった状態で取得される。<br/>
/// </remarks>
public enum WindowCaptureMode
{
    /// <summary>PrintWindow API を使用（DWM から直接取得、1px枠線防止）。</summary>
    PrintWindow = 0,

    /// <summary>CopyFromScreen を使用（従来方式、1px枠線写り込みあり）。</summary>
    CopyFromScreen = 1,
}

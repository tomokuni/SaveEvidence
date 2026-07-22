using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using app.Models;
using app.Services;

namespace app.ViewModels;

/// <summary>
/// メイン画面の ViewModel。CommunityToolkit.Mvvm ソースジェネレーターにより、
/// プロパティ変更通知とコマンドを自動生成する。
/// </summary>
/// <remarks>
/// <see cref="ObservableObject"/> を継承し、<c>[ObservableProperty]</c> や
/// <c>[RelayCommand]</c> 属性によるソースジェネレーターを活用する。<br/>
/// 設定管理は <see cref="Services.SettingsService"/> を介して行い、疎結合な設計を実現する。<br/>
/// 画像のライフサイクル管理（Dispose）は <c>OnPreviewImageChanging</c> で行う。<br/>
/// </remarks>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private int _currentNumber;
    private string _lastSavedFileName = "";

    /// <summary>画像保存時にインクリメントされる、次回ファイル名に使用する数値成分。</summary>
    public int CurrentNumber => _currentNumber;

    /// <summary>選択モード開始デリゲート（View 側から設定されるコールバック）。</summary>
    public Action<CaptureType>? StartSelectionMode { get; set; }

    /// <summary>
    /// MainViewModel の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="settingsService">設定サービス</param>
    public MainViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;

        var settings = _settingsService.Current;
        _fileNameTemplateText = settings.FileNameTemplate;
        _saveFolderPath = !string.IsNullOrEmpty(settings.SaveFolderPath)
            ? settings.SaveFolderPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SaveEvidence");
        _currentNumber = FileNameTemplate.ExtractCurrentNumber(_fileNameTemplateText);
        UpdateSaveFolderDisplayName();
    }

    /// <summary>プレビュー画像</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
    [NotifyPropertyChangedFor(nameof(CanCopy))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private Image? _previewImage;

    /// <summary>画像が保存済みかどうか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isSaved;

    /// <summary>ファイル名テンプレート文字列</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFileNamePreview))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string _fileNameTemplateText = string.Empty;

    /// <summary>保存先フォルダパス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveFolderDisplayName))]
    private string _saveFolderPath = string.Empty;

    /// <summary>保存先フォルダの表示名</summary>
    [ObservableProperty]
    private string _saveFolderDisplayName = string.Empty;

    /// <summary>現在のズーム率（0=自動）。View 側から設定される。</summary>
    [ObservableProperty]
    private int _zoomPercent;

    /// <summary>切出し選択範囲があるかどうか。View 側から設定される。</summary>
    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>ズームイン可能かどうか。View 側から設定される。</summary>
    [ObservableProperty]
    private bool _canZoomIn;

    /// <summary>ズームアウト可能かどうか。View 側から設定される。</summary>
    [ObservableProperty]
    private bool _canZoomOut;

    /// <summary>プレビュー画像が存在するかどうかを取得する。</summary>
    public bool HasPreviewImage => PreviewImage is not null;

    /// <summary>クリップボードにコピー可能かどうかを取得する。</summary>
    public bool CanCopy => PreviewImage is not null;

    /// <summary>保存可能かどうかを取得する（画像があり、かつ未保存の場合のみ保存可能）。</summary>
    public bool CanSave => PreviewImage is not null && !IsSaved;

    /// <summary>現在のファイル名プレビューテキストを取得する。</summary>
    public string CurrentFileNamePreview
    {
        get
        {
            var displayNumber = _currentNumber;
            return $"保存ファイル名: {FileNameTemplate.Generate(FileNameTemplateText, displayNumber)}";
        }
    }

    /// <summary>現在の状態テキストを取得する（保存済みファイル名を表示）。</summary>
    public string StatusText
    {
        get
        {
            if (IsSaved && !string.IsNullOrEmpty(_lastSavedFileName))
                return $"保存済み {_lastSavedFileName}";
            return "";
        }
    }

    /// <summary>設定オブジェクトを取得する。</summary>
    public Settings Settings => _settingsService.Current;

    /// <summary>
    /// プレビュー画像変更前に古い画像を破棄する。
    /// </summary>
    /// <param name="value">新しい画像（このメソッド内では未使用）</param>
    /// <remarks>
    /// <c>OnPreviewImageChanging</c> はフィールド更新前に呼ばれるため、
    /// <c>_previewImage</c> はまだ変更前の古い画像を保持している。</remarks>
    partial void OnPreviewImageChanging(Image? value)
    {
        _previewImage?.Dispose();
        IsSaved = false;
        _lastSavedFileName = "";
    }

    /// <summary>ファイル名テンプレート変更時に数値成分を抽出し、設定を保存する。</summary>
    partial void OnFileNameTemplateTextChanged(string value)
    {
        _currentNumber = FileNameTemplate.ExtractCurrentNumber(value);
        _settingsService.Current.FileNameTemplate = value;
        _settingsService.Save();
    }

    /// <summary>保存先フォルダ変更時に設定を保存し、表示名を更新する。</summary>
    partial void OnSaveFolderPathChanged(string value)
    {
        _settingsService.Current.SaveFolderPath = value;
        _settingsService.Save();
        UpdateSaveFolderDisplayName();
    }

    /// <summary>
    /// 指定されたスクリーン領域をキャプチャしてプレビューに設定する。
    /// </summary>
    /// <param name="bounds">キャプチャするスクリーン座標の矩形領域</param>
    public void CaptureScreenArea(Rectangle bounds)
    {
        var bitmap = CaptureManager.CaptureArea(bounds);
        PreviewImage = bitmap;
    }

    /// <summary>指定された画像をプレビューとして設定する。</summary>
    /// <param name="image">設定する画像</param>
    public void SetPreviewImage(Image image) => PreviewImage = image;

    /// <summary>画像をクリップボードにコピーする。</summary>
    [RelayCommand]
    public void CopyToClipboard()
    {
        if (PreviewImage is not null)
        {
            Clipboard.SetImage(PreviewImage);
        }
    }

    /// <summary>クリップボードから画像を貼り付ける。</summary>
    /// <returns>貼り付けに成功したかどうか</returns>
    public bool PasteFromClipboard()
    {
        if (!Clipboard.ContainsImage())
        {
            return false;
        }

        var image = Clipboard.GetImage();
        if (image is not null)
        {
            PreviewImage = image;
            return true;
        }

        return false;
    }

    /// <summary>指定された範囲でプレビュー画像を切り出す。</summary>
    /// <param name="bounds">切り出す矩形領域（画像座標）</param>
    [RelayCommand]
    private void CropPreview(Rectangle bounds)
    {
        if (PreviewImage is null)
        {
            return;
        }

        var cropped = ImageProcessor.Crop(PreviewImage, bounds);
        PreviewImage = cropped;
    }

    /// <summary>プレビュー画像からウィンドウ領域を自動判定して切り出す。</summary>
    [RelayCommand]
    private void AutoCropWindow()
    {
        if (PreviewImage is null)
        {
            return;
        }

        var cropped = ImageProcessor.DetectAndCropWindow(PreviewImage);
        PreviewImage = cropped;
    }

    /// <summary>
    /// 現在のプレビュー画像を保存する。
    /// </summary>
    /// <remarks>
    /// 保存後にファイル名の数値をインクリメントし、設定を更新する。<br/>
    /// 同名ファイルが存在する場合は上書き確認のダイアログを表示する。<br/>
    /// 画像形式は拡張子から自動判定する（既定は PNG）。<br/>
    /// </remarks>
    /// <returns>保存に成功したかどうか</returns>
    public bool SaveImage()
    {
        if (PreviewImage is null)
        {
            return false;
        }

        try
        {
            var fileName = FileNameTemplate.Generate(FileNameTemplateText, _currentNumber);
            var filePath = Path.Combine(SaveFolderPath, fileName);

            if (File.Exists(filePath))
            {
                var result = MessageBox.Show(
                    $"ファイル「{fileName}」は既に存在します。上書きしますか？",
                    "上書き確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    return false;
                }
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var format = extension switch
            {
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                ".gif" => System.Drawing.Imaging.ImageFormat.Gif,
                _ => System.Drawing.Imaging.ImageFormat.Png,
            };

            PreviewImage.Save(filePath, format);

            _currentNumber++;
            FileNameTemplateText = FileNameTemplate.IncrementRightmostNumber(FileNameTemplateText, _currentNumber);

            _lastSavedFileName = fileName;
            IsSaved = true;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>保存先フォルダをエクスプローラーで開く。</summary>
    [RelayCommand]
    private void OpenSaveFolder()
    {
        if (!string.IsNullOrEmpty(SaveFolderPath))
        {
            if (!Directory.Exists(SaveFolderPath))
            {
                Directory.CreateDirectory(SaveFolderPath);
            }

            System.Diagnostics.Process.Start("explorer.exe", SaveFolderPath);
        }
    }

    /// <summary>設定を再読み込みして、関連プロパティの変更を通知する。</summary>
    public void ReloadSettings()
    {
        _settingsService.Reload();
        OnPropertyChanged(nameof(FileNameTemplateText));
        OnPropertyChanged(nameof(SaveFolderPath));
        OnPropertyChanged(nameof(SaveFolderDisplayName));
    }

    /// <summary>保存先フォルダの表示名（フォルダ名のみ）を更新する。</summary>
    private void UpdateSaveFolderDisplayName()
    {
        SaveFolderDisplayName = Path.GetFileName(SaveFolderPath);
    }
}

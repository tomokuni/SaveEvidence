using System.Drawing.Imaging;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using app.Converters;
using app.Enum;
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
    private readonly ISettingsService _settingsService;
    private string _lastSavedFileName = "";

    /// <summary>選択モード開始デリゲート（View 側から設定されるコールバック）。</summary>
    public Action<CaptureType>? StartSelectionMode { get; set; }

    /// <summary>画像クリップボード操作のコールバック（View 側から設定）。</summary>
    public Func<Bitmap?, Task<bool>>? CopyToClipboardAsync { get; set; }

    /// <summary>画像貼り付け操作のコールバック（View 側から設定）。</summary>
    public Func<Task<Bitmap?>>? PasteFromClipboardAsync { get; set; }

    /// <summary>上書き確認ダイアログのコールバック（View 側から設定）。</summary>
    public Func<string, Task<bool>>? ConfirmOverwriteAsync { get; set; }

    /// <summary>画像編集（切り出し等）前に View 側で Undo をプッシュするためのコールバック。</summary>
    public Action? BeforeEditAction { get; set; }

    /// <summary>設定再読込後に View 側で UI を更新するためのコールバック。</summary>
    public Action? AfterSettingsReloaded { get; set; }

    /// <summary>
    /// MainViewModel の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="settingsService">設定サービス</param>
    public MainViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        var settings = _settingsService.Current;
        _fileNameTemplateText = settings.FileNameTemplate;
        _saveFolderPath = !string.IsNullOrEmpty(settings.SaveFolderPath)
            ? settings.SaveFolderPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SaveEvidence");
        UpdateSaveFolderDisplayName();
    }

    /// <summary>プレビュー画像（Avalonia Bitmap）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
    [NotifyPropertyChangedFor(nameof(CanCopy))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private Bitmap? _previewImage;

    /// <summary>画像が保存済みかどうか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isSaved;

    /// <summary>ファイル名テンプレート文字列</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFileNamePreview))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasValidTemplate))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
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

    /// <summary>保存可能かどうかを取得する（画像があり、かつ有効なファイル名テンプレートの場合のみ保存可能）。</summary>
    public bool CanSave => PreviewImage is not null && HasValidTemplate;

    /// <summary>貼り付け可能かどうかを取得する。</summary>
    public bool CanPaste => PasteFromClipboardAsync is not null;

    /// <summary>ズーム率の選択肢（0=自動）を取得する。</summary>
    /// <remarks>仕様書SpecSystem.md 11.1節に基づく全18段階。</remarks>
    public int[] ZoomLevels => [0, 25, 33, 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400, 500];

    /// <summary>ComboBox 表示用のズーム率ラベル一覧を取得する。</summary>
    public string[] ZoomLabels => ["自動(Fit)", "25%", "33%", "50%", "67%", "75%", "80%", "90%", "100%", "110%", "125%", "150%", "175%", "200%", "250%", "300%", "400%", "500%"];

    /// <summary>現在のファイル名プレビューテキストを取得する。</summary>
    public string CurrentFileNamePreview
    {
        get
        {
            return $"保存ファイル名: {FileNameTemplate.Generate(FileNameTemplateText)}";
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

    /// <summary>ファイル名テンプレートが有効かどうかを取得する。</summary>
    public bool HasValidTemplate => FileNameTemplate.IsValidTemplate(FileNameTemplateText, out _);

    /// <summary>ファイル名テンプレートの検証エラーメッセージを取得する。有効な場合は null。</summary>
    public string? ValidationMessage
    {
        get
        {
            FileNameTemplate.IsValidTemplate(FileNameTemplateText, out var message);
            return message;
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
    partial void OnPreviewImageChanging(Bitmap? value)
    {
        _previewImage?.Dispose();
        IsSaved = false;
        _lastSavedFileName = "";
    }

    /// <summary>ファイル名テンプレート変更時に数値成分を抽出し、設定を保存する。</summary>
    partial void OnFileNameTemplateTextChanged(string value)
    {
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
    /// <param name="bounds">キャプチャするスクリーン座標の矩形領域（System.Drawing.Rectangle）</param>
    public void CaptureScreenArea(System.Drawing.Rectangle bounds)
    {
        var drawingBmp = CaptureManager.CaptureArea(bounds);
        if (drawingBmp is not null)
        {
            PreviewImage = drawingBmp.ToAvaloniaBitmap();
            drawingBmp.Dispose();
        }
    }

    /// <summary>指定された画像（Avalonia Bitmap）をプレビューとして設定する。</summary>
    /// <param name="image">設定する画像</param>
    public void SetPreviewImage(Bitmap image) => PreviewImage = image;

    /// <summary>画像をクリップボードにコピーする。</summary>
    [RelayCommand]
    public async Task CopyToClipboard()
    {
        if (PreviewImage is not null && CopyToClipboardAsync is not null)
        {
            await CopyToClipboardAsync(PreviewImage);
        }
    }

    /// <summary>クリップボードから画像を貼り付ける。</summary>
    /// <returns>貼り付けに成功したかどうか</returns>
    public async Task<bool> PasteFromClipboard()
    {
        if (PasteFromClipboardAsync is null)
        {
            return false;
        }

        var image = await PasteFromClipboardAsync();
        if (image is not null)
        {
            PreviewImage = image;
            return true;
        }

        return false;
    }

    /// <summary>指定された範囲でプレビュー画像を切り出す。</summary>
    /// <param name="bounds">切り出す矩形領域（画像座標、System.Drawing.Rectangle）</param>
    [RelayCommand]
    private void CropPreview(System.Drawing.Rectangle bounds)
    {
        if (PreviewImage is null)
        {
            return;
        }

        BeforeEditAction?.Invoke();

        // System.Drawing.Bitmap に変換して切り出し処理
        using var drawingBmp = PreviewImage.ToDrawingBitmap();
        var croppedDrawing = ImageProcessor.Crop(drawingBmp, bounds);
        if (croppedDrawing is not null)
        {
            PreviewImage = croppedDrawing.ToAvaloniaBitmap();
        }
    }

    /// <summary>プレビュー画像からウィンドウ領域を自動判定して切り出す。</summary>
    [RelayCommand]
    private void AutoCropWindow()
    {
        if (PreviewImage is null)
        {
            return;
        }

        BeforeEditAction?.Invoke();

        using var drawingBmp = PreviewImage.ToDrawingBitmap();
        var croppedDrawing = ImageProcessor.DetectAndCropWindow(drawingBmp);
        if (croppedDrawing is not null)
        {
            PreviewImage = croppedDrawing.ToAvaloniaBitmap();
        }
    }

    /// <summary>
    /// 現在のプレビュー画像を保存する。
    /// </summary>
    /// <remarks>
    /// 保存後にファイル名の数値をインクリメントし、設定を更新する。<br/>
    /// 同名ファイルが存在する場合は、<see cref="ConfirmOverwriteAsync"/> で確認する。<br/>
    /// 画像形式は拡張子から自動判定する（既定は PNG）。<br/>
    /// </remarks>
    /// <returns>保存に成功したかどうか</returns>
    [RelayCommand]
    public async Task<bool> SaveImageAsync()
    {
        if (PreviewImage is null)
        {
            return false;
        }

        try
        {
            var fileName = FileNameTemplate.Generate(FileNameTemplateText);
            var filePath = Path.Combine(SaveFolderPath, fileName);

            if (File.Exists(filePath))
            {
                if (ConfirmOverwriteAsync is not null)
                {
                    var shouldOverwrite = await ConfirmOverwriteAsync(fileName);
                    if (!shouldOverwrite)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // System.Drawing.Bitmap に変換して保存
            using var drawingBmp = PreviewImage.ToDrawingBitmap();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var format = extension switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".bmp" => ImageFormat.Bmp,
                ".gif" => ImageFormat.Gif,
                _ => ImageFormat.Png,
            };

            drawingBmp.Save(filePath, format);

            FileNameTemplateText = FileNameTemplate.IncrementRightmostNumber(FileNameTemplateText);

            _lastSavedFileName = fileName;
            IsSaved = true;

            return true;
        }
        catch (Exception)
        {
            // 保存失敗時は false を返す
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

        AfterSettingsReloaded?.Invoke();
    }

    /// <summary>保存先フォルダの表示名（フォルダ名のみ）を更新する。</summary>
    private void UpdateSaveFolderDisplayName()
    {
        SaveFolderDisplayName = Path.GetFileName(SaveFolderPath);
    }
}

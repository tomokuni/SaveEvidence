using System.ComponentModel;
using System.Runtime.CompilerServices;
using app.Models;

namespace app.ViewModels;

/// <summary>
/// メイン画面のViewModel。INotifyPropertyChanged を実装し、MVVMライクなデータバインディングを提供する。
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Settings _settings;
    private Image? _previewImage;
    private string _fileNameTemplate;
    private int _currentNumber;
    private string _saveFolderPath = string.Empty;
    private string _saveFolderDisplayName = string.Empty;
    private bool _isSaved;

    /// <summary>
    /// MainViewModel を初期化する
    /// </summary>
    public MainViewModel()
    {
        _settings = Settings.Load();
        _fileNameTemplate = _settings.FileNameTemplate;
        _saveFolderPath = _settings.SaveFolderPath;

        if (string.IsNullOrEmpty(_settings.SaveFolderPath))
        {
            _saveFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            _saveFolderPath = Path.Combine(_saveFolderPath, "SaveEvidence");
        }

        _currentNumber = FileNameTemplate.ExtractCurrentNumber(_fileNameTemplate);
        UpdateSaveFolderDisplayName();
    }

    /// <summary>
    /// プレビュー画像
    /// </summary>
    public Image? PreviewImage
    {
        get => _previewImage;
        set
        {
            if (_previewImage != value)
            {
                _previewImage?.Dispose();
                _previewImage = value;
                IsSaved = false; // 画像変更で保存済み解除
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPreviewImage));
                OnPropertyChanged(nameof(CanCopy));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>
    /// プレビュー画像が存在するかどうか
    /// </summary>
    public bool HasPreviewImage => _previewImage is not null;

    /// <summary>
    /// 画像が保存済みかどうか
    /// </summary>
    public bool IsSaved
    {
        get => _isSaved;
        private set
        {
            if (_isSaved != value)
            {
                _isSaved = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>
    /// クリップボードにコピー可能かどうか
    /// </summary>
    public bool CanCopy => _previewImage is not null;

    /// <summary>
    /// 保存可能かどうか（画像があり、かつ未保存の場合のみ保存可能）
    /// </summary>
    public bool CanSave => _previewImage is not null && !_isSaved;

    /// <summary>
    /// ファイル名テンプレート
    /// </summary>
    public string FileNameTemplateText
    {
        get => _fileNameTemplate;
        set
        {
            if (_fileNameTemplate != value)
            {
                _fileNameTemplate = value;
                _currentNumber = FileNameTemplate.ExtractCurrentNumber(_fileNameTemplate);
                _settings.FileNameTemplate = value;
                _settings.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFileNamePreview));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>
    /// 現在のファイル名プレビュー
    /// </summary>
    public string CurrentFileNamePreview
    {
        get
        {
            var displayNumber = _currentNumber;
            return $"保存ファイル名: {FileNameTemplate.Generate(_fileNameTemplate, displayNumber)}";
        }
    }

    /// <summary>
    /// 保存先フォルダパス
    /// </summary>
    public string SaveFolderPath
    {
        get => _saveFolderPath;
        set
        {
            if (_saveFolderPath != value)
            {
                _saveFolderPath = value;
                _settings.SaveFolderPath = value;
                _settings.Save();
                UpdateSaveFolderDisplayName();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SaveFolderDisplayName));
            }
        }
    }

    /// <summary>
    /// 保存先フォルダ表示名
    /// </summary>
    public string SaveFolderDisplayName
    {
        get => _saveFolderDisplayName;
        private set
        {
            if (_saveFolderDisplayName != value)
            {
                _saveFolderDisplayName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 現在の状態テキスト
    /// </summary>
    public string StatusText
    {
        get
        {
            var imageStatus = HasPreviewImage ? "画像あり" : "画像なし";
            var preview = FileNameTemplate.Generate(_fileNameTemplate, _currentNumber);
            var saved = IsSaved ? " | 保存済み" : "";
            return $"{imageStatus}{saved} | 保存ファイル名: {preview}";
        }
    }

    /// <summary>
    /// 設定オブジェクト
    /// </summary>
    public Settings Settings => _settings;

    /// <summary>
    /// 指定されたスクリーン領域をキャプチャしてプレビューに設定する
    /// </summary>
    public void CaptureScreenArea(Rectangle bounds)
    {
        var bitmap = CaptureManager.CaptureArea(bounds);
        PreviewImage = bitmap;
    }

    /// <summary>
    /// アクティブウィンドウキャプチャを実行する
    /// </summary>


    /// <summary>
    /// ウィンドウ選択キャプチャを実行する（フォームを非表示にして選択モードに）
    /// </summary>
    public Action<CaptureType>? StartSelectionMode { get; set; }

    /// <summary>
    /// 指定された画像をプレビューとして設定する
    /// </summary>
    public void SetPreviewImage(Image image)
    {
        PreviewImage = image;
    }

    /// <summary>
    /// 画像をクリップボードにコピーする
    /// </summary>
    public void CopyToClipboard()
    {
        if (_previewImage is not null)
        {
            Clipboard.SetImage(_previewImage);
        }
    }

    /// <summary>
    /// クリップボードから画像を貼り付ける
    /// </summary>
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

    /// <summary>
    /// プレビュー画像から指定領域を切り出して表示する
    /// </summary>
    public void CropPreview(Rectangle bounds)
    {
        if (_previewImage is null)
        {
            return;
        }

        var cropped = ImageProcessor.Crop(_previewImage, bounds);
        PreviewImage = cropped;
    }

    /// <summary>
    /// プレビュー画像からウィンドウ領域を自動判定して切り出す
    /// </summary>
    public void AutoCropWindow()
    {
        if (_previewImage is null)
        {
            return;
        }

        var cropped = ImageProcessor.DetectAndCropWindow(_previewImage);
        PreviewImage = cropped;
    }

    /// <summary>
    /// 現在のプレビュー画像を保存する。
    /// 保存後にファイル名の数値をインクリメントする。
    /// </summary>
    /// <returns>保存に成功したかどうか</returns>
    public bool SaveImage()
    {
        if (_previewImage is null)
        {
            return false;
        }

        try
        {
            var fileName = FileNameTemplate.Generate(_fileNameTemplate, _currentNumber);
            var filePath = Path.Combine(_saveFolderPath, fileName);

            // 同名ファイルが存在する場合は上書き確認
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

            // 画像形式の判別
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var format = extension switch
            {
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                ".gif" => System.Drawing.Imaging.ImageFormat.Gif,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };

            _previewImage.Save(filePath, format);

            // 保存後に数値をインクリメントし、テンプレートを更新
            _currentNumber++;
            _fileNameTemplate = FileNameTemplate.IncrementRightmostNumber(_fileNameTemplate, _currentNumber);
            _settings.FileNameTemplate = _fileNameTemplate;
            _settings.Save();

            OnPropertyChanged(nameof(FileNameTemplateText));
            OnPropertyChanged(nameof(CurrentFileNamePreview));
            OnPropertyChanged(nameof(StatusText));

            IsSaved = true;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 保存先フォルダをエクスプローラーで開く
    /// </summary>
    public void OpenSaveFolder()
    {
        if (!string.IsNullOrEmpty(_saveFolderPath))
        {
            if (!Directory.Exists(_saveFolderPath))
            {
                Directory.CreateDirectory(_saveFolderPath);
            }

            System.Diagnostics.Process.Start("explorer.exe", _saveFolderPath);
        }
    }

    /// <summary>
    /// 設定を再読み込みしてホットキーを更新する
    /// </summary>
    public void ReloadSettings()
    {
        OnPropertyChanged(nameof(FileNameTemplateText));
        OnPropertyChanged(nameof(SaveFolderPath));
        OnPropertyChanged(nameof(SaveFolderDisplayName));
    }

    private void UpdateSaveFolderDisplayName()
    {
        SaveFolderDisplayName = Path.GetFileName(_saveFolderPath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

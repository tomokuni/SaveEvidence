using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.SnipCaptureUI;
using app.Enum;
using app.Models;
using app.Services;
using Microsoft.Extensions.Logging.Abstractions;

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
/// ピクセル操作（切出し・ウィンドウ検出）は PlatformFactory の <see cref="ImageProcessor"/> / <see cref="AppPixelData"/> に委譲し、
/// 画面キャプチャは <see cref="ICaptureService"/> に委譲する（プラットフォーム抽象。DIP）。
/// </remarks>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    /// <summary>画面キャプチャサービス（プラットフォーム抽象）。</summary>
    private readonly ICaptureService _captureService;

    private string _lastSavedFileName = "";

    /// <summary>プレビュー画像変更前の旧画像（新しい画像が Image.Source へ反映された後に破棄する）。</summary>
    private Bitmap? _previousPreviewImage;

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
    /// <param name="captureService">画面キャプチャサービス。null の場合は PlatformFactory の既定実装を使用する。</param>
    public MainViewModel(ISettingsService settingsService, ICaptureService? captureService = null)
    {
        _settingsService = settingsService;
        _captureService = captureService ?? PlatformFactory.CreateCaptureService(NullLogger.Instance);

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
    [NotifyPropertyChangedFor(nameof(SaveStatusText))]
    [NotifyPropertyChangedFor(nameof(SavedFileName))]
    [NotifyPropertyChangedFor(nameof(ImageSizeDisplayText))]
    private Bitmap? _previewImage;

    /// <summary>画像が保存済みかどうか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveStatusText))]
    [NotifyPropertyChangedFor(nameof(SavedFileName))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isSaved;

    /// <summary>ファイル名テンプレート文字列</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFileNamePreview))]
    [NotifyPropertyChangedFor(nameof(HasValidTemplate))]
    [NotifyPropertyChangedFor(nameof(HasInvalidTemplate))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _fileNameTemplateText = string.Empty;

    /// <summary>保存先フォルダパス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveFolderPathResolved))]
    [NotifyPropertyChangedFor(nameof(SaveFolderDisplayName))]
    [NotifyPropertyChangedFor(nameof(HasInvalidSaveFolder))]
    private string _saveFolderPath = string.Empty;

    /// <summary>保存先フォルダパスのプレースホルダを解決した実パス。</summary>
    /// <remarks>設定値（<see cref="SaveFolderPath"/>）に含まれるプレースホルダ（例: <c>{MyPictures}</c>）を展開する。</remarks>
    public string SaveFolderPathResolved => ResolveSaveFolderPath(SaveFolderPath);

    /// <summary>保存先フォルダが無効かどうか（プレースホルダ解決後のパスが存在しない場合）。</summary>
    /// <value>解決後のパスが空、または <see cref="Directory.Exists"/> が false の場合 true。</value>
    /// <remarks>設定画面のテキストボックス枠の赤表示（<c>validation-error</c> クラス）に使用する。</remarks>
    public bool HasInvalidSaveFolder
        => string.IsNullOrEmpty(SaveFolderPathResolved) || !Directory.Exists(SaveFolderPathResolved);

    /// <summary>保存先フォルダパスのプレースホルダ（<c>{MyPictures}</c> 等）を展開する。</summary>
    /// <param name="path">プレースホルダを含む可能性のあるフォルダパス。</param>
    /// <returns>展開後のパス。プレースホルダが無ければ入力をそのまま返す。</returns>
    /// <remarks>
    /// 対応プレースホルダ: <c>{MyPictures}</c> → <see cref="Environment.SpecialFolder.MyPictures"/>。<br/>
    /// 大文字小文字は区別しない（<c>{mypictures}</c> も展開する）。<br/>
    /// テスト（app.Tests）から検証するため public 静的メソッドとする。
    /// </remarks>
    public static string ResolveSaveFolderPath(string path)
        => string.IsNullOrEmpty(path)
            ? path
            : path.Replace("{MyPictures}",
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                StringComparison.OrdinalIgnoreCase);

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

    /// <summary>ナビゲーションパネルの展開状態（true = 展開 / false = アイコンのみ収納）。</summary>
    /// <remarks>展開時はタイトル + テキスト + アイコンを表示する。収納時はアイコンのみ表示する。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavPanelWidth))]
    private bool _isNavigationExpanded = true;

    /// <summary>ナビゲーションパネルの幅（展開時 180 / 収納時 40）。</summary>
    /// <remarks>収納時はアイコンのみ表示する幅。</remarks>
    public double NavPanelWidth => IsNavigationExpanded ? 180 : 40;

    /// <summary>プレビュー画像が存在するかどうかを取得する。</summary>
    public bool HasPreviewImage => PreviewImage is not null;

    /// <summary>クリップボードにコピー可能かどうかを取得する。</summary>
    public bool CanCopy => PreviewImage is not null;

    /// <summary>保存可能かどうかを取得する（画像があり、かつ有効なファイル名テンプレートの場合のみ保存可能）。</summary>
    /// <remarks>保存済み（<see cref="IsSaved"/>）の場合は無効化する（画像変更で保存済み状態が解除される）。</remarks>
    public bool CanSave => PreviewImage is not null && HasValidTemplate && !IsSaved;

    /// <summary>貼り付け可能かどうかを取得する。</summary>
    public bool CanPaste => PasteFromClipboardAsync is not null;

    /// <summary>ズーム率の選択肢（0=自動）を取得する。</summary>
    /// <remarks>フライアウト選択肢: 自動 + 固定倍率 10〜500% の全13段階。</remarks>
    public IReadOnlyList<int> ZoomLevels => s_zoomLevels;

    /// <summary>テーマモードの選択肢（Light / Dark / System）を取得する。</summary>
    /// <remarks>設定画面の ComboBox の ItemsSource に使用する。</remarks>
    public IReadOnlyList<ThemeMode> ThemeModes => s_themeModes;

    /// <summary>ComboBox 表示用のズーム率ラベル一覧を取得する。</summary>
    public IReadOnlyList<string> ZoomLabels => s_zoomLabels;

    private static readonly int[] s_zoomLevels = [0, 10, 15, 25, 35, 50, 75, 100, 150, 200, 250, 350, 500];

    private static readonly ThemeMode[] s_themeModes = System.Enum.GetValues<ThemeMode>();

    private static readonly string[] s_zoomLabels =
        ["自動(Fit)", "10%", "15%", "25%", "35%", "50%", "75%", "100%", "150%", "200%", "250%", "350%", "500%"];

    /// <summary>現在のファイル名プレビューテキストを取得する。</summary>
    public string CurrentFileNamePreview
    {
        get
        {
            return $"保存ファイル名: {FileNameTemplate.Generate(FileNameTemplateText)}";
        }
    }

    /// <summary>現在の状態テキスト（左側: 保存済み/未）。</summary>
    /// <remarks>表示している画像が保存済みなら「保存済み」、画像変更後（キャプチャ・Crop・貼り付け等）または初期表示なら「未」。</remarks>
    public string SaveStatusText => IsSaved ? "保存済み" : "未";

    /// <summary>保存済みファイル名（未保存の場合は空欄）。</summary>
    public string SavedFileName => IsSaved ? _lastSavedFileName : "";

    /// <summary>拡大率の表示テキスト（Fit 時も実効拡大率を % で表示する。「自動」表記は行わない）。View 側から設定される。</summary>
    [ObservableProperty]
    private string _zoomDisplayText = "100%";

    /// <summary>マウスポインタ位置のドメイン座標（画像ピクセル）の表示テキスト。画像外は "-"。View 側から設定される。</summary>
    [ObservableProperty]
    private string _pointerDisplayText = "-";

    /// <summary>画像サイズ（ドメイン座標 = 元画像ピクセル）の表示テキスト。</summary>
    /// <remarks>画像未設定時は空欄。画像サイズはズーム状態に依存しない固定値。</remarks>
    public string ImageSizeDisplayText => PreviewImage is { } img ? $"{img.PixelSize.Width} x {img.PixelSize.Height}" : "";

    /// <summary>ファイル名テンプレートが有効かどうかを取得する。</summary>
    /// <remarks><see cref="ValidationMessage"/> が null かどうかで判定する。</remarks>
    public bool HasValidTemplate => ValidationMessage is null;

    /// <summary>ファイル名テンプレートが無効かどうかを取得する（枠線エラー表示用）。</summary>
    /// <remarks><see cref="HasValidTemplate"/> の否定。View の <c>validation-error</c> クラスバインディングに使用する。</remarks>
    public bool HasInvalidTemplate => !HasValidTemplate;

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
    /// プレビュー画像変更前の処理（破棄は行わない。変更後に破棄する）。
    /// </summary>
    /// <param name="value">新しい画像（このメソッド内では未使用）</param>
    /// <remarks>
    /// <c>OnPreviewImageChanging</c> はフィールド更新前に呼ばれるため、<c>_previewImage</c> はまだ変更前の古い画像を保持している。<br/>
    /// ここで破棄すると、<c>Image.Source</c> が古い画像を参照している間に破棄され、<br/>
    /// ウィンドウ再表示時にプレビューエリアがチラつく（破棄済み Bitmap の描画）。<br/>
    /// 破棄は <c>OnPreviewImageChanged</c>（新しい画像が設定された後）で行う。
    /// </remarks>
    partial void OnPreviewImageChanging(Bitmap? value)
    {
        // 前回の画像を保持（OnPreviewImageChanged で破棄。Image.Source 更新前に破棄しない）
        _previousPreviewImage = _previewImage;
        IsSaved = false;
        _lastSavedFileName = "";
    }

    /// <summary>プレビュー画像変更後に前回の画像を破棄する。</summary>
    /// <param name="value">新しい画像。</param>
    /// <remarks>
    /// 新しい画像が <c>Image.Source</c> へ反映された後に前回の画像を破棄することで、<br/>
    /// 破棄済み Bitmap が描画されるチラつきを防止する。
    /// </remarks>
    partial void OnPreviewImageChanged(Bitmap? value)
    {
        _previousPreviewImage?.Dispose();
        _previousPreviewImage = null;
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
    /// <param name="bounds">キャプチャするスクリーン座標の矩形領域（PixelRect）</param>
    public void CaptureScreenArea(PixelRect bounds)
    {
        PreviewImage = _captureService.CaptureArea(bounds);
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
    /// <param name="bounds">切り出す矩形領域（画像座標 PixelRect）</param>
    [RelayCommand]
    private void CropPreview(PixelRect bounds)
    {
        if (PreviewImage is null)
        {
            return;
        }

        BeforeEditAction?.Invoke();

        // PreviewImage → AppPixelData → Crop → Bitmap（ピクセル操作は PlatformFactory の共通基盤に委譲）
        var srcData = AppPixelData.ToPixelData(PreviewImage);
        var croppedData = ImageProcessor.Crop(srcData, bounds);
        if (croppedData is not null)
        {
            PreviewImage = croppedData.ToBitmap();
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

        // PreviewImage → AppPixelData → DetectAndCropWindow → Bitmap（ピクセル操作は PlatformFactory の共通基盤に委譲）
        var srcData = AppPixelData.ToPixelData(PreviewImage);
        var croppedData = ImageProcessor.DetectAndCropWindow(srcData);
        if (croppedData is not null)
        {
            PreviewImage = croppedData.ToBitmap();
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
            var filePath = Path.Combine(SaveFolderPathResolved, fileName);

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

            // Avalonia Bitmap を保存（ファイル拡張子から自動判別）
            PreviewImage.Save(filePath, PngBitmapEncoderOptions.Default);

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
        var resolved = SaveFolderPathResolved;
        if (!string.IsNullOrEmpty(resolved))
        {
            if (!Directory.Exists(resolved))
            {
                Directory.CreateDirectory(resolved);
            }

            Process.Start("explorer.exe", resolved);
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
        SaveFolderDisplayName = Path.GetFileName(SaveFolderPathResolved);
    }
}


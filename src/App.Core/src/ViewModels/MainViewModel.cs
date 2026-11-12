using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Parts.LineDetection;

namespace EsUtil.SaveEvidence.App.Core.ViewModels;

/// <summary>メイン画面の ViewModel。 CommunityToolkit.Mvvm ソースジェネレーターにより、プロパティ変更通知とコマンドを自動生成する。</summary>
/// <remarks>
/// <see cref="ObservableObject"/> を継承し、<c>[ObservableProperty]</c> や
/// <c>[RelayCommand]</c> 属性によるソースジェネレーターを活用する。<br/>
/// 設定管理は <see cref="Services.SettingsService"/> を介して行い、疎結合な設計を実現する。<br/>
/// 画像のライフサイクル管理（Dispose）は <c>OnPreviewImageChanging</c> で行う。<br/>
/// ピクセル操作（切出し）は UI 非依存の <see cref="EsReadOnlyBitmap"/> のメンバーに委譲し、
/// 画面キャプチャは <see cref="CaptureType"/> に委譲する（プラットフォーム抽象。 DIP）。<br/>
/// クリップボードと上書き確認は UI が必要なため、コールバックとして UI 実装側から注入する（依存性逆転）。
/// </remarks>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    private readonly IImageWriter _imageWriter;

    /// <summary>初期画像（起動直後のプレースホルダ）を表示しているかどうか。</summary>
    /// <remarks>
    /// プレースホルダは利用者の画像ではないため保存できない（<see cref="CanSave"/>）。<br/>
    /// 通常の画像を設定すると <c>OnPreviewImageChanging</c> が解除する。
    /// </remarks>
    private bool _isStartupImage;

    /// <summary>初期画像の設定中かどうか（差し替えの経路が初期画像の印を消さないようにする）。</summary>
    /// <remarks>
    /// <see cref="SetStartupPreviewImage"/> は <see cref="PreviewImage"/> を設定するため差し替えの経路を通る。<br/>
    /// その経路は印を消す（利用者の画像として扱う）ため、設定中だけは消さずに引き継ぐ。
    /// </remarks>
    private bool _settingStartupImage;

    /// <summary>最後に保存したファイル名（未保存の場合は空）。 保存のたびに更新して表示へ通知する。</summary>
    /// <remarks>
    /// <b>観測可能にしている理由</b>: 保存済みのまま再保存すると <see cref="IsSaved"/> が true のまま変わらず、<br/>
    /// 通知が発火しないため、ステータスバーの表示が前回のファイル名のまま残る（実測 2026-11-11）。
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SavedFileName))]
    private string _lastSavedFileName = "";

    /// <summary>ファイル名テンプレートの検証結果（有効時は null）。</summary>
    private string? _validationMessage;

    /// <summary>保存先フォルダパスのプレースホルダ解決後の実パス。</summary>
    private string _saveFolderPathResolved = "";

    /// <summary>保存先フォルダが無効かどうか。</summary>
    private bool _hasInvalidSaveFolder;

    /// <summary>Fit 時の画像の中央寄せ / 左上寄せ。</summary>
    /// <remarks>配置は UI 側のズームビューポート（Avalonia 版は <c>ZoomViewControl.CenterAlign</c>）へ反映される（画像がビューポートより小さい場合）。</remarks>
    public bool CenterAlign => Settings.CenterAlign;

    /// <summary>プレビュー画像変更前の旧画像（新しい画像が Image.Source へ反映された後に破棄する）。</summary>
    private EsReadOnlyBitmap? _previousPreviewImage;

    /// <summary>選択モード開始デリゲート（View 側から設定されるコールバック）。</summary>
    public Action<CaptureType>? StartSelectionMode { get; set; }

    /// <summary>画像クリップボード操作のコールバック（View 側から設定）。</summary>
    /// <remarks>コピーは <see cref="EsReadOnlyBitmap"/>（読み取りの面）を渡し、UI 型への変換は UI 実装側（View）が行う。</remarks>
    public Func<EsReadOnlyBitmap, Task<bool>>? CopyToClipboardCallback { get; set; }

    /// <summary>画像貼り付け操作のコールバック（View 側から設定）。</summary>
    /// <remarks>貼り付けは書き換え可能な画像（<see cref="EsBitmap"/>）を受け取り（所有権も移る）、
    /// UI 型からの変換は UI 実装側（View）が行う。</remarks>
    public Func<Task<EsBitmap?>>? PasteFromClipboardCallback { get; set; }

    /// <summary>書き換え可能な画像をプレビューの型（読み取りの面）へ移す（凍結して 0 コピーで共有する）。</summary>
    /// <param name="image">移す画像（書き換え可能）。 渡した実体は凍結される（読み取りは引き続きできる）。</param>
    /// <returns>読み取りの面。 破棄はプレビューの所有者（本 ViewModel）が行う。</returns>
    /// <remarks>
    /// 渡した実体の画素は読み取りの面と共有されるため、<b>書き換えには新しい画像を作る</b>
    /// （凍結のため <c>SetPixel</c> 等は例外になる）。
    /// </remarks>
    private static EsReadOnlyBitmap ToReadOnly(EsBitmap image)
    {
        image.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(image);
    }

    /// <summary>所有権ごと受け取った画像をプレビューの型（読み取りの面）へ移す。</summary>
    /// <param name="image">移す画像（書き換え可能）。 画素は共有され、渡した実体は解放される。</param>
    /// <returns>読み取りの面。 破棄はプレビューの所有者（本 ViewModel）が行う。</returns>
    /// <remarks>編集結果・貼り付け結果のように<b>他に保持者がいない画像</b>に使う（参照を 1 つ早く返す）。</remarks>
    private static EsReadOnlyBitmap TakeReadOnly(EsBitmap image)
    {
        var read = ToReadOnly(image);
        // 画素は読み取りの面が共有して引き継ぐため、受け取った実体（参照）はここで解放する
        image.Dispose();
        return read;
    }

    /// <summary>上書き確認ダイアログのコールバック（View 側から設定）。</summary>
    public Func<string, Task<bool>>? ConfirmOverwriteAsync { get; set; }

    /// <summary>画像編集（切り出し等）前に View 側で Undo をプッシュするためのコールバック。</summary>
    public Action? BeforeEditAction { get; set; }

    /// <summary>MainViewModel の新しいインスタンスを初期化する。</summary>
    /// <param name="settingsService">設定サービス</param>
    /// <param name="imageWriter">画像書き込みサービス。 本番は <see cref="EsBitmapImageWriter"/>（コアの保存へ委譲する）を注入する。</param>
    public MainViewModel(ISettingsService settingsService, IImageWriter imageWriter)
    {
        _settingsService = settingsService;
        _imageWriter = imageWriter;

        var settings = _settingsService.Current;
        _fileNameTemplateText = settings.FileNameTemplate;
        _saveFolderPath = !string.IsNullOrEmpty(settings.SaveFolderPath)
            ? settings.SaveFolderPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SaveEvidence");
        UpdateValidation();
        UpdateSaveFolderResolution();
    }

    /// <summary>プレビュー画像（読み取りの面。 表示は表示境界で Avalonia/Parts の画像へ変換する）</summary>
    /// <remarks>
    /// <b>読み取りの面で保持する理由</b>: 表示・保存・切り出し・検出はすべて画像を読むだけであり、<br/>
    /// 編集は元の画像を書き換えず<b>新しい画像を作る</b>（差し替える）ため、書き換えの入口を持つ必要が無い。
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
    [NotifyPropertyChangedFor(nameof(CanCopy))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(SavedFileName))]
    [NotifyPropertyChangedFor(nameof(ImageSizeDisplayText))]
    private EsReadOnlyBitmap? _previewImage;

    /// <summary>画像が保存済みかどうか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SavedFileName))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isSaved;

    /// <summary>ファイル名テンプレート文字列</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidTemplate))]
    [NotifyPropertyChangedFor(nameof(HasInvalidTemplate))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _fileNameTemplateText = string.Empty;

    /// <summary>保存先フォルダパス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveFolderPathResolved))]
    [NotifyPropertyChangedFor(nameof(HasInvalidSaveFolder))]
    private string _saveFolderPath = string.Empty;

    /// <summary>保存先フォルダパスのプレースホルダを解決した実パス。</summary>
    /// <remarks>設定値（<see cref="SaveFolderPath"/>）に含まれるプレースホルダ（例: <c>{MyPictures}</c>）を展開する。</remarks>
    public string SaveFolderPathResolved => _saveFolderPathResolved;

    /// <summary>保存先フォルダが無効かどうか（プレースホルダ解決後のパスが存在しない場合）。</summary>
    /// <value>解決後のパスが空、または <see cref="Directory.Exists"/> が false の場合 true。</value>
    /// <remarks>設定画面のテキストボックス枠の赤表示（<c>validation-error</c> クラス）に使用する。</remarks>
    public bool HasInvalidSaveFolder => _hasInvalidSaveFolder;

    /// <summary>保存先フォルダパスのプレースホルダ（<c>{MyPictures}</c> 等）を展開する。</summary>
    /// <param name="path">プレースホルダを含む可能性のあるフォルダパス。</param>
    /// <returns>展開後のパス。 プレースホルダが無ければ入力をそのまま返す。</returns>
    /// <remarks>
    /// 対応プレースホルダ: <c>{MyPictures}</c> → <see cref="Environment.SpecialFolder.MyPictures"/>。<br/>
    /// 大文字小文字は区別しない（<c>{mypictures}</c> も展開する）。<br/>
    /// テスト（EsUtil.SaveEvidence.Tests）から検証するため public 静的メソッドとする。
    /// </remarks>
    public static string ResolveSaveFolderPath(string path)
        => string.IsNullOrEmpty(path)
            ? path
            : path.Replace("{MyPictures}",
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                StringComparison.OrdinalIgnoreCase);

    /// <summary>現在のズーム率（%）。 0 = 自動（Fit）。 表示用ストア（ズーム率の単一所有は ZoomableView。 ZoomChanged で更新）。</summary>
    /// <remarks>スライダ・ズーム表示のバインディング用に保持する。 ズーム率の実ソースは ZoomViewControl 側。</remarks>
    [ObservableProperty]
    private double _zoomPercent;

    /// <summary>切出し選択範囲があるかどうか。 View 側から設定される。</summary>
    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>自動切り出しモード中かどうか（検出した矩形を表示中）。</summary>
    [ObservableProperty]
    private bool _isAutoCropActive;

    /// <summary>自動切り出しで検出された矩形候補群（画像座標 <see cref="EsRectInt"/>・閉区間・面積昇順）。</summary>
    /// <value>4 辺が矩形を構成する候補のみ。 座標の重複は統合し、最大 256 件に制限される。</value>
    [ObservableProperty]
    private IReadOnlyList<EsRectInt> _detectedRects = [];

    /// <summary>現在ホバー中の矩形（複数矩形内にある場合は最も面積の小さいもの）。 なければ null。</summary>
    [ObservableProperty]
    private EsRectInt? _hoveredRect;

    /// <summary>ステータスメッセージ（自動切り出しの検出結果・案内）。</summary>
    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>ナビゲーションパネルの展開状態（true = 展開 / false = アイコンのみ収納）。</summary>
    /// <remarks>展開時はタイトル + テキスト + アイコンを表示する。 収納時はアイコンのみ表示する。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavPanelWidth))]
    private bool _isNavigationExpanded = true;

    /// <summary>ナビゲーションパネルの幅（展開時 128 / 収納時 40）。</summary>
    /// <remarks>
    /// 幅の根拠（アイコン + 最も長い項目名 + 余白）は <see cref="NavPanelMetrics"/> が単一所有する<br/>
    /// （4 つの UI 版が同じ幅を使う。 UI ごとに固定値を書かない）。
    /// </remarks>
    public double NavPanelWidth => NavPanelMetrics.Width(IsNavigationExpanded);

    /// <summary>プレビュー画像が存在するかどうかを取得する。</summary>
    public bool HasPreviewImage => PreviewImage is not null;

    /// <summary>クリップボードにコピー可能かどうかを取得する。</summary>
    public bool CanCopy => PreviewImage is not null;

    /// <summary>保存可能かどうかを取得する（画像があり、かつ有効なファイル名テンプレートの場合のみ保存可能）。</summary>
    /// <remarks>
    /// 次のいずれかでは保存できない（保存ボタン・メニュー・ショートカットを同じ判定で無効にする）。<br/>
    /// - 画像が無い（<see cref="PreviewImage"/> が null）。<br/>
    /// - 起動直後の初期画像を表示している（<see cref="IsStartupImage"/>。 利用者の画像ではない）。<br/>
    /// - ファイル名テンプレートが無効。<br/>
    /// - 保存済みで画像が変わっていない（<see cref="IsSaved"/>。 画像変更で解除される）。
    /// </remarks>
    public bool CanSave => PreviewImage is not null && HasValidTemplate && !IsSaved && !IsStartupImage;

    /// <summary>起動直後の初期画像（プレースホルダ）を表示しているかどうかを取得する。</summary>
    /// <value>初期画像の場合は true。 撮影・切り出し・貼り付け等で差し替えると false。</value>
    /// <remarks>初期画像は利用者の画像ではないため保存できない（<see cref="CanSave"/> が false になる）。</remarks>
    public bool IsStartupImage => _isStartupImage;

    /// <summary>自動保存が有効かどうかを取得または設定する。</summary>
    /// <value>有効な場合、画像が変わると自動で保存する（設定へ保存する）。</value>
    /// <remarks>
    /// 値の保持と永続化は <see cref="Models.Settings.AutoSave"/> が担う（4 つの UI 版で共有する）。<br/>
    /// 自動保存は<b>保存できない状態では行わない</b>（初期画像・テンプレート無効・変更なし）。<br/>
    /// 同名のファイルがある場合は確認せずに中止する（自動保存で確認ダイアログを出さない）。
    /// </remarks>
    public bool AutoSave
    {
        get => Settings.AutoSave;
        set
        {
            if (Settings.AutoSave == value)
            {
                return;
            }

            Settings.AutoSave = value;
            _settingsService.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>貼り付け可能かどうかを取得する。</summary>
    public bool CanPaste => PasteFromClipboardCallback is not null;

    /// <summary>テーマモードの選択肢（Light / Dark / System）を取得する。</summary>
    /// <remarks>設定画面の ComboBox の ItemsSource に使用する。</remarks>
    public static IReadOnlyList<ThemeMode> ThemeModes => s_themeModes;

    /// <summary>色の表記の選択肢（色名 + 不透明度 / カラーコード）を取得する。</summary>
    /// <remarks>設定画面の ComboBox の ItemsSource に使用する（表示名は <see cref="ColorTextFormatDisplay"/> が単一所有する）。</remarks>
    public static IReadOnlyList<ColorTextFormat> ColorTextFormats => ColorTextFormatDisplay.All;

    /// <summary>起動直後の画像（プレースホルダ）の選択肢（グラデーション画像 / 空白）を取得する。</summary>
    /// <remarks>設定画面のコンボ ボックスの選択肢に使用する（表示名は列挙の説明属性が単一所有する）。</remarks>
    public static IReadOnlyList<StartupPreviewMode> StartupPreviewModes => s_startupPreviewModes;

    private static readonly ThemeMode[] s_themeModes = Enum.GetValues<ThemeMode>();

    private static readonly StartupPreviewMode[] s_startupPreviewModes = Enum.GetValues<StartupPreviewMode>();

    /// <summary>保存済みファイル名（未保存の場合は空欄）。</summary>
    public string SavedFileName => IsSaved ? LastSavedFileName : "";

    /// <summary>拡大率の表示テキスト（Fit 時も実効拡大率を % で表示する。 「自動」表記は行わない）。 View 側から設定される。</summary>
    [ObservableProperty]
    private string _zoomDisplayText = "100%";

    /// <summary>マウスポインタ位置の対象座標（画像ピクセル）の表示テキスト。 画像外は "-"。 View 側から設定される。</summary>
    [ObservableProperty]
    private string _pointerDisplayText = "-";

    /// <summary>画像サイズ（対象座標 = 元画像ピクセル）の表示テキスト。</summary>
    /// <remarks>画像未設定時は空欄。 画像サイズはズーム状態に依存しない固定値。</remarks>
    public string ImageSizeDisplayText => PreviewImage is { } img ? $"{img.Size.Width} x {img.Size.Height}" : "";

    /// <summary>ファイル名テンプレートが有効かどうかを取得する。</summary>
    /// <remarks><see cref="ValidationMessage"/> が null かどうかで判定する。</remarks>
    public bool HasValidTemplate => _validationMessage is null;

    /// <summary>ファイル名テンプレートが無効かどうかを取得する（枠線エラー表示用）。</summary>
    /// <remarks><see cref="HasValidTemplate"/> の否定。 View の <c>validation-error</c> クラスバインディングに使用する。</remarks>
    public bool HasInvalidTemplate => !HasValidTemplate;

    /// <summary>ファイル名テンプレートの検証エラーメッセージを取得する。 有効な場合は null。</summary>
    public string? ValidationMessage => _validationMessage;

    /// <summary>設定オブジェクトを取得する。</summary>
    public Settings Settings => _settingsService.Current;

    /// <summary>プレビュー画像変更前の処理（破棄は行わない。 変更後に破棄する）。</summary>
    /// <param name="value">新しい画像（このメソッド内では未使用）</param>
    /// <remarks>
    /// <c>OnPreviewImageChanging</c> はフィールド更新前に呼ばれるため、<c>_previewImage</c> はまだ変更前の古い画像を保持している。<br/>
    /// ここで破棄すると、<c>Image.Source</c> が古い画像を参照している間に破棄され、<br/>
    /// ウィンドウ再表示時にプレビューエリアがチラつく（破棄済み Bitmap の描画）。<br/>
    /// 破棄は <c>OnPreviewImageChanged</c>（新しい画像が設定された後）で行う。
    /// </remarks>
    partial void OnPreviewImageChanging(EsReadOnlyBitmap? value)
    {
        // 前回の画像を保持（OnPreviewImageChanged で破棄。 Image.Source 更新前に破棄しない）
        _previousPreviewImage = _previewImage;
        IsSaved = false;
        LastSavedFileName = "";

        // 差し替えは利用者の画像（撮影・切り出し・貼り付け等）のため初期画像ではない
        // （初期画像を設定する経路は SetStartupPreviewImage が印を引き継がせる）
        var wasStartupImage = _isStartupImage;
        _isStartupImage = _settingStartupImage;
        if (wasStartupImage != _isStartupImage)
        {
            OnPropertyChanged(nameof(IsStartupImage));
        }
    }

    /// <summary>プレビュー画像変更後に前回の画像を破棄する。</summary>
    /// <param name="value">新しい画像。</param>
    /// <remarks>
    /// 新しい画像が <c>Image.Source</c> へ反映された後に前回の画像を破棄することで、<br/>
    /// 破棄済み Bitmap が描画されるチラつきを防止する。
    /// </remarks>
    partial void OnPreviewImageChanged(EsReadOnlyBitmap? value)
    {
        _previousPreviewImage?.Dispose();
        _previousPreviewImage = null;

        // 画像変更時は自動切り出しの検出矩形・モードを無効化する（既存画像に対する検出結果のため）
        IsAutoCropActive = false;
        DetectedRects = [];
        HoveredRect = null;
        StatusMessage = "";

        // 自動保存が有効なら、画像が変わった時点で保存する
        // （保存できない状態（初期画像・テンプレート無効）は Core 側で判定する）
        if (AutoSave && CanSave)
        {
            _ = SaveAfterImageChangedAsync();
        }
    }

    /// <summary>画像の変更後に自動保存する（変更を通知した側の後処理が終わってから保存する）。</summary>
    /// <returns>待機タスク。</returns>
    /// <remarks>
    /// <b>1 度待つ理由</b>: 画像を差し替えた側（キャプチャ等）は、この後に「キャプチャしました（…）」を<br/>
    /// ステータスへ設定する。 待たずに保存すると、その通知に「保存しました」が上書きされる（実測 2026-11-11）。
    /// </remarks>
    private async Task SaveAfterImageChangedAsync()
    {
        await Task.Yield();

        if (AutoSave && CanSave)
        {
            await SaveCoreAsync(confirmOverwrite: false);
        }
    }

    /// <summary>ファイル名テンプレート変更時に検証結果を更新し、設定を保存する。</summary>
    partial void OnFileNameTemplateTextChanged(string value)
    {
        _settingsService.Current.FileNameTemplate = value;
        _settingsService.Save();
        UpdateValidation();
    }

    /// <summary>保存先フォルダ変更時に解決結果を更新し、設定を保存する。</summary>
    partial void OnSaveFolderPathChanged(string value)
    {
        _settingsService.Current.SaveFolderPath = value;
        _settingsService.Save();
        UpdateSaveFolderResolution();
    }

    /// <summary>ファイル名テンプレートの検証結果を更新する。</summary>
    private void UpdateValidation()
    {
        FileNameTemplate.IsValidTemplate(FileNameTemplateText, out var message);
        _validationMessage = message;
    }

    /// <summary>保存先フォルダの解決結果を更新する。</summary>
    private void UpdateSaveFolderResolution()
    {
        _saveFolderPathResolved = ResolveSaveFolderPath(SaveFolderPath);
        _hasInvalidSaveFolder = string.IsNullOrEmpty(_saveFolderPathResolved) || !Directory.Exists(_saveFolderPathResolved);
    }

    /// <summary>指定された画像をプレビューとして設定する。</summary>
    /// <param name="image">設定する画像（読み取りの面）。 所有権が本型へ移る。</param>
    public void SetPreviewImage(EsReadOnlyBitmap image) => PreviewImage = image;

    /// <summary>指定された画像をプレビューとして設定する（書き換え可能な画像を受け取る版）。</summary>
    /// <param name="image">設定する画像（書き換え可能）。 凍結して 0 コピーでプレビューへ渡す。</param>
    /// <remarks>
    /// 撮影結果・貼り付け・読み込みは書き換え可能な画像として届くため、その入口を本オーバーロードが担う。<br/>
    /// <b>渡した実体の所有権は移らない</b>（画素はプレビューと共有されて共に生かされる。 渡した実体を破棄するのは呼び出し側）。
    /// ただし凍結されるため、以降の書き換えには新しい画像を作る。<br/>
    /// <b>呼び出し側が先に実体を破棄してもプレビューは読める</b>（<see cref="EsReadOnlyBitmap.FromBitmap"/> が画素領域の
    /// 参照を 1 つ取るため。 実体の解放は最後の 1 つが行う）。 それでも決定的に解放したい場合は、渡した後に
    /// 実体を破棄します（プレビューはそのまま使えます）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> が null の場合。</exception>
    public void SetPreviewImage(EsBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        PreviewImage = ToReadOnly(image);
    }

    /// <summary>起動直後の初期画像（プレースホルダ）をプレビューとして設定する。</summary>
    /// <param name="image">設定する画像（書き換え可能）。 凍結して 0 コピーでプレビューへ渡す。</param>
    /// <remarks>
    /// 初期画像は利用者の画像ではないため<b>保存できない</b>（<see cref="CanSave"/> が false になる）。<br/>
    /// 撮影・切り出し・貼り付け等で差し替えると通常の画像として扱われる。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> が null の場合。</exception>
    public void SetStartupPreviewImage(EsBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        // 差し替えの経路（OnPreviewImageChanging）が印を消すため、設定中だけは引き継がせる
        _settingStartupImage = true;
        try
        {
            PreviewImage = ToReadOnly(image);
        }
        finally
        {
            _settingStartupImage = false;
        }

        _isStartupImage = true;
        OnPropertyChanged(nameof(IsStartupImage));
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>画像をクリップボードにコピーする。</summary>
    [RelayCommand]
    public async Task CopyToClipboardAsync()
    {
        if (PreviewImage is not null && CopyToClipboardCallback is not null)
        {
            // クリップボードは UI 型を要求するため、変換は UI 実装側（View）が行う
            // （プレビューは読み取りの面のため、そのまま渡せる。 借用であり破棄はしない）
            await CopyToClipboardCallback(PreviewImage);
        }
    }

    /// <summary>クリップボードから画像を貼り付ける。</summary>
    /// <returns>貼り付けに成功したかどうか</returns>
    /// <remarks>結果は <see cref="StatusMessage"/> へ通知する（UI 層で文言を組み立てない）。</remarks>
    public async Task<bool> PasteFromClipboardAsync()
    {
        if (PasteFromClipboardCallback is null)
        {
            StatusMessage = StatusMessages.PasteUnavailable();
            return false;
        }

        var image = await PasteFromClipboardCallback();
        if (image is not null)
        {
            // 貼り付け結果の所有権は本型へ移る（画素は読み取りの面が共有して引き継ぐ）
            PreviewImage = TakeReadOnly(image);
            StatusMessage = StatusMessages.PasteCompleted();
            return true;
        }

        StatusMessage = StatusMessages.PasteUnavailable();
        return false;
    }

    /// <summary>指定された範囲でプレビュー画像を切り出す。</summary>
    /// <param name="bounds">切り出す矩形領域（画像座標 <see cref="EsRectInt"/>）</param>
    [RelayCommand]
    private void CropPreview(EsRectInt bounds)
    {
        // 通知は画像の差し替えより後に行う（画像の差し替えは StatusMessage を空にするため）
        if (ApplyPixelEdit(srcData => srcData.Crop(bounds.X, bounds.Y, bounds.Width, bounds.Height)))
        {
            StatusMessage = StatusMessages.CropCompleted(bounds.Width, bounds.Height);
        }
    }

    /// <summary>プレビュー画像を LineDetection で検出し、ウィンドウ矩形の候補を表示する。</summary>
    /// <remarks>
    /// LineDetection（<see cref="LogicLineDetector"/>）で水平・垂直の辺を検出し、<br/>
    /// 矩形（ウィンドウの 4 辺）の候補を復元して画像上に水色で表示する。<br/>
    /// ユーザーが矩形内でダブルクリックすると、その矩形で切り出す（<see cref="ConfirmAutoCrop"/>）。<br/>
    /// マウス位置が複数矩形内にある場合は、<see cref="HoveredRect"/>（面積最小）が赤色で強調される。<br/>
    /// 検出結果が空の場合は <see cref="StatusMessage"/> に通知する。
    /// </remarks>
    [RelayCommand]
    private void AutoCropWindow()
    {
        if (PreviewImage is not { } image)
        {
            return;
        }

        // 検出器は読み取りの面（EsReadOnlyBitmap）を入力として受け取るため、プレビューをそのまま渡せる
        // LineDetection で水平・垂直の辺（線分）を検出し、矩形（ウィンドウの 4 辺）を復元する
        var detector = new LogicLineDetector();
        var results = detector.Detect(image);

        var rects = TryReconstructRects(results);
        DetectedRects = rects;
        IsAutoCropActive = rects.Count > 0;
        HoveredRect = null;

        StatusMessage = rects.Count > 0
            ? StatusMessages.AutoCropDetected(rects.Count)
            : StatusMessages.AutoCropNotFound();
    }

    /// <summary>検出された線分から矩形（ウィンドウの 4 辺）の候補を復元する。</summary>
    /// <param name="results">検出された線分一覧。</param>
    /// <returns>復元した矩形候補の一覧（画像座標・閉区間 <see cref="EsRectInt"/>。 面積昇順・最大 256 件）。</returns>
    /// <remarks>
    /// 線分→矩形復元のロジックは <see cref="LineRectangleReconstructor"/>（純粋クラス）へ委譲する（単一所有・DRY）。<br/>
    /// 本メソッドはホストからの呼び出し窓口（アプリ固有の委譲）として維持する。
    /// </remarks>
    internal static IReadOnlyList<EsRectInt> TryReconstructRects(IReadOnlyList<LineDetectionResult> results)
        => LineRectangleReconstructor.Reconstruct(results);

    /// <summary>指定座標（画像ピクセル）を含む自動切り出しの検出矩形を求める。</summary>
    /// <param name="x">X 座標（画像ピクセル。 対象座標）。</param>
    /// <param name="y">Y 座標（画像ピクセル。 対象座標）。</param>
    /// <returns>座標を含む矩形のうち面積が最小のもの。 含む矩形がない場合は null。</returns>
    /// <remarks>
    /// 自動切り出しのホバー強調（<see cref="HoveredRect"/>）とダブルクリックの確定・キャンセル判定で共用する。<br/>
    /// 複数の矩形が重なる場合は<b>面積が最小</b>のものを選ぶ（内側のウィンドウを優先するため）。<br/>
    /// 各辺は<b>閉区間</b>として扱う（右端・下端のピクセルも矩形に含める）。<br/>
    /// 判定を UI 側で重複実装しないよう、本メソッドを単一の所有者とする（両 UI が呼び出す）。
    /// </remarks>
    public EsRectInt? HitDetectedRect(double x, double y)
    {
        EsRectInt? hit = null;
        var hitArea = long.MaxValue;

        foreach (var rect in DetectedRects)
        {
            if (x < rect.X || x > rect.Right || y < rect.Y || y > rect.Bottom)
            {
                continue;
            }

            var area = (long)rect.Width * rect.Height;
            if (area < hitArea)
            {
                hitArea = area;
                hit = rect;
            }
        }

        return hit;
    }

    /// <summary>確定したウィンドウ矩形でプレビュー画像を切り出す。</summary>
    /// <param name="bounds">確定した矩形領域（画像座標 <see cref="EsRectInt"/>）。</param>
    [RelayCommand]
    private void ConfirmAutoCrop(EsRectInt bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // 検出モードを終了し、確定した矩形で切り出す（BeforeEditAction → Crop → 画像差し替え）
        IsAutoCropActive = false;
        DetectedRects = [];
        HoveredRect = null;
        StatusMessage = "";

        // 通知は画像の差し替えより後に行う（画像の差し替えは StatusMessage を空にするため）
        if (ApplyPixelEdit(srcData => srcData.Crop(bounds.X, bounds.Y, bounds.Width, bounds.Height)))
        {
            StatusMessage = StatusMessages.CropCompleted(bounds.Width, bounds.Height);
        }
    }

    /// <summary>自動切り出しモードをキャンセルする。</summary>
    [RelayCommand]
    private void CancelAutoCrop()
    {
        // モードに入っていないときは通知しない（画像変更などの内部的な後片付けと区別する）
        var wasActive = IsAutoCropActive;
        IsAutoCropActive = false;
        DetectedRects = [];
        HoveredRect = null;
        StatusMessage = wasActive ? StatusMessages.AutoCropCanceled() : "";
    }

    /// <summary>ピクセル編集（切出し等）を適用する（BeforeEditAction → 変換 → 画像差し替え）。</summary>
    /// <param name="edit">読み取りの面（<see cref="EsReadOnlyBitmap"/>）を受け取り、編集後の画像（書き換え可能）を返す処理。</param>
    /// <returns>編集を適用した場合は true。 対象の画像が無い・編集結果が無い場合は false。</returns>
    /// <remarks>
    /// ピクセル操作は共通基盤（<see cref="EsReadOnlyBitmap"/> のメンバー）に委譲する。<br/>
    /// 画像を差し替えると <see cref="StatusMessage"/> が空になるため、通知は本メソッドの<b>後</b>に行う。
    /// </remarks>
    private bool ApplyPixelEdit(Func<EsReadOnlyBitmap, EsBitmap?> edit)
    {
        if (PreviewImage is null)
        {
            return false;
        }

        BeforeEditAction?.Invoke();

        // プレビュー（読み取りの面）→ 編集 → 編集結果を新しいプレビューとして設定する
        // （編集は元の画像を書き換えず、新しい画像を返す）
        var edited = edit(PreviewImage);
        if (edited is null)
        {
            return false;
        }

        // 編集結果の所有権は本型へ移る（画素は読み取りの面が共有して引き継ぐ）
        PreviewImage = TakeReadOnly(edited);
        return true;
    }

    /// <summary>現在のプレビュー画像を保存する。</summary>
    /// <remarks>
    /// 保存後にファイル名の数値をインクリメントし、設定を更新する。<br/>
    /// 同名ファイルが存在する場合は、<see cref="ConfirmOverwriteAsync"/> で確認する。<br/>
    /// 画像形式は拡張子から自動判定する（既定は PNG）。<br/>
    /// <b>保存できる状態のときだけ保存する</b>（<see cref="CanSave"/>。 画像が変わっていない・初期画像・
    /// テンプレート無効の場合は何もしない）。 この判定により、ショートカットの連打やキー リピートでも同じ画像を保存しない。
    /// </remarks>
    /// <returns>保存に成功したかどうか</returns>
    [RelayCommand]
    public async Task<bool> SaveImageAsync()
        => CanSave && await SaveCoreAsync(confirmOverwrite: true);

    /// <summary>保存の本体（判定を除く）。</summary>
    /// <param name="confirmOverwrite">同名のファイルがある場合に確認するかどうか（自動保存は false）。</param>
    /// <returns>保存に成功したかどうか。</returns>
    private async Task<bool> SaveCoreAsync(bool confirmOverwrite)
    {
        if (PreviewImage is null)
        {
            return false;
        }

        string filePath = "";
        try
        {
            var fileName = FileNameTemplate.Generate(FileNameTemplateText);
            filePath = Path.Combine(SaveFolderPathResolved, fileName);

            if (File.Exists(filePath))
            {
                // 自動保存は確認ダイアログを出さない（同名のファイルは壊さずに中止する）
                if (!confirmOverwrite)
                {
                    StatusMessage = StatusMessages.AutoSaveSkipped(fileName);
                    return false;
                }

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

            // 画像エンコード・ファイル書き込みを IImageWriter に委譲する（本番は Avalonia Skia でエンコード）
            // 保存は画像を読むだけであり、プレビューは読み取りの面のためそのまま渡せる
            _imageWriter.Save(PreviewImage, filePath);

            FileNameTemplateText = FileNameTemplate.IncrementRightmostNumber(FileNameTemplateText);

            LastSavedFileName = fileName;
            IsSaved = true;
            // 撮影の通知を保存の通知で置き換える（保存後に何が起きたかがステータスバーで分かるようにする）
            StatusMessage = StatusMessages.SaveCompleted(DateTime.Now);

            return true;
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 保存失敗は通知する（通知の有無・文言が UI 版でずれないようコアが単一所有する）
            StatusMessage = StatusMessages.SaveFailed();
            // 原因はイベントでホストへ渡す（記録はログの所有者であるアプリが行う）
            RaiseSaveFailed(new SaveFailedEventArgs(ex, filePath));
            return false;
        }
    }

    /// <summary>画像の保存に失敗したときに発生する。</summary>
    /// <remarks>
    /// 保存の失敗は例外にせず本イベントで通知する（アプリは処理を継続する）。<br/>
    /// 記録（ログ）はホスト（アプリ）が行う。 <see cref="SaveImageAsync"/> の完了より前に発火する。
    /// </remarks>
    public event EventHandler<SaveFailedEventArgs>? SaveFailed;

    /// <summary><see cref="SaveFailed"/> を発火する。</summary>
    /// <param name="args">イベントの引数。</param>
    /// <remarks>イベントの発火は本ヘルパー 1 箇所へ集約する（[`.github/コーディング規約.md`](../../../../.github/コーディング規約.md) §7）。</remarks>
    private void RaiseSaveFailed(SaveFailedEventArgs args) => SaveFailed?.Invoke(this, args);

}


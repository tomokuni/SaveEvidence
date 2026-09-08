using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.SnipCaptureUI;
using Estable.SaveEvidence.Enum;
using Estable.SaveEvidence.Icons;
using Estable.SaveEvidence.Logging;
using Estable.SaveEvidence.Services;
using Estable.SaveEvidence.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Estable.SaveEvidence.Views;

/// <summary>
/// SaveEvidence アプリケーションのメインウィンドウ。
/// </summary>
/// <remarks>
/// 画像のプレビュー表示、キャプチャ操作、保存、クリップボード連携、ルーペ表示、<br/>
/// 切り出し選択、ズーム、ホットキー管理など、アプリケーションの主要機能を統括する。<br/>
/// ViewModel（MainViewModel）と連携し、画面キャプチャやファイル保存などの
/// 実際の処理は Model 層に委譲する。<br/>
/// </remarks>
public sealed partial class MainWindow : Window
{
    private MainViewModel _viewModel = null!;
    private SettingsService _settingsService = null!;
    private IGlobalHotKeyService<CaptureType> _hotKeyService = null!;

    /// <summary>SnipCaptureUI の公開 Facade（キャプチャセッションの窓口）。</summary>
    private SnipCapture _snipCapture = null!;

    /// <summary>SnipCaptureUI へ注入するロガー（Serilog 経由でファイル出力）。</summary>
    private ILogger _snipCaptureLogger = null!;

    /// <summary>アプリケーションロガー。</summary>
    private static readonly ILogger _logger = AppLog.CreateLogger("MainWindow");

    // ナビゲーション状態（AppPage 型。初期値 null: コンストラクタの NavigateTo(AppPage.Capture) が
    // 早期 return せず、初期ページの選択状態が設定されるようにする）
    private AppPage? _navSelectedPage;

    /// <summary>
    /// MainWindow を初期化する。
    /// </summary>
    /// <param name="viewModel">メインViewModel</param>
    /// <param name="settingsService">設定サービス</param>
    /// <param name="hotKeyService">ホットキーサービス</param>
    public MainWindow(MainViewModel viewModel, ISettingsService settingsService,
        IGlobalHotKeyService<CaptureType> hotKeyService)
    {
        InitializeComponent();
        if (Design.IsDesignMode) return;

        DataContext = viewModel;
        _viewModel = viewModel;
        _settingsService = (SettingsService)settingsService;

        // SnipCaptureUI 公開 Facade を生成
        // ロガーは Serilog（%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log、日次ローテーション・7日で自動削除）へ転送する。
        // AppLog.CreateLogger が Serilog のファイル出力を構成するため、コード内の設定は不要。
        _snipCaptureLogger = AppLog.CreateLogger("SnipCaptureUI");
        _snipCapture = new SnipCapture(logger: _snipCaptureLogger);
        // キャプチャ完了イベントを購読する（同期実行: 画像変更 → 完了後に APP が再表示される）
        _snipCapture.CaptureCompleted += OnCaptureCompleted;

        // 設定変更を SnipCapture へ即時反映する（MVVM 的: 設定画面での変更がセッション開始を待たずに反映される）。
        // SnipCapture.AppearanceSettings 側で同一値の再設定はスキップされるため、無駄な更新は発生しない。
        // 例外が発生しても UI を壊さないよう try-catch で保護し、ログに記録する。
        _settingsService.Current.PropertyChanged += (_, e) => TryApplySnipCaptureSettings(e.PropertyName ?? "<null>");
        // 起動時の初期設定を反映する
        ApplySnipCaptureSettings();

        // DPI スケーリング（描画スケール）の変化に追従する（別モニタへの移動・ディスプレイ設定変更時）
        ScalingChanged += (_, _) => TryApplySnipCaptureSettings(nameof(ScalingChanged));

        // ViewModel にコールバックを設定
        SetupViewModelCallbacks();

        // CapturePage を初期化（SnipCapture を注入）
        CaptureContent.Initialize(viewModel, settingsService, this, _snipCapture);

        // グローバルホットキーを登録（ネイティブウィンドウハンドルが必要）
        // 実際の登録は NavigateTo(AppPage.Capture) が行う（キャプチャページ表示時のみ有効）
        _hotKeyService = hotKeyService;
        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is not null)
        {
            _hotKeyService.Initialize(platformHandle.Handle);
            _hotKeyService.HotKeyPressed += OnHotKeyPressed;
        }

        // ウィンドウ位置・サイズの復元
        RestoreWindowBounds();

        // ナビゲーション開閉状態の復元（設定ファイルから。購読設定前のため書き戻しは発生しない）
        _viewModel.IsNavigationExpanded = _settingsService.Current.IsNavigationExpanded;

        // ナビゲーション開閉状態の変更を設定ファイルへ即時保存する（次回起動時に復元）
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsNavigationExpanded))
            {
                _settingsService.Current.IsNavigationExpanded = _viewModel.IsNavigationExpanded;
                _settingsService.Save();

                // 開閉に応じてナビパネルの高さを調整する（閉じたらコンテンツ分に縮む）
                UpdateNavPanelLayout();
            }
        };

        // 初期ページ（キャプチャ）を選択状態にする
        NavigateTo(AppPage.Capture);

        // ナビパネルのアイコン（SVG）を設定する
        SetupNavIcons();

        // アプリ（ウィンドウ）アイコンを設定する
        Icon = IconLoader.LoadWindowIcon("ic_fluent_scan_table_24_regular.svg");

        // 初期のナビパネル高さを設定する（IsNavigationExpanded の設定後）
        UpdateNavPanelLayout();

        // アンロード時の処理
        Closing += OnMainWindowClosing;
        Activated += OnMainWindowActivated;
        Deactivated += OnMainWindowDeactivated;
    }

    /// <summary>ナビパネルの高さを開閉状態に応じて調整する。</summary>
    /// <remarks>
    /// - 展開時: <see cref="VerticalAlignment.Stretch"/> でウィンドウの全高に広げる。<br/>
    /// - 収納時: <see cref="VerticalAlignment.Top"/> + 自動高さでコンテンツ分（タイトルバー + アイテム）に縮む。<br/>
    /// NavPanel の Background は Transparent（MainWindow.axaml）で、その背後に全高の背景色 Border を<br/>
    /// 配置しているため、収納時に NavPanel が縮んでもその下は背景色で塗られ、白く見えない。<br/>
    /// どちらの状態でも左上の同じ位置に表示され、アイテムの位置は変化しない。
    /// </remarks>
    private void UpdateNavPanelLayout()
    {
        // 収納時はコンテンツ高さ（Top）。展開時は全高（Stretch）
        NavPanel.VerticalAlignment = _viewModel.IsNavigationExpanded ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        NavPanel.Height = double.NaN; // Auto（コンテンツ高さ）
    }

    /// <summary>ナビパネルのトグルボタンとアイテム（キャプチャ / フォルダビュー / Undo 一覧 / 設定）のアイコンを SVG から設定する。</summary>
    /// <remarks>各アイコンのジオメトリは <see cref="Icons.IconLoader.LoadGeometry"/> で読み込む。</remarks>
    private void SetupNavIcons()
    {
        NavToggleIcon.Data = IconLoader.LoadGeometry("ic_fluent_arrow_autofit_width_dotted_24_regular.svg");
        NavCaptureIcon.Data = IconLoader.LoadGeometry("ic_fluent_scan_table_24_regular.svg");
        NavFolderViewIcon.Data = IconLoader.LoadGeometry("ic_fluent_folder_search_24_regular.svg");
        NavUndoIcon.Data = IconLoader.LoadGeometry("ic_fluent_arrow_undo_24_regular.svg");
        NavSettingsIcon.Data = IconLoader.LoadGeometry("ic_fluent_settings_24_regular.svg");
    }

    /// <summary>ViewModel に UI 操作用のコールバックを設定する。</summary>
    private void SetupViewModelCallbacks()
    {
        _viewModel.CopyToClipboardAsync = async (Bitmap? bitmap) =>
        {
            if (bitmap is not null && Clipboard is not null)
            {
                // ビットマップとしてクリップボードにコピー
                await Clipboard.SetBitmapAsync(bitmap);
                return true;
            }
            return false;
        };
        _viewModel.PasteFromClipboardAsync = async () =>
        {
            if (Clipboard is not null)
            {
                try
                {
                    var bitmap = await Clipboard.TryGetBitmapAsync();
                    if (bitmap is not null) return bitmap;
                }
                catch
                {
                    // クリップボードに画像がない場合は無視
                }
            }
            return null;
        };
        _viewModel.ConfirmOverwriteAsync = async (fileName) =>
        {
            return await ConfirmDialog.ShowAsync(
                this,
                "上書き確認",
                $"ファイル「{fileName}」は既に存在します。上書きしますか？");
        };
    }

    /// <summary>ホットキーを登録する。</summary>
    private void RegisterHotKeys()
    {
        var s = _settingsService.Current;
        _hotKeyService.RegisterAll(
        [
            (CaptureType.ScreenSelect, s.SelectScreenHotKey),
            (CaptureType.WindowSelect, s.WindowSelectHotKey),
            (CaptureType.AreaSelect, s.AreaSelectHotKey),
        ]);
    }

    /// <summary>WM_HOTKEY 受信時（ホットキーサービスから対応するアクションが配信される）。</summary>
    private void OnHotKeyPressed(CaptureType captureType)
    {
        _logger.LogHotKeyReceived();
        _logger.LogHotKeyTriggered(captureType);
        StartSelectionMode(captureType);
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowBounds();
        if (_hotKeyService is not null)
        {
            _hotKeyService.HotKeyPressed -= OnHotKeyPressed;
            _hotKeyService.Dispose();
        }
        CaptureContent.CleanupCaptureResources();
        _snipCapture.Dispose();
    }

    /// <summary>ウィンドウのアクティブ化を記録する。</summary>
    private void OnMainWindowActivated(object? sender, EventArgs e)
    {
        _logger.LogActivated();
    }

    /// <summary>ウィンドウの非アクティブ化を記録する。</summary>
    private void OnMainWindowDeactivated(object? sender, EventArgs e)
    {
        _logger.LogDeactivated();
    }

    /// <summary>ウィンドウ位置・サイズを復元する。</summary>
    private void RestoreWindowBounds()
    {
        var bounds = _settingsService.Current.MainWindowBounds;
        if (bounds is not null)
        {
            Position = new PixelPoint(bounds.Value.X, bounds.Value.Y);
            Width = bounds.Value.Width;
            Height = bounds.Value.Height;
        }

        // PixelRect? / WindowState は直接設定可能
        WindowState = _settingsService.Current.MainWindowState;
    }

    /// <summary>ウィンドウ位置・サイズを保存する。</summary>
    private void SaveWindowBounds()
    {
        _settingsService.Current.MainWindowBounds = new PixelRect(
            Position.X, Position.Y, (int)Width, (int)Height);

        _settingsService.Current.MainWindowState = WindowState;
        _settingsService.Save();
    }

    // ===================== ナビゲーション =====================

    /// <summary>指定されたページに遷移する。</summary>
    /// <param name="page">遷移先ページ（<see cref="AppPage"/>）。</param>
    /// <remarks>
    /// ページ遷移は enum で管理し、index のマジックナンバーによる誤遷移（ページ追加時のずれ）を防ぐ。<br/>
    /// Undo 一覧は <see cref="UndoPage"/> 自身が IsVisible 変更を監視して最新の履歴を読み込むため、
    /// MainWindow からの明示的な Refresh 呼び出しは不要。
    /// </remarks>
    private void NavigateTo(AppPage page)
    {
        if (_navSelectedPage == page) return;
        _navSelectedPage = page;

        // 全ページを非表示
        CaptureContent.IsVisible = false;
        FolderViewContent.IsVisible = false;
        UndoContent.IsVisible = false;
        SettingsContent.IsVisible = false;

        // 選択中のページのみ表示
        switch (page)
        {
            case AppPage.Capture:
                CaptureContent.IsVisible = true;
                UpdateNavItemSelection(NavCapture);
                break;
            case AppPage.FolderView:
                FolderViewContent.IsVisible = true;
                UpdateNavItemSelection(NavFolderView);
                break;
            case AppPage.Undo:
                UndoContent.IsVisible = true;
                UpdateNavItemSelection(NavUndo);
                break;
            case AppPage.Settings:
                SettingsContent.IsVisible = true;
                UpdateNavItemSelection(NavSettings);
                break;
        }

        // グローバルホットキーはキャプチャページ表示時のみ有効
        // （他ページでの誤キャプチャを防ぐ）
        if (page == AppPage.Capture)
        {
            RegisterHotKeys();
        }
        else
        {
            _hotKeyService?.UnregisterAll();
        }
    }

    /// <summary>ナビゲーションアイテムの選択状態を更新する。</summary>
    private static void UpdateNavItemSelection(Border selectedItem)
    {
        // 親の StackPanel から全ての NavItem を取得して選択状態をクリア
        if (selectedItem.Parent is StackPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border border)
                {
                    border.Classes.Remove("selected");
                }
            }
        }
        selectedItem.Classes.Add("selected");
    }

    /// <summary>ナビ「キャプチャ」クリック。</summary>
    private void OnNavCaptureClick(object? sender, RoutedEventArgs e)
        => NavigateTo(AppPage.Capture);

    /// <summary>ナビ「フォルダビュー」クリック。</summary>
    private void OnNavFolderViewClick(object? sender, RoutedEventArgs e)
        => NavigateTo(AppPage.FolderView);

    /// <summary>ナビ「Undo 一覧」クリック。</summary>
    private void OnNavUndoClick(object? sender, RoutedEventArgs e)
        => NavigateTo(AppPage.Undo);

    /// <summary>ナビ「設定」クリック。</summary>
    private void OnNavSettingsClick(object? sender, RoutedEventArgs e)
        => NavigateTo(AppPage.Settings);

    // ===================== キャプチャ関連 =====================

    /// <summary>現在の設定（<see cref="ISettingsService.Current"/>）を SnipCapture へ反映する。</summary>
    /// <remarks>
    /// キャプチャ（<see cref="StartSelectionMode"/>）・切出し（CapturePage）のセッション開始前に呼び出し、<br/>
    /// 設定画面での変更（画像外の選択を禁止 等）をセッションへ即時反映する（起動時設定のままにならないようにする）。<br/>
    /// Settings → CaptureSettings の変換は <see cref="CaptureSettingsMapper"/> に委譲する。
    /// </remarks>
    public void ApplySnipCaptureSettings()
    {
        // 変換は CaptureSettingsMapper に一元化する
        var capSettings = CaptureSettingsMapper.From(_settingsService.Current, DesktopScaling);

        // 設定を SnipCapture に反映（部分更新）
        _snipCapture.AppearanceSettings = capSettings;
    }

    /// <summary>SnipCapture 設定の反映を例外保護付きで行う（失敗時はログに記録）。</summary>
    /// <param name="propertyName">変更の契機となったプロパティ名（ログ用）。</param>
    private void TryApplySnipCaptureSettings(string propertyName)
    {
        try
        {
            ApplySnipCaptureSettings();
        }
        catch (Exception ex)
        {
            _logger.LogSettingsApplyError(ex, propertyName);
        }
    }

    /// <summary>選択モードを開始する。</summary>
    internal async void StartSelectionMode(CaptureType captureType)
    {
        _logger.LogStartSelectionMode(captureType);

        // キャプチャ開始直前に設定を反映する（PropertyChanged 購読に加えて確実に最新の設定を使う。
        // 設定画面での変更が購読で反映されないケース（起動直後・Reload 後など）を防ぐ）。
        ApplySnipCaptureSettings();

        // キャプチャセッションを開始（HideOwnerWindow=true の場合は SnipCapture が自動非表示する）。
        // 画像変更は CaptureCompleted イベント（同期）で行い、完了後に SnipCapture が APP を再表示する。
        // フロー: APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）
        var result = await _snipCapture.StartCaptureAsync(captureType, this);

        var imgInfo = result?.Image is not null ? $"{result.Image.Size.Width}x{result.Image.Size.Height}" : "null";
        _logger.LogSessionReturned(imgInfo, result?.SourceBounds?.ToString() ?? "null");
    }

    /// <summary>キャプチャ完了イベントの受信処理（同期実行。画像変更を行う）。</summary>
    /// <param name="result">キャプチャ結果。キャンセル時は 0x0 の空結果。</param>
    /// <remarks>
    /// イベントは同期型のため、本ハンドラ（画像変更）の完了後に<br/>
    /// SnipCapture のキャプチャセッションがオーナー（本ウィンドウ）を再表示する。<br/>
    /// 既に UI スレッドで発行されるため、UI 更新をそのまま実行できる。
    /// </remarks>
    private void OnCaptureCompleted(CaptureResult? result)
    {
        // キャプチャ結果の処理（画像変更）
        if (result?.Image is { } captured)
        {
            CaptureContent.PushUndo();
            CaptureContent.SetIsCaptureInProgress(true);
            // キャプチャ結果（PbBitmap）を内部表現のままプレビューへ設定する（所有権は ViewModel へ移る）。
            _viewModel.SetPreviewImage(captured);
            CaptureContent.SetIsCaptureInProgress(false);
            CaptureContent.ExitCropMode();
        }
    }
}


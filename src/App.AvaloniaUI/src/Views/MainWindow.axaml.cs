using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.App.AvaloniaUI.Icons;
using EsUtil.SaveEvidence.App.AvaloniaUI.Logging;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
// ILogger は Serilog.ILogger と衝突するため、Microsoft.Extensions.Logging 側を優先する別名で解決する
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views;

/// <summary>SaveEvidence アプリケーションのメインウィンドウ。</summary>
/// <remarks>
/// 画像のプレビュー表示、キャプチャ操作、保存、クリップボード連携、ルーペ表示、<br/>
/// 切り出し選択、ズーム、ホットキー管理など、アプリケーションの主要機能を統括する。<br/>
/// ViewModel（MainViewModel）と連携し、画面キャプチャやファイル保存などの
/// 実際の処理は Model 層に委譲する。<br/>
/// </remarks>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = null!;
    private readonly SettingsService _settingsService = null!;
    private readonly IGlobalHotKeyService<CaptureType> _hotKeyService = null!;

    /// <summary>ホットキー押下の購読（クローズ時に解除する。 解除しないと本ウィンドウが通知先に残る）。</summary>
    private readonly IDisposable? _hotKeySubscription;

    /// <summary>Parts.CapturePreview.ControlAvaloniaUI の公開 Facade（キャプチャセッションの窓口）。</summary>
    private readonly CaptureSessionControl _captureSessionControl = null!;

    /// <summary>Parts.CapturePreview.ControlAvaloniaUI へ注入するロガー（Serilog 経由でファイル出力）。</summary>
    private readonly ILogger _captureSessionControlLogger = null!;

    /// <summary>アプリケーションロガー。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger("MainWindow");

    // ナビゲーション状態（AppPage 型。 初期値 null: コンストラクタの NavigateTo(AppPage.Capture) が
    // 早期 return せず、初期ページの選択状態が設定されるようにする）
    private AppPage? _navSelectedPage;

    /// <summary>MainWindow を初期化する。</summary>
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

        // Parts.CapturePreview.ControlAvaloniaUI 公開 Facade を生成
        // ロガーは Serilog（%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log、日次ローテーション・7日で自動削除）へ転送する。
        // AppLog.CreateLogger が Serilog のファイル出力を構成するため、コード内の設定は不要。
        _captureSessionControlLogger = AppLog.CreateLogger("Parts.CapturePreview.ControlAvaloniaUI");
        _captureSessionControl = new CaptureSessionControl(logger: _captureSessionControlLogger);
        // キャプチャ完了イベントを購読する（同期実行: 画像変更 → 完了後に APP が再表示される）
        _captureSessionControl.CaptureCompleted += OnCaptureCompleted;

        // 設定変更を CaptureSessionControl へ即時反映する（MVVM 的: 設定画面での変更がセッション開始を待たずに反映される）。
        // CaptureSessionControl.AppearanceSettings 側で同一値の再設定はスキップされるため、無駄な更新は発生しない。
        // 例外が発生しても UI を壊さないよう try-catch で保護し、ログに記録する。
        _settingsService.Current.PropertyChanged += (_, e) => TryApplyCaptureSessionSettings(e.PropertyName ?? "<null>");
        // 起動時の初期設定を反映する
        ApplyCaptureSessionSettings();

        // DPI スケーリング（描画スケール）の変化に追従する（別モニタへの移動・ディスプレイ設定変更時）
        ScalingChanged += (_, _) => TryApplyCaptureSessionSettings(nameof(ScalingChanged));

        // ViewModel にコールバックを設定
        SetupViewModelCallbacks();

        // CapturePage を初期化（CaptureSessionControl を注入）
        CaptureContent.Initialize(viewModel, settingsService, this, _captureSessionControl);

        // 起動直後のプレビュー画像（設定で「グラデーション画像」か「空白」を選ぶ）。
        // ページの初期化より後に設定する（ページは PreviewImage の変更通知で画像を取り込むため）
        StartupPreviewImage.Apply(viewModel, _settingsService.Current.StartupPreview);

        // ナビゲーション パネルのフッターへ UI 名と実際のバージョンを表示する
        // （バージョンの整形は AppVersion（App.Core）が単一所有する）
        VersionText.Text = AppVersion.For("AvaloniaUI");

        // グローバルホットキーを登録（ネイティブウィンドウハンドルが必要）
        // 実際の登録は NavigateTo(AppPage.Capture) が行う（キャプチャページ表示時のみ有効）
        _hotKeyService = hotKeyService;
        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is not null)
        {
            _hotKeyService.Initialize(platformHandle.Handle);
            _hotKeySubscription = _hotKeyService.Subscribe(OnHotKeyPressed);
        }

        // ウィンドウ位置・サイズの復元
        RestoreWindowBounds();

        // ナビゲーション開閉状態の復元（設定ファイルから。 購読設定前のため書き戻しは発生しない）
        _viewModel.IsNavigationExpanded = _settingsService.Current.IsNavigationExpanded;

        // ナビゲーション開閉状態の変更を設定ファイルへ即時保存する（次回起動時に復元）
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.IsNavigationExpanded), StringComparison.Ordinal))
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

        // アプリ（ウィンドウ）アイコンを設定する。 左上のタイトル バーにも同じアイコンを出す
        Icon = IconLoader.LoadWindowIcon(AppIdentity.IconFileName);
        AppIconImage.Data = IconLoader.LoadGeometry(AppIdentity.IconFileName);

        // 初期のナビパネル高さを設定する（IsNavigationExpanded の設定後）
        UpdateNavPanelLayout();

        // ナビゲーションが開いているとき、ナビ領域の外の操作（押下）で閉じる。
        // トンネル段階で購読するのは、ページのコントロールが押下を処理した（Handled）場合でも閉じるため
        AddHandler(PointerPressedEvent, OnNavigationOutsidePointerPressed, RoutingStrategies.Tunnel);

        // アンロード時の処理
        Closing += OnMainWindowClosing;
        Activated += OnMainWindowActivated;
        Deactivated += OnMainWindowDeactivated;

        // 一覧ページのサムネイルを先に生成しておく（最初の切り替えを待たせない）
        // ウィンドウを表示した後（Opened）のディスパッチャ ターンで開始し、描画のジョブより後に回す（起動の描画を遅らせない）。
        // 生成は低負荷で進み、キャプチャページの操作を妨げない。
        Opened += (_, _) => Dispatcher.UIThread.Post(WarmUpThumbnails, DispatcherPriority.Background);
    }

    /// <summary>ナビ領域の外の押下でナビゲーションを閉じる。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// ナビはページ領域の左端へ重ねて表示する（幅は <see cref="MainViewModel.NavPanelWidth"/>）ため、<br/>
    /// 押下位置の X がその幅以上ならナビ領域の外である。 押下自体は処理しない（背後の要素の操作を妨げない）。
    /// </remarks>
    private void OnNavigationOutsidePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_viewModel.IsNavigationExpanded)
        {
            return;
        }

        // ナビ領域は「タイトル バーの下・ナビの幅の内側」である。 その外（タイトル バーを含む）は閉じる
        var position = e.GetPosition(this);
        var insideNavigation = position.Y >= WindowLayout.TitleBarHeight && position.X < _viewModel.NavPanelWidth;
        if (insideNavigation)
        {
            return;
        }

        CollapseNavigation();
    }

    /// <summary>展開中のナビゲーションを閉じる。</summary>
    /// <remarks>設定への保存と高さの更新は <see cref="MainViewModel.IsNavigationExpanded"/> の購読が行う。</remarks>
    private void CollapseNavigation()
    {
        if (_viewModel.IsNavigationExpanded)
        {
            _viewModel.IsNavigationExpanded = false;
        }
    }

    /// <summary>タイトル バー相当の帯の押下でウィンドウの移動を開始する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// 押下位置がタイトル バーの高さ以内のときだけ移動を開始する（ページの操作を妨げない）。<br/>
    /// ボタンなどが押下を処理した場合は <see cref="RoutedEventArgs.Handled"/> が立つため、本ハンドラは呼ばれない<br/>
    /// （ツールバーのボタン操作と両立する）。 ダブルクリックで最大化と元のサイズを切り替える。
    /// </remarks>
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // ウィンドウ座標（クライアント領域の左上基準）で判定する
        if (e.GetPosition(this).Y > TitleBarDragHeight)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        BeginMoveDrag(e);
        e.Handled = true;
    }

    /// <summary>タイトル バー相当の領域の高さ（DIP）。 この帯の押下でウィンドウを移動する。</summary>
    private const double TitleBarDragHeight = 32;

    /// <summary>最小化ボタンの押下でウィンドウを最小化する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    /// <summary>最大化ボタンの押下で最大化と元のサイズを切り替える。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeGlyph();
    }

    /// <summary>閉じるボタンの押下でウィンドウを閉じる。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>最大化ボタンのアイコンとツールヒントを現在の状態へ合わせる。</summary>
    private void UpdateMaximizeGlyph()
    {
        if (this.FindControl<TextBlock>("MaximizeGlyph") is not { } glyph)
        {
            return;
        }

        var maximized = WindowState == WindowState.Maximized;
        // Segoe Fluent Icons: E922 = 最大化 / E923 = 元のサイズに戻す
        glyph.Text = maximized ? "\uE923" : "\uE922";
        if (this.FindControl<Button>("MaximizeButton") is { } button)
        {
            ToolTip.SetTip(button, maximized ? "元のサイズに戻す" : "最大化");
        }
    }

    /// <summary>一覧ページ（フォルダビュー / Undo 一覧）のサムネイルを低負荷で先に生成しておく。</summary>
    /// <remarks>
    /// 各ページはウィンドウの内容として生成済みであり（<c>IsVisible=False</c>）、本メソッドは
    /// ページを表示せずに一覧を構築して生成要求を積む。 一覧を構築しておくと、最初の切り替えでは
    /// 生成済みのサムネイルがそのまま並ぶ。
    /// </remarks>
    private void WarmUpThumbnails()
    {
        try
        {
            FolderViewContent.WarmUp();
            UndoContent.WarmUp();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 先読みの失敗はアプリの動作を妨げない（ページを開いたときに改めて構築される）
            s_logger.LogThumbnailGenerateError(ex);
        }
    }

    /// <summary>ナビパネルの高さを開閉状態に応じて調整する。</summary>
    /// <remarks>
    /// - 展開時: <see cref="VerticalAlignment.Stretch"/> でウィンドウの全高に広げる（<b>ページ領域の上へ重ねる</b>。<br/>
    ///   ページ領域は収納時の位置から移動しない）。<br/>
    /// - 収納時: <see cref="VerticalAlignment.Top"/> + 自動高さでコンテンツ分（タイトルバー + アイテム）に縮む。<br/>
    /// NavPanel の Background は Transparent（MainWindow.axaml）で、その背後に全高の背景色 Border を<br/>
    /// 配置しているため、収納時に NavPanel が縮んでもその下は背景色で塗られ、白く見えない。<br/>
    /// どちらの状態でも左上の同じ位置に表示され、アイテムの位置は変化しない。
    /// </remarks>
    private void UpdateNavPanelLayout()
    {
        // 収納時はコンテンツ高さ（Top）。 展開時は全高（Stretch）
        NavPanel.VerticalAlignment = _viewModel.IsNavigationExpanded ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        NavPanel.Height = double.NaN; // Auto（コンテンツ高さ）

        // 開いているときは「閉じる」、閉じているときは「開く」のアイコンにする
        NavToggleIcon.Data = IconLoader.LoadGeometry(
            _viewModel.IsNavigationExpanded ? NavIconPanelLeftContract : NavIconPanelRightContract);
        NavTitleText.IsVisible = _viewModel.IsNavigationExpanded;
    }

    /// <summary>ナビゲーション パネルの開閉アイコン（開いているときに使う「閉じる」）。</summary>
    private const string NavIconPanelLeftContract = "ic_fluent_panel_left_contract_24_filled.svg";

    /// <summary>ナビゲーション パネルの開閉アイコン（閉じているときに使う「開く」）。</summary>
    private const string NavIconPanelRightContract = "ic_fluent_panel_right_contract_24_filled.svg";

    /// <summary>ナビゲーションの開閉ボタンの押下で開閉を切り替える。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnNavToggleClick(object? sender, RoutedEventArgs e)
        => _viewModel.IsNavigationExpanded = !_viewModel.IsNavigationExpanded;

    /// <summary>左上のアプリ アイコンの押下でウィンドウの標準メニューを出す。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 項目と並びは <see cref="WindowMenu"/>（App.Core）が単一所有する（4 つの UI 版で同じメニューになる）。<br/>
    /// 移動・サイズ変更は OS のモードへ入るため、Win32 のシステム コマンドを送る（<see cref="SystemWindowCommands"/>）。
    /// </remarks>
    private void OnAppIconClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (var entry in WindowMenu.Items(WindowState == WindowState.Maximized))
        {
            if (entry.IsSeparatorBefore)
            {
                menu.Items.Add(new Separator());
            }

            var item = new MenuItem { Header = entry.Label, IsEnabled = entry.IsEnabled };
            var action = entry.Action;
            item.Click += (_, _) => ExecuteWindowMenuAction(action);
            menu.Items.Add(item);
        }

        menu.Open(AppIconButton);
    }

    /// <summary>ウィンドウの標準メニューの項目を実行する。</summary>
    /// <param name="action">実行する操作。</param>
    private void ExecuteWindowMenuAction(WindowMenuAction action)
    {
        switch (action)
        {
            case WindowMenuAction.Restore:
                WindowState = WindowState.Normal;
                UpdateMaximizeGlyph();
                break;
            case WindowMenuAction.Move:
                SendSystemCommand(SystemWindowCommands.BeginMove);
                break;
            case WindowMenuAction.Size:
                SendSystemCommand(SystemWindowCommands.BeginSize);
                break;
            case WindowMenuAction.Minimize:
                WindowState = WindowState.Minimized;
                break;
            case WindowMenuAction.Maximize:
                WindowState = WindowState.Maximized;
                UpdateMaximizeGlyph();
                break;
            case WindowMenuAction.Close:
                Close();
                break;
            default:
                break;
        }
    }

    /// <summary>ウィンドウへネイティブのシステム コマンドを送る。</summary>
    /// <param name="command">送るコマンド（ハンドルを受け取る）。</param>
    private void SendSystemCommand(Func<EsPlatformHandle, bool> command)
    {
        if (TryGetPlatformHandle() is { } handle)
        {
            _ = command(new EsPlatformHandle(handle.Handle));
        }
    }

    /// <summary>ナビパネルのトグルボタンとアイテム（キャプチャ / フォルダビュー / Undo 一覧 / 設定）のアイコンを SVG から設定する。</summary>
    /// <remarks>各アイコンのジオメトリは <see cref="Icons.IconLoader.LoadGeometry"/> で読み込む。</remarks>
    private void SetupNavIcons()
    {
        NavCaptureIcon.Data = IconLoader.LoadGeometry("ic_fluent_scan_table_24_regular.svg");
        NavFolderViewIcon.Data = IconLoader.LoadGeometry("ic_fluent_folder_search_24_regular.svg");
        NavUndoIcon.Data = IconLoader.LoadGeometry("ic_fluent_arrow_undo_24_regular.svg");
        NavSettingsIcon.Data = IconLoader.LoadGeometry("ic_fluent_settings_24_regular.svg");
    }

    /// <summary>ViewModel に UI 操作用のコールバックを設定する。</summary>
    /// <remarks>
    /// ViewModel（<c>App.Core</c>）は UI 非依存のため、クリップボード操作（Avalonia 依存）は<br/>
    /// 本メソッドで境界変換して委譲する（コア型 <see cref="EsBitmap"/> ⇔ Avalonia <see cref="Bitmap"/>）。
    /// </remarks>
    private void SetupViewModelCallbacks()
    {
        _viewModel.CopyToClipboardCallback = async image =>
        {
            if (Clipboard is null)
            {
                return false;
            }

            // クリップボードは Avalonia Bitmap を要求するため、境界で変換する（所有権は本メソッド内で完結）
            using var bitmap = image.ToAvaloniaBitmap();
            await Clipboard.SetBitmapAsync(bitmap);
            return true;
        };
        _viewModel.PasteFromClipboardCallback = async () =>
        {
            if (Clipboard is null)
            {
                return null;
            }

            try
            {
                var bitmap = await Clipboard.TryGetBitmapAsync();
                // 貼り付け結果（Avalonia Bitmap）をコア型へ変換して返す（変換後は不要になるため破棄する）
                using (bitmap)
                {
                    return bitmap?.ToEsBitmap();
                }
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // クリップボードに画像がない場合は無視
                return null;
            }
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
    /// <remarks>
    /// <b>登録できなかった組み合わせを記録する</b>: 他のアプリが同じ組み合わせを使用している場合にも起こるため、
    /// 1 件の競合でアプリを止めず、失敗した組み合わせだけを Warning で残す（規則は <c>ILogger</c> の呼び出し側が持つ）。
    /// </remarks>
    private void RegisterHotKeys()
    {
        var s = _settingsService.Current;
        var failed = _hotKeyService.RegisterAll(
        [
            (CaptureType.ScreenSelect, s.SelectScreenHotKey),
            (CaptureType.WindowSelect, s.WindowSelectHotKey),
            (CaptureType.AreaSelect, s.AreaSelectHotKey),
        ]);

        foreach (var (action, setting) in failed)
        {
            s_logger.LogHotKeyRegisterFailed(action, setting.Key);
        }
    }

    /// <summary>WM_HOTKEY 受信時（ホットキーサービスから対応するアクションが配信される）。</summary>
    /// <param name="captureType">押下されたホットキーに対応するアクション。</param>
    private void OnHotKeyPressed(CaptureType captureType)
    {
        s_logger.LogHotKeyReceived();
        s_logger.LogHotKeyTriggered(captureType);
        _ = StartSelectionModeAsync(captureType);
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowBounds();
        if (_hotKeyService is not null)
        {
            _hotKeySubscription?.Dispose();
            _hotKeyService.Dispose();
        }
        CaptureContent.CleanupCaptureResources();
        _captureSessionControl.Dispose();
    }

    /// <summary>ウィンドウのアクティブ化を記録する。</summary>
    private void OnMainWindowActivated(object? sender, EventArgs e)
    {
        s_logger.LogActivated();
    }

    /// <summary>ウィンドウの非アクティブ化を記録し、展開中のナビゲーションを閉じる。</summary>
    private void OnMainWindowDeactivated(object? sender, EventArgs e)
    {
        s_logger.LogDeactivated();

        // ウィンドウの外を操作した場合もナビ領域の外の操作とみなして閉じる
        CollapseNavigation();
    }

    /// <summary>ウィンドウ位置・サイズを復元する。</summary>
    /// <remarks>
    /// 保存値は<b>画面物理ピクセル</b>（4 つの UI 版が共有する設定の単位）。 Avalonia の <see cref="Window.Position"/> は<br/>
    /// 物理ピクセルだが <see cref="Layoutable.Width"/> / <see cref="Layoutable.Height"/> は DIP のため、モニターの<br/>
    /// スケールで割って設定する（そのまま入れると高 DPI 環境でウィンドウが倍率分だけ大きくなる）。<br/>
    /// 画面外・最小化の値は復元しない（判定は <see cref="WindowPlacementRule"/> が単一所有する）。
    /// </remarks>
    private void RestoreWindowBounds()
    {
        if (_settingsService.Current.MainWindowBounds is { } saved)
        {
            var visibleArea = GetVisibleScreenArea();
            if (WindowPlacementRule.CanRestore(saved, visibleArea))
            {
                var bounds = WindowPlacementRule.ClampToVisibleArea(saved, visibleArea);
                var scaling = ResolveScaling(bounds.X, bounds.Y);
                Position = new PixelPoint(bounds.X, bounds.Y);
                Width = bounds.Width / scaling;
                Height = bounds.Height / scaling;
            }
        }

        // 設定は UI 非依存のコア型で保持されるため、境界で Avalonia 型へ変換する
        // （最小化は通常表示へ置き換える: 最小化のまま起動すると「表示されない」ように見える）
        WindowState = WindowPlacementRule.ToRestorableState(_settingsService.Current.MainWindowState)
            .ToAvaloniaWindowState();
    }

    /// <summary>ウィンドウ位置・サイズを保存する。</summary>
    /// <remarks>保存先の単位は画面物理ピクセル（他 UI 版と同じ）。 DIP の <see cref="Layoutable.Width"/> を換算する。</remarks>
    private void SaveWindowBounds()
    {
        var scaling = DpiScaler.NormalizeScaling(RenderScaling);
        _settingsService.Current.MainWindowBounds = new EsRectInt(
            Position.X, Position.Y,
            WindowPlacementRule.ToPhysicalLength(Width, scaling),
            WindowPlacementRule.ToPhysicalLength(Height, scaling));

        _settingsService.Current.MainWindowState = WindowState.ToEsWindowState();
        _settingsService.Save();
    }

    /// <summary>全モニターを包含する表示領域を取得する（画面物理ピクセル）。</summary>
    /// <returns>表示領域。 モニターが取得できない場合は 0 サイズ（復元しない）。</returns>
    private EsRectInt GetVisibleScreenArea()
    {
        var screens = Screens.All;
        if (screens.Count == 0)
        {
            return new EsRectInt(0, 0, 0, 0);
        }

        var left = screens.Min(screen => screen.Bounds.X);
        var top = screens.Min(screen => screen.Bounds.Y);
        var right = screens.Max(screen => screen.Bounds.X + screen.Bounds.Width);
        var bottom = screens.Max(screen => screen.Bounds.Y + screen.Bounds.Height);
        return new EsRectInt(left, top, right - left, bottom - top);
    }

    /// <summary>指定位置のモニターの DPI スケール係数を取得する。</summary>
    /// <param name="x">X 座標（画面物理ピクセル）。</param>
    /// <param name="y">Y 座標（画面物理ピクセル）。</param>
    /// <returns>スケール係数（1.0 = 96 DPI）。 取得できない場合はプライマリ モニターの値。</returns>
    private double ResolveScaling(int x, int y)
    {
        var scaling = Screens.ScreenFromPoint(new PixelPoint(x, y))?.Scaling
            ?? Screens.Primary?.Scaling
            ?? 1.0;
        return DpiScaler.NormalizeScaling(scaling);
    }

    // ===================== ナビゲーション =====================

    /// <summary>指定されたページに遷移する。</summary>
    /// <param name="page">遷移先ページ（<see cref="AppPage"/>）。</param>
    /// <remarks>
    /// ページ遷移は enum で管理し、index のマジックナンバーによる誤遷移（ページ追加時のずれ）を防ぐ。<br/>
    /// Undo 一覧は <see cref="Views.Pages.UndoPage"/> 自身が IsVisible 変更を監視して最新の履歴を読み込むため、
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
    {
        NavigateTo(AppPage.Capture);
        CollapseNavigation();
    }

    /// <summary>ナビ「フォルダビュー」クリック。</summary>
    private void OnNavFolderViewClick(object? sender, RoutedEventArgs e)
    {
        NavigateTo(AppPage.FolderView);
        CollapseNavigation();
    }

    /// <summary>ナビ「Undo 一覧」クリック。</summary>
    private void OnNavUndoClick(object? sender, RoutedEventArgs e)
    {
        NavigateTo(AppPage.Undo);
        CollapseNavigation();
    }

    /// <summary>ナビ「設定」クリック。</summary>
    private void OnNavSettingsClick(object? sender, RoutedEventArgs e)
    {
        NavigateTo(AppPage.Settings);
        CollapseNavigation();
    }

    // ===================== キャプチャ関連 =====================

    /// <summary>現在の設定（<see cref="ISettingsService.Current"/>）を CaptureSessionControl へ反映する。</summary>
    /// <remarks>
    /// キャプチャ（<see cref="StartSelectionModeAsync"/>）・切出し（CapturePage）のセッション開始前に呼び出し、<br/>
    /// 設定画面での変更（画像外の選択を禁止 等）をセッションへ即時反映する（起動時設定のままにならないようにする）。<br/>
    /// Settings → CaptureSettings の変換は <see cref="CaptureSettingsMapper"/> に委譲する。
    /// </remarks>
    public void ApplyCaptureSessionSettings()
    {
        // 変換は CaptureSettingsMapper に一元化する
        var capSettings = CaptureSettingsMapper.From(_settingsService.Current, DesktopScaling);

        // 設定を CaptureSessionControl に反映（部分更新）
        _captureSessionControl.AppearanceSettings = capSettings;
    }

    /// <summary>CaptureSessionControl 設定の反映を例外保護付きで行う（失敗時はログに記録）。</summary>
    /// <param name="propertyName">変更の契機となったプロパティ名（ログ用）。</param>
    private void TryApplyCaptureSessionSettings(string propertyName)
    {
        try
        {
            ApplyCaptureSessionSettings();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogSettingsApplyError(ex, propertyName);
        }
    }

    /// <summary>選択モードを開始する。</summary>
    /// <param name="captureType">開始するキャプチャ種別。</param>
    /// <remarks>
    /// 呼び出し側は完了を待たない（ホットキー・メニューのハンドラーは同期のため）。<br/>
    /// セッション開始の失敗はアプリを継続し、原因をログへ記録する（非同期の完了を待たないため
    /// 例外を呼び出し側へ返せない。 `async void` にすると例外がプロセスを落とす）。
    /// </remarks>
    internal async Task StartSelectionModeAsync(CaptureType captureType)
    {
        s_logger.LogStartSelectionMode(captureType);

        // キャプチャ開始直前に設定を反映する（PropertyChanged 購読に加えて確実 に最新の設定を使う。
        // 設定画面での変更が購読で反映されないケース（起動直後・Reload 後など）を防ぐ）。
        ApplyCaptureSessionSettings();

        try
        {
            // キャプチャセッションを開始（HideOwnerWindow=true の場合は CaptureSessionControl が自動非表示する）。
            // 画像変更は CaptureCompleted イベント（同期）で行い、完了後に CaptureSessionControl が APP を再表示する。
            // フロー: APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）
            var result = await _captureSessionControl.StartCaptureAsync(captureType, this);

            var imgInfo = result?.Image is not null ? $"{result.Image.Size.Width}x{result.Image.Size.Height}" : "null";
            s_logger.LogSessionReturned(imgInfo, result?.SourceBounds?.ToString() ?? "null");
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // セッション開始の失敗（画面取得不可等）はアプリを継続する
            s_logger.LogCaptureStartError(ex, captureType);
        }
    }

    /// <summary>キャプチャ完了イベントの受信処理（同期実行。 画像変更を行う）。</summary>
    /// <param name="sender">イベントの発信元（使用しない）。</param>
    /// <param name="e">キャプチャ結果。 キャンセル時は 0x0 の空結果。</param>
    /// <remarks>
    /// イベントは同期型のため、本ハンドラ（画像変更）の完了後に<br/>
    /// CaptureSessionControl のキャプチャセッションがオーナー（本ウィンドウ）を再表示する。<br/>
    /// 既に UI スレッドで発行されるため、UI 更新をそのまま実行できる。
    /// </remarks>
    private void OnCaptureCompleted(object? sender, CaptureCompletedEventArgs e)
    {
        // キャプチャ結果の処理（画像変更）
        if (e.Result?.Image is { } captured)
        {
            CaptureContent.PushUndo();
            CaptureContent.SetIsCaptureInProgress(true);
            // キャプチャ結果（EsBitmap）を内部表現のままプレビューへ設定する（所有権は ViewModel へ移る）。
            _viewModel.SetPreviewImage(captured);
            CaptureContent.SetIsCaptureInProgress(false);
            CaptureContent.ExitCropMode();

            // 通知メッセージは画像の差し替え（SetPreviewImage が StatusMessage を空にする）より後に設定する
            // （先に設定すると消える）。 文言はコア（StatusMessages）が単一所有する
            _viewModel.StatusMessage = StatusMessages.CaptureCompleted(captured.Width, captured.Height);
        }
    }
}

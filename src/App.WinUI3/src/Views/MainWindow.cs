using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Icons;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using EsUtil.Ui.WinUI3.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using Windows.Foundation;
using Windows.Graphics;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>アプリのメイン ウィンドウ（WinUI 3 版）。</summary>
/// <remarks>
/// <b>構成</b>: 左の <see cref="NavigationView"/> ペインでページを切り替え、右へ選択中のページを表示する。<br/>
/// ページの内容は選択時に初めて生成し、以降は同じ要素を表示する（ページの状態と購読を保持する）。<br/>
/// <br/>
/// <b>VM 連携</b>: <c>App.Core</c> の <see cref="MainViewModel"/>（変更通知付き）をそのまま参照し、<br/>
/// 変更通知の購読で表示を更新する（バインディング エンジンを使わず、必要な箇所だけを明示的に更新する）。<br/>
/// <br/>
/// <b>設定の永続化</b>: 起動時にウィンドウの位置・サイズ・表示状態を復元し、終了時に保存する<br/>
/// （変換は <see cref="WindowPlacementStore"/> が単一所有する）。 ナビゲーション ペインの開閉も設定へ保存する。<br/>
/// <br/>
/// <b>グローバル ホットキー</b>: ウィンドウ ハンドルとウィンドウ プロシージャは生成時に確定するため、<br/>
/// 初回の表示（<c>Activated</c>）で登録する。 終了時に解除する。<br/>
/// <br/>
/// <b>先読み</b>: 初回の描画後に、一覧ページを表示せずに構築してサムネイルの低負荷生成を始める<br/>
/// （起動を遅くせず、最初の切り替えを待たせない）。
/// </remarks>
internal sealed class MainWindow : Window
{
    /// <summary>ロガー（先読みの失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(MainWindow));

    /// <summary>ナビゲーション ページの定義（表示名・ページ・アイコン・内容の生成）。</summary>
    /// <param name="Page">対応するページ。</param>
    /// <param name="Title">ナビに表示する名前。</param>
    /// <param name="Icon">ナビに表示するアイコンの SVG ファイル名。</param>
    /// <param name="Build">内容を生成する関数。</param>
    private sealed record NavEntry(AppPage Page, string Title, string Icon, Func<MainWindow, PageContent> Build);

    /// <summary>ナビゲーション ページの定義（宣言順が表示順になる）。</summary>
    private static readonly NavEntry[] s_entries =
    [
        new(AppPage.Capture, "キャプチャ", "ic_fluent_scan_table_24_regular.svg", static w => CapturePage.Build(w.Services, w)),
        new(AppPage.FolderView, "フォルダビュー", "ic_fluent_folder_search_24_regular.svg", static w => FolderViewPage.Build(w.Services)),
        new(AppPage.Undo, "Undo 一覧", "ic_fluent_arrow_undo_24_regular.svg", static w => UndoPage.Build(w.Services)),
        new(AppPage.Settings, "設定", "ic_fluent_settings_24_regular.svg", static w => SettingsPage.Build(w.Services, w)),
    ];

    /// <summary>ナビゲーション ペインの開閉ボタン（左上へ重ねる）。</summary>
    private Button _navToggle = null!;

    /// <summary>アプリ名（タイトル バー。 開閉に依存しない）。</summary>
    private Border _navTitle = null!;

    /// <summary>ナビゲーションの開閉アイコン（開閉の状態で差し替える）。</summary>
    private ImageIcon _navToggleIcon = null!;

    /// <summary>ナビゲーション ペインのフッター（UI 名とバージョン。 収納時は隠す）。</summary>
    private TextBlock _navFooter = null!;

    /// <summary>ウィンドウ内容のルート（テーマの下地色を塗る対象）。</summary>
    private Grid _root = null!;

    /// <summary>タイトル バー相当の帯をドラッグしているかどうか。</summary>
    private bool _isDragging;

    /// <summary>ドラッグ開始時のポインター位置（ウィンドウ座標・DIP）。</summary>
    private Point _dragStartPointer;

    /// <summary>ドラッグ開始時のウィンドウ位置（画面物理ピクセル）。</summary>
    private PointInt32 _dragStartWindowPosition;

    /// <summary>ページの内容（選択時に生成し、以降は同じ要素を表示する）。</summary>
    private readonly PageContent?[] _pages = new PageContent?[s_entries.Length];

    /// <summary>ページ領域のホスト（ナビゲーションのアイコン列の右）。</summary>
    private readonly Grid _contentHost = new();

    /// <summary>収納時のナビゲーション（アイコン列）。</summary>
    private readonly StackPanel _navRail = new();

    /// <summary>ナビゲーションの列の下地（収納・展開で同じ色）。</summary>
    private Border _navRailBackground = null!;

    /// <summary>収納時のナビゲーションの項目（選択状態の反映に使う）。</summary>
    private readonly List<NavItemVisual> _navRailItems = [];

    /// <summary>グローバル ホットキーの登録。</summary>
    private readonly GlobalHotKeyRegistration _hotKeys = new();

    /// <summary>現在表示中のページ。</summary>
    private AppPage _currentPage = AppPage.Capture;

    /// <summary>初回の表示処理（配置の反映とホットキーの登録）を済ませたかどうか。</summary>
    private bool _isActivated;

    /// <summary>先読みを開始したかどうか。</summary>
    private bool _isWarmUpStarted;

    /// <summary>ウィンドウのサイズを確定したかどうか（保存された配置の復元、または既定サイズの適用）。</summary>
    private bool _isSizeInitialized;

    /// <summary>左上の見出し（アプリ アイコン・アプリ名・開閉ボタン）の下地。</summary>
    private Border _navHeader = null!;

    /// <summary>展開時のナビゲーション（ページ領域の上へ重ねる自前の一覧）。</summary>
    private Border _navOverlay = null!;

    /// <summary>展開時のナビゲーションの項目（選択状態の反映に使う）。</summary>
    private readonly List<NavItemVisual> _navOverlayItems = [];

    /// <summary>ナビゲーションの項目の見た目（収納時のアイコン列と展開時の一覧で共用する）。</summary>
    /// <param name="Page">対応するページ。</param>
    /// <param name="Button">項目のボタン。</param>
    /// <param name="Mark">選択中を示す縦のマーク。</param>
    /// <param name="Icon">項目のアイコン（テーマ変更で描き直す）。</param>
    private sealed record NavItemVisual(AppPage Page, Button Button, Border Mark, Image Icon);

    /// <summary>メイン ウィンドウを生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <remarks>
    /// 1. ナビゲーション ペインを組み、保存済みの開閉状態を反映する。<br/>
    /// 2. コアのコールバック（上書き確認・クリップボード）を UI 層の実装へ配線する。<br/>
    /// 3. 最初のページ（キャプチャ）を生成して表示する。<br/>
    /// 4. 保存済みのウィンドウ配置を復元する。
    /// </remarks>
    public MainWindow(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Services = services;
        Title = AppIdentity.ProductName;

        var viewModel = services.Main;
        var settings = services.Settings;

        // 起動直後のプレビュー画像（設定で「グラデーション画像」か「空白」を選ぶ）
        StartupPreviewImage.Apply(viewModel, settings.Current.StartupPreview);

        // ナビの開閉は設定から復元する（変更は購読が設定へ保存する）
        viewModel.IsNavigationExpanded = settings.Current.IsNavigationExpanded;

        BuildNavigation();
        Content = BuildRoot();
        WireViewModel(viewModel, settings);



        // 上書き確認とクリップボードは UI 型を要求するため、UI 層の実装をコールバックへ配線する
        viewModel.ConfirmOverwriteAsync = fileName => ConfirmDialog.ShowAsync(
            (Content as FrameworkElement)!.XamlRoot!, "上書きの確認", $"{fileName} は既に存在します。上書きしますか？");
        viewModel.CopyToClipboardCallback = image => Task.FromResult(CaptureClipboard.Copy(image));
        viewModel.PasteFromClipboardCallback = CaptureClipboard.PasteAsync;

        SelectPage(_currentPage, force: true);

        // ウィンドウ（タスク バー）アイコンを他の UI 版と同じアイコンにする。
        // WinUI 3 はアイコン ファイルのパスしか受け付けないため、共有 SVG から ICO を書き出して渡す
        if (WindowIconFile.Ensure() is { Length: > 0 } iconPath)
        {
            AppWindow.SetIcon(iconPath);
        }

        // 保存済みの位置・サイズ・表示状態を復元する（表示前でも AppWindow は操作できる）。
        // 復元できなかった場合は、DPI が確定する読み込み後に既定サイズを適用する
        _isSizeInitialized = WindowPlacementStore.Restore(this, settings.Current);

        // ウィンドウの最小サイズを他の UI 版と同じにする（WinUI 3 の標準機能で制限する）
        ApplyMinimumSize();

        AppStyles.ApplyTheme(Content as FrameworkElement, settings.Current.ThemeMode);
        ApplyThemeResources();

        // OS のテーマが変わったとき（System のとき）も色を追随させる
        ((FrameworkElement)Content).ActualThemeChanged += (_, _) => ApplyThemeResources();
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;

        // 先読みは要素ツリーが実体化した後に行う（表示が済んでから一覧の構築を始める）
        ((FrameworkElement)Content).Loaded += (_, _) =>
        {
            // DPI が変わると物理ピクセルの最小サイズも変わる（読み込み後に倍率が確定する）
            if (((FrameworkElement)Content).XamlRoot is { } root)
            {
                root.Changed += (_, _) => ApplyMinimumSize();
            }

            ApplyMinimumSize();
            ApplyDefaultSizeIfNeeded();

            TryWarmUpPages();
        };
    }

    /// <summary>DPI の倍率（要素ツリーが実体化するまでは 1）。</summary>
    private double RasterizationScale => (Content as FrameworkElement)?.XamlRoot?.RasterizationScale ?? 1.0;

    /// <summary>最小サイズを現在の DPI で画面物理ピクセルへ換算して設定する。</summary>
    /// <remarks>
    /// <see cref="OverlappedPresenter.PreferredMinimumWidth"/> / <c>PreferredMinimumHeight</c> は<br/>
    /// 画面物理ピクセルで解釈されるため、他の UI 版と同じ DIP の見た目にするには DPI 倍率を掛ける<br/>
    /// （掛けないと高 DPI ほど最小サイズが他 UI 版より小さくなる。 実測 125%: 外寸 640 × 400 DIP = 800 × 500 ピクセル）。
    /// </remarks>
    private void ApplyMinimumSize()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        var scaling = RasterizationScale;
        presenter.PreferredMinimumWidth = WindowPlacementRule.ToPhysicalLength(AppStyles.MinWindowWidth, scaling);
        presenter.PreferredMinimumHeight = WindowPlacementRule.ToPhysicalLength(AppStyles.MinWindowHeight, scaling);
    }

    /// <summary>保存された配置が無い場合に既定の外寸（640 × 400 DIP）を適用する。</summary>
    /// <remarks>
    /// サイズは画面物理ピクセルで指定するため、<b>DPI が確定した後</b>（要素ツリーの読み込み後）に適用する。<br/>
    /// 1 回だけ適用する（利用者がサイズを変えた後に読み込みが再度走っても上書きしない）。
    /// </remarks>
    private void ApplyDefaultSizeIfNeeded()
    {
        if (_isSizeInitialized)
        {
            return;
        }

        _isSizeInitialized = true;
        var scaling = RasterizationScale;
        AppWindow.Resize(new SizeInt32(
            WindowPlacementRule.ToPhysicalLength(WindowMetrics.DefaultWidth, scaling),
            WindowPlacementRule.ToPhysicalLength(WindowMetrics.DefaultHeight, scaling)));
    }

    /// <summary>アプリ全体で共有するコアオブジェクトを取得する。</summary>
    /// <value>ページへ渡すサービス束。</value>
    public AppServices Services { get; }

    /// <summary>現在表示中のページの内容を取得する。</summary>
    /// <value>表示中のページ。 未生成の場合は null。</value>
    private PageContent? CurrentPageContent => _pages[(int)_currentPage];

    /// <summary>ウィンドウの内容（ナビゲーション + ナビゲーションの開閉ボタン）を組み立てる。</summary>
    /// <returns>組み立てたルート要素。</returns>
    /// <remarks>
    /// <b>クライアント領域をタイトル バーへ拡張する</b>（<see cref="Window.ExtendsContentIntoTitleBar"/>）。<br/>
    /// 他の UI 版と同じく、<b>タイトル バーの位置にはツールバーを表示する</b>（専用の帯を作らない）。<br/>
    /// <b>ウィンドウ操作ボタンは自前で置かない</b>: 拡張しても標準のボタンが描画されるため、<br/>
    /// 自前のボタンを重ねると 2 重に見える（実測 2026-10-17）。<br/>
    /// <b>ドラッグ</b>: 見出し領域の押下で <c>WM_NCLBUTTONDOWN</c>（<c>HTCAPTION</c>）を送り、OS に移動を任せる。
    /// </remarks>
    private FrameworkElement BuildRoot()
    {
        var root = new Grid();
        _root = root;

        // 左の列がナビゲーション（収納時 40 DIP）、右の列がページ領域。
        // ページ領域は収納時の幅だけ右へ寄せたまま動かない（展開したナビは上へ重ねる）
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AppStyles.NavPanelCompactWidth) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(_contentHost, 1);
        root.Children.Add(_contentHost);

        // ナビゲーションの列の下地（上端の見出しと同じ色で下端まで塗る）
        _navRailBackground = new Border
        {
            Background = new SolidColorBrush(AppStyles.ToColor(AppPalette.NavPanel(AppStyles.IsDarkTheme))),
        };
        Grid.SetColumn(_navRailBackground, 0);
        root.Children.Add(_navRailBackground);

        // 収納時のアイコン列（見出しの下から並べる）
        Grid.SetColumn(_navRail, 0);
        root.Children.Add(_navRail);

        // 展開時の一覧と左上の見出し（アプリ アイコン・アプリ名・開閉ボタン）は上へ重ねる
        root.Children.Add(BuildNavigationOverlay());
        root.Children.Add(BuildNavigationHeader());
        Grid.SetColumnSpan(_navOverlay, 2);
        Grid.SetColumnSpan(_navHeader, 2);

        // ページ固有のキー処理を表示中のページへ転送する
        root.KeyDown += OnRootKeyDown;


        // クライアント領域をタイトル バーへ拡張する（標準のウィンドウ操作ボタンが描画される）
        ExtendsContentIntoTitleBar = true;

        // タイトル バー相当の帯（見出し）の押下でウィンドウを移動する。
        // 標準のドラッグ領域（SetTitleBar）だけに頼らず、ポインターの移動量からウィンドウ位置を自前で更新する
        // （ペインの見出しは NavigationView の内部にあり、ドラッグ領域として認識されないことがある）
        root.PointerPressed += OnTitleBarPointerPressed;
        root.PointerMoved += OnTitleBarPointerMoved;
        root.PointerReleased += OnTitleBarPointerReleased;
        root.PointerCaptureLost += OnTitleBarPointerCaptureLost;
        root.DoubleTapped += OnTitleBarDoubleTapped;

        // ナビが開いているとき、ナビ領域の外の押下で閉じる。
        // ページの要素が押下を処理した場合（Handled）でも閉じるよう、処理済みも受ける
        root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnNavigationOutsidePointerPressed), handledEventsToo: true);

        return root;
    }

    /// <summary>左上のアプリ アイコンとナビの開閉ボタンを組み立てる。</summary>
    /// <returns>ページ領域の上へ重ねる要素（左上に置く）。</returns>
    /// <remarks>
    /// <b>重ねる理由</b>: ペインの見出し（<c>PaneHeader</c>）は収納時に表示されないため、自前の要素を<br/>
    /// ページ領域の上へ重ねる。 項目と重ならないよう、ナビゲーションの先頭へ同じ高さの空きを確保する<br/>
    /// （<see cref="BuildNavigation"/> の空の項目）。
    /// </remarks>
    private FrameworkElement BuildNavigationHeader()
    {
        var appIconButton = CreateCaptionButton(WindowLayout.AppIconColumnWidth);
        appIconButton.Height = WindowLayout.AppIconRowHeight;
        appIconButton.Content = new ImageIcon
        {
            Source = IconLoader.LoadThemedImage(AppIdentity.IconFileName, AppStyles.IconRasterSize),
            Width = WindowLayout.AppIconSize,
            Height = WindowLayout.AppIconSize,
        };
        appIconButton.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTipService.SetToolTip(appIconButton, "ナビゲーションを開閉（右クリックでウィンドウ メニュー）");
        // 左クリックでナビを開閉し、右クリックでウィンドウ メニューを出す
        appIconButton.Click += (_, _) => Services.Main.IsNavigationExpanded = !Services.Main.IsNavigationExpanded;
        appIconButton.RightTapped += OnAppIconRightTapped;

        _navToggle = CreateCaptionButton(AppStyles.NavPanelCompactWidth);
        _navToggleIcon = new ImageIcon
        {
            Source = IconLoader.LoadThemedImage(
                NavToggleIconFile(Services.Main.IsNavigationExpanded), AppStyles.IconRasterSize),
            Width = AppStyles.NavIconSize,
            Height = AppStyles.NavIconSize,
        };
        _navToggle.Content = _navToggleIcon;
        _navToggle.HorizontalAlignment = HorizontalAlignment.Left;
        _navToggle.Height = WindowLayout.NavToggleHeight;
        ToolTipService.SetToolTip(_navToggle, "ナビゲーション パネルの開閉");
        _navToggle.Click += (_, _) => Services.Main.IsNavigationExpanded = !Services.Main.IsNavigationExpanded;

        // アプリ名はアプリ アイコンの右（展開時のみ表示。 アイコンとの間隔を狭くする）
        _navTitle = new Border
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
            Margin = new Thickness(-8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = AppIdentity.ProductName,
                FontSize = AppStyles.NavTitleFontSize,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,

                // ペインの幅からはみ出さない（はみ出すとページのツールバーへ重なる）
                MaxWidth = AppStyles.NavPanelExpandedWidth - WindowLayout.AppIconColumnWidth + 4,
            },
            Visibility = Services.Main.IsNavigationExpanded ? Visibility.Visible : Visibility.Collapsed,
        };

        var titleRow = new Grid { Height = WindowLayout.AppIconRowHeight };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(WindowLayout.AppIconColumnWidth) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(appIconButton, 0);
        titleRow.Children.Add(appIconButton);
        Grid.SetColumn(_navTitle, 1);
        titleRow.Children.Add(_navTitle);

        var header = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
        };
        header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(WindowLayout.AppIconRowHeight) });
        header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(WindowLayout.NavToggleHeight) });
        header.Children.Add(titleRow);
        Grid.SetRow(_navToggle, 1);
        header.Children.Add(_navToggle);

        // 見出しの下地はナビゲーションと同じ色にする（展開時はアプリ名の分だけ広げる）
        _navHeader = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = NavPanelMetrics.Width(Services.Main.IsNavigationExpanded),
            Height = WindowLayout.AppIconRowHeight + WindowLayout.NavToggleHeight,
            Background = new SolidColorBrush(AppStyles.ToColor(AppPalette.NavPanel(AppStyles.IsDarkTheme))),
            Child = header,
        };
        return _navHeader;
    }

    /// <summary>テーマに依存する色（下地）を現在のテーマで塗り直す。</summary>
    /// <remarks>
    /// ルートの下地を塗らないとウィンドウの背景（Mica）が透け、暗色テーマでも明るい色が見える。<br/>
    /// ナビゲーション ペインの背景は <see cref="BuildNavigation"/> が ThemeDictionaries で与える<br/>
    /// （テーマ変更で自動的に解決し直される）。
    /// </remarks>
    private void ApplyThemeResources()
    {
        _root.Background = new SolidColorBrush(AppStyles.ToColor(AppPalette.Page(AppStyles.IsDarkTheme)));

        // 自前で描くナビゲーションの下地も同じ色へ揃える
        if (_navHeader is not null)
        {
            var navPanel = new SolidColorBrush(AppStyles.ToColor(AppPalette.NavPanel(AppStyles.IsDarkTheme)));
            _navHeader.Background = navPanel;
            _navOverlay.Background = navPanel;
            _navRailBackground.Background = navPanel;
            RefreshNavSelection();
        }
    }

    /// <summary>タイトル バー相当の帯のダブルクリックで最大化と元のサイズを切り替える。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">タップの情報。</param>
    private void OnTitleBarDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.GetPosition(this.Content).Y > AppStyles.TitleBarHeight)
        {
            return;
        }

        ToggleMaximize();
        e.Handled = true;
    }

    /// <summary>タイトル バー相当の帯の押下でウィンドウの移動を開始する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// 押下位置がタイトル バーの高さ以内のときだけ移動を開始する（ページの操作を妨げない）。<br/>
    /// ボタンなどが押下を処理した場合（<c>Handled</c> が true）は開始しない。<br/>
    /// <b>移動を自前で行う理由</b>: WinUI 3 には移動を開始する API が無く、非クライアント領域の押下を<br/>
    /// 送り直す方法は XAML のマウス捕捉に邪魔されて効かない（実測）。 ポインターの移動量から<br/>
    /// <see cref="AppWindow.Move"/> で位置を更新すれば、捕捉の状態に関係なく確実に追従する。
    /// </remarks>
    private void OnTitleBarPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Handled || sender is not UIElement element ||
            !e.GetCurrentPoint(this.Content).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetCurrentPoint(this.Content).Position;
        if (position.Y > AppStyles.TitleBarHeight)
        {
            return;
        }

        _dragStartPointer = position;
        _dragStartWindowPosition = AppWindow.Position;
        _isDragging = element.CapturePointer(e.Pointer);
        e.Handled = _isDragging;
    }

    /// <summary>ドラッグ中のポインター移動に合わせてウィンドウを移動する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの情報。</param>
    private void OnTitleBarPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = e.GetCurrentPoint(this.Content).Position;
        var scaling = RasterizationScale;
        var dx = (int)Math.Round((position.X - _dragStartPointer.X) * scaling);
        var dy = (int)Math.Round((position.Y - _dragStartPointer.Y) * scaling);
        AppWindow.Move(new PointInt32(_dragStartWindowPosition.X + dx, _dragStartWindowPosition.Y + dy));
        e.Handled = true;
    }

    /// <summary>ドラッグを終了する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの情報。</param>
    private void OnTitleBarPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        e.Handled = true;
    }

    /// <summary>捕捉が失われたときにドラッグを終了する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの情報。</param>
    private void OnTitleBarPointerCaptureLost(object sender, PointerRoutedEventArgs e) => _isDragging = false;

    /// <summary>最大化と元のサイズに戻すを切り替える。</summary>
    private void ToggleMaximize()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (presenter.State == OverlappedPresenterState.Maximized)
        {
            presenter.Restore();
        }
        else
        {
            presenter.Maximize();
        }
    }

    /// <summary>タイトル バーのボタン（背景なし・角なし）を生成する。</summary>
    /// <param name="width">幅（DIP）。</param>
    /// <returns>生成したボタン。</returns>
    private static Button CreateCaptionButton(double width)
        => new()
        {
            Width = width,
            Height = AppStyles.TitleBarHeight,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

    /// <summary>ナビゲーションの項目（収納時のアイコン列）を組み立てる。</summary>
    /// <remarks>
    /// <b>NavigationView を使わない理由</b>: ペインの展開は窓の幅・DPI・テーマによってラベルが出ない、<br/>
    /// 内容が上端から始まらないなど表示が安定しない（実測）。 4 つの UI 版で同じ見た目にするため、<br/>
    /// 収納時はアイコン列、展開時は一覧を自前で組み立てる（位置は <see cref="WindowLayout"/> と<br/>
    /// <see cref="NavPanelMetrics"/>（App.Core）が単一所有する）。
    /// </remarks>
    private void BuildNavigation()
    {
        // 見出し（アプリ アイコン 44 + 開閉ボタン 40）の下から並べる（他の UI 版と同じ間隔）
        _navRail.VerticalAlignment = VerticalAlignment.Top;
        _navRail.Margin = new Thickness(
            0,
            WindowLayout.AppIconRowHeight + WindowLayout.NavToggleHeight + AppStyles.NavItemsMarginTop,
            0,
            AppStyles.NavItemsMarginTop);

        foreach (var entry in s_entries)
        {
            var item = CreateNavRailItem(entry);
            _navRailItems.Add(item);
            _navRail.Children.Add(item.Button);
        }

        // テーマが変わったらラスタライズ済みのアイコンと下地を描き直す（色は画像へ焼き込まれている）
        AppStyles.ThemeChanged += (_, _) =>
        {
            RefreshNavigationIcons();
            RefreshNavSelection();
        };
    }

    /// <summary>収納時のナビゲーションの項目を 1 つ組み立てる。</summary>
    /// <param name="entry">項目の定義。</param>
    /// <returns>項目の見た目（選択状態の反映に使う要素を含む）。</returns>
    private NavItemVisual CreateNavRailItem(NavEntry entry)
    {
        var (mark, icon) = CreateNavMarkAndIcon(entry);

        var content = new Grid();
        content.Children.Add(mark);
        content.Children.Add(icon);

        var button = CreateNavItemButton(content);
        button.Click += (_, _) =>
        {
            SelectPage(entry.Page, force: false);
            CollapseNavigation();
        };

        return new NavItemVisual(entry.Page, button, mark, icon);
    }

    /// <summary>ナビゲーションの項目の選択マークとアイコンを組み立てる。</summary>
    /// <param name="entry">項目の定義。</param>
    /// <returns>選択マーク（左端）とアイコン（中央）。</returns>
    private static (Border Mark, Image Icon) CreateNavMarkAndIcon(NavEntry entry)
    {
        var mark = new Border
        {
            Width = AppStyles.NavSelectionMarkWidth,
            Height = NavPanelMetrics.IconSize,
            CornerRadius = new CornerRadius(AppStyles.NavSelectionMarkWidth / 2),
            Margin = new Thickness(AppStyles.NavSelectionMarkLeft, 0, 0, 0),
            Background = new SolidColorBrush(
                AppStyles.ToColor(AppPalette.NavItemSelectionAccent(AppStyles.IsDarkTheme))),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        var icon = new Image
        {
            Width = NavPanelMetrics.IconSize,
            Height = NavPanelMetrics.IconSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = IconLoader.LoadThemedImage(entry.Icon, AppStyles.IconRasterSize),
        };

        return (mark, icon);
    }

    /// <summary>ナビゲーションの項目のボタンを組み立てる。</summary>
    /// <param name="content">ボタンの内容。</param>
    /// <returns>項目の高さ・余白を他の UI 版と揃えたボタン。</returns>
    private static Button CreateNavItemButton(UIElement content)
        => new()
        {
            Content = content,
            Height = AppStyles.NavItemHeight,
            Margin = new Thickness(2, 1, 2, 1),
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

    /// <summary>コアの ViewModel と設定の変更通知を購読する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    /// <param name="settings">設定サービス。</param>
    private void WireViewModel(MainViewModel viewModel, ISettingsService settings)
    {
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.IsNavigationExpanded), StringComparison.Ordinal))
            {
                ApplyNavigationExpansion(viewModel.IsNavigationExpanded);
            }
        };

        settings.Current.PropertyChanged += (_, e) =>
        {
            if (!string.Equals(e.PropertyName, nameof(Settings.ThemeMode), StringComparison.Ordinal))
            {
                return;
            }

            // テーマはウィンドウ全体（ルート）へ適用する。 ナビゲーションだけに適用すると
            // ツールバー・ステータス バー・ページが明色のまま残り、暗色テーマで色が混ざる（実測）
            AppStyles.ApplyTheme(Content as FrameworkElement, settings.Current.ThemeMode);
            ApplyThemeResources();
        };
    }

    /// <summary>ナビ領域の外の押下でナビゲーションを閉じる。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// ナビゲーション ペインはページ領域の左端へ重ねて表示する（幅は <c>OpenPaneLength</c>）ため、<br/>
    /// 押下位置の X がその幅以上ならナビ領域の外である。 押下自体は処理しない（背後の要素の操作を妨げない）。
    /// </remarks>
    private void OnNavigationOutsidePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!Services.Main.IsNavigationExpanded)
        {
            return;
        }

        // ナビ領域は「タイトル バーの下・ペインの幅の内側」である。 その外（タイトル バーを含む）は閉じる
        var point = e.GetCurrentPoint(_root).Position;
        if (point.X < AppStyles.NavPanelExpandedWidth)
        {
            return;
        }

        CollapseNavigation();
    }

    /// <summary>展開時のナビゲーション（ページ領域の上へ重ねる一覧）を組み立てる。</summary>
    /// <returns>見出しの下からページ領域の上へ重ねる要素。</returns>
    /// <remarks>
    /// <b>NavigationView のペインを使わない理由</b>: ペインの展開は窓の幅・DPI・テーマによって<br/>
    /// ラベルが出ない、幅が収納時のままになるなど表示が安定しない（実測）。 4 つの UI 版で<br/>
    /// 同じ見た目にするため、展開時の一覧は自前で組み立てる（収納時はアイコン列を使う）。
    /// </remarks>
    private FrameworkElement BuildNavigationOverlay()
    {
        var items = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var entry in s_entries)
        {
            var item = CreateNavOverlayItem(entry);
            _navOverlayItems.Add(item);
            items.Children.Add(item.Button);
        }

        // フッター: UI 名と実際のバージョン（他の UI 版と同じ位置・書式）
        _navFooter = new TextBlock
        {
            Text = AppVersion.For("WinUI3"),
            FontSize = AppStyles.NavVersionFontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var grid = new Grid();
        grid.Children.Add(items);
        grid.Children.Add(_navFooter);

        _navOverlay = new Border
        {
            Width = NavPanelMetrics.ExpandedWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(
                0, WindowLayout.AppIconRowHeight + WindowLayout.NavToggleHeight, 0, 0),
            Background = new SolidColorBrush(AppStyles.ToColor(AppPalette.NavPanel(AppStyles.IsDarkTheme))),
            Visibility = Services.Main.IsNavigationExpanded ? Visibility.Visible : Visibility.Collapsed,
            Child = grid,
        };
        return _navOverlay;
    }

    /// <summary>展開時のナビゲーションの項目を 1 つ組み立てる。</summary>
    /// <param name="entry">項目の定義。</param>
    /// <returns>項目の見た目（選択状態の反映に使う要素を含む）。</returns>
    private NavItemVisual CreateNavOverlayItem(NavEntry entry)
    {
        var (mark, icon) = CreateNavMarkAndIcon(entry);

        // 一覧のアイコンは他の UI 版と同じ 12 DIP（マーク 4 + 幅 3 + 間隔 5）の位置へ置く
        mark.Margin = new Thickness(AppStyles.NavSelectionMarkLeft, 0, 5, 0);
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        icon.Margin = new Thickness(0);

        var label = new TextBlock
        {
            Text = entry.Title,
            FontSize = AppStyles.NavItemFontSize,
            Margin = new Thickness(NavPanelMetrics.IconSpacing, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Children.Add(mark);
        content.Children.Add(icon);
        content.Children.Add(label);

        var button = CreateNavItemButton(content);
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Click += (_, _) =>
        {
            SelectPage(entry.Page, force: false);
            CollapseNavigation();
        };

        return new NavItemVisual(entry.Page, button, mark, icon);
    }

    /// <summary>ナビゲーションの開閉状態を画面へ反映する。</summary>
    /// <param name="expanded">開いているかどうか。</param>
    private void ApplyNavigationExpansion(bool expanded)
    {
        _navOverlay.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _navTitle.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _navHeader.Width = NavPanelMetrics.Width(expanded);
        _navToggleIcon.Source = IconLoader.LoadThemedImage(
            NavToggleIconFile(expanded), AppStyles.IconRasterSize);
        RefreshNavSelection();
    }

    /// <summary>ナビゲーションの選択状態（収納時のアイコン列と展開時の一覧）を現在のページへ合わせる。</summary>
    private void RefreshNavSelection()
    {
        var isDark = AppStyles.IsDarkTheme;
        var selectedBrush = new SolidColorBrush(AppStyles.ToColor(AppPalette.NavItemSelected(isDark)));
        var transparent = new SolidColorBrush(Colors.Transparent);
        var accent = new SolidColorBrush(AppStyles.ToColor(AppPalette.NavItemSelectionAccent(isDark)));
        foreach (var item in _navRailItems.Concat(_navOverlayItems))
        {
            var isCurrent = item.Page == _currentPage;
            item.Button.Background = isCurrent ? selectedBrush : transparent;
            item.Mark.Background = accent;
            item.Mark.Visibility = isCurrent ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>ナビゲーションの開閉状態に応じたアイコンのファイル名を取得する。</summary>
    /// <param name="expanded">開いているかどうか。</param>
    /// <returns>開いているときは「閉じる」、閉じているときは「開く」のアイコン。</returns>
    private static string NavToggleIconFile(bool expanded)
        => expanded
            ? "ic_fluent_panel_left_contract_24_filled.svg"
            : "ic_fluent_panel_right_contract_24_filled.svg";

    /// <summary>左上のアプリ アイコンの押下でウィンドウの標準メニューを出す。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <remarks>項目と並びは <see cref="WindowMenu"/>（App.Core）が単一所有する。</remarks>
    private void OnAppIconClick(object sender)
    {
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        var isMaximized = presenter?.State == OverlappedPresenterState.Maximized;
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        foreach (var entry in WindowMenu.Items(isMaximized))
        {
            if (entry.IsSeparatorBefore)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            var item = new MenuFlyoutItem { Text = entry.Label, IsEnabled = entry.IsEnabled };
            var action = entry.Action;
            item.Click += (_, _) => ExecuteWindowMenuAction(action);
            flyout.Items.Add(item);
        }

        flyout.ShowAt((FrameworkElement)sender);
    }

    /// <summary>左上のアプリ アイコンの右クリックでウィンドウの標準メニューを出す。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">タップの情報。</param>
    /// <remarks>左クリックはナビゲーションの開閉に使うため、メニューは右クリックで出す。</remarks>
    private void OnAppIconRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        OnAppIconClick(sender);
    }

    /// <summary>ウィンドウの標準メニューの項目を実行する。</summary>
    /// <param name="action">実行する操作。</param>
    private void ExecuteWindowMenuAction(WindowMenuAction action)
    {
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        switch (action)
        {
            case WindowMenuAction.Restore:
                presenter?.Restore();
                break;
            case WindowMenuAction.Move:
                SendSystemCommand(SystemWindowCommands.BeginMove);
                break;
            case WindowMenuAction.Size:
                SendSystemCommand(SystemWindowCommands.BeginSize);
                break;
            case WindowMenuAction.Minimize:
                presenter?.Minimize();
                break;
            case WindowMenuAction.Maximize:
                presenter?.Maximize();
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
        if (this.ToEsPlatformHandle() is { IsValid: true } handle)
        {
            _ = command(handle);
        }
    }

    /// <summary>展開中のナビゲーションを閉じる。</summary>
    /// <remarks>設定への保存は <see cref="MainViewModel.IsNavigationExpanded"/> の購読が行う。</remarks>
    private void CollapseNavigation()
    {
        if (Services.Main.IsNavigationExpanded)
        {
            Services.Main.IsNavigationExpanded = false;
        }
    }

    /// <summary>指定されたページを表示する。</summary>
    /// <param name="page">表示するページ。</param>
    /// <param name="force">true の場合、同じページでも表示処理を行い直す（起動時の初回表示）。</param>
    private void SelectPage(AppPage page, bool force)
    {
        if (!force && page == _currentPage)
        {
            return;
        }

        // 表示から外れるページへ通知する（背景の処理を低負荷へ落とす）
        CurrentPageContent?.OnDeactivated?.Invoke();

        var index = Array.FindIndex(s_entries, entry => entry.Page == page);
        if (index < 0)
        {
            return;
        }

        var content = EnsurePage(index);
        _currentPage = page;
        _contentHost.Children.Clear();
        _contentHost.Children.Add(content.Content);
        content.OnActivated?.Invoke();

        // ナビの選択状態を合わせる（プログラムからの切り替えでも選択がずれないようにする）
        RefreshNavSelection();
    }

    /// <summary>ページ固有のキー処理を表示中のページへ転送する。</summary>
    /// <param name="sender">イベントの送信元（ナビゲーション）。</param>
    /// <param name="args">キーの情報。</param>
    /// <remarks>ページが処理しない場合は後続（他のハンドラーと既定の動作）へ伝える。</remarks>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!args.Handled)
        {
            CurrentPageContent?.OnKeyDown?.Invoke(args);
        }
    }

    /// <summary>ナビゲーションのアイコンを現在のテーマの色で描き直す。</summary>
    private void RefreshNavigationIcons()
    {
        foreach (var item in _navRailItems.Concat(_navOverlayItems))
        {
            var index = Array.FindIndex(s_entries, e => e.Page == item.Page);
            if (index >= 0)
            {
                item.Icon.Source = IconLoader.LoadThemedImage(s_entries[index].Icon, AppStyles.IconRasterSize);
            }
        }
    }

    /// <summary>初回の表示時にウィンドウ配置の反映とホットキーの登録を行う。</summary>
    /// <param name="sender">イベントの送信元（本ウィンドウ）。</param>
    /// <param name="args">ウィンドウの有効化の情報。</param>
    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        // 非アクティブ（他のウィンドウへ切り替え）では閉じない。 実測では表示直後にも通知が来て、
        // 設定が「展開」でも収納のまま起動してしまう。 閉じるのはナビ領域の外の操作と項目の選択だけにする
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        if (_isActivated)
        {
            return;
        }

        _isActivated = true;

        // ウィンドウ ハンドルは生成時に確定するが、ウィンドウ プロシージャの差し替えは
        // 表示後に行う（表示前に差し替えると WinUI 側の設定に上書きされ得る）
        var handle = this.ToEsPlatformHandle();
        if (_hotKeys.Initialize(handle))
        {
            var viewModel = Services.Main;
            _hotKeys.Subscribe(type =>
            {
                // ツールバーのボタンと同じ経路へ一本化する（開始処理を二重所有しない）
                if (viewModel.StartSelectionMode is { } start)
                {
                    start(type);
                }
            });

            _hotKeys.RegisterAll(Services.Settings.Current);
        }

        // 要素ツリーが既に実体化している場合（Loaded が先に発火した場合）に備えて、ここでも確認する
        TryWarmUpPages();
    }

    /// <summary>先読みを一度だけ開始する。</summary>
    /// <remarks>
    /// 発火の経路が 2 つ（要素ツリーの実体化と初回の表示）あるため、開始の判定を本メソッドへ集約する。
    /// </remarks>
    private void TryWarmUpPages()
    {
        if (_isWarmUpStarted)
        {
            return;
        }

        _isWarmUpStarted = true;
        WarmUpPages();
    }

    /// <summary>ページの内容を生成して保持する（表示はしない）。</summary>
    /// <param name="index">ページの位置。</param>
    /// <returns>生成済み（または既存）のページの内容。</returns>
    private PageContent EnsurePage(int index) => _pages[index] ??= s_entries[index].Build(this);

    /// <summary>一覧・設定ページを表示せずに構築し、サムネイルの低負荷生成を始める。</summary>
    /// <remarks>
    /// 最初の切り替えで生成済みのサムネイルが並ぶようにする（キャプチャ ページの操作は妨げない）。<br/>
    /// <b>1 ページずつ低優先度のキューで作る</b>: まとめて作ると、その間 UI スレッドが塞がれて<br/>
    /// キャプチャ ページの操作が引っかかる（実測）。<br/>
    /// 先読みの失敗はアプリの動作を妨げない（ページを開いたときに改めて構築される）。
    /// </remarks>
    private void WarmUpPages()
    {
        var pending = new Queue<AppPage>([AppPage.FolderView, AppPage.Undo, AppPage.Settings]);
        ScheduleNext();

        void ScheduleNext()
        {
            if (pending.Count == 0 || DispatcherQueue is null)
            {
                return;
            }

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Step);
        }

        void Step()
        {
            try
            {
                var page = pending.Dequeue();
                var index = Array.FindIndex(s_entries, entry => entry.Page == page);
                if (index >= 0)
                {
                    EnsurePage(index).OnWarmUp?.Invoke();
                }
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                s_logger.LogThumbnailGenerateError(ex);
            }

            ScheduleNext();
        }
    }

    /// <summary>終了時にウィンドウ配置とナビの開閉を保存し、ホットキーを解除する。</summary>
    /// <param name="sender">イベントの送信元（本ウィンドウ）。</param>
    /// <param name="args">終了の情報。</param>
    /// <remarks>保存の失敗は終了を妨げない（原因だけをログへ残す）。</remarks>
    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _hotKeys.Dispose();

        var settings = Services.Settings;
        settings.Current.IsNavigationExpanded = Services.Main.IsNavigationExpanded;
        WindowPlacementStore.Save(this, settings);
    }
}

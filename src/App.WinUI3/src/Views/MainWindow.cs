using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Icons;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using EsUtil.SaveEvidence.Platform.WinUI3;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    /// <summary>アプリ名（開閉ボタンのすぐ右。 開閉に追従して表示を切り替える）。</summary>
    private Border _navTitle = null!;

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

    /// <summary>ナビゲーションの要素（メニュー項目と内容のホスト）。</summary>
    private readonly NavigationView _navigation = new();

    /// <summary>グローバル ホットキーの登録。</summary>
    private readonly GlobalHotKeyRegistration _hotKeys = new();

    /// <summary>現在表示中のページ。</summary>
    private AppPage _currentPage = AppPage.Capture;

    /// <summary>初回の表示処理（配置の反映とホットキーの登録）を済ませたかどうか。</summary>
    private bool _isActivated;

    /// <summary>先読みを開始したかどうか。</summary>
    private bool _isWarmUpStarted;

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

        // アプリ名はパネルの開閉状態に合わせる（購読設定前の初期値）
        _navTitle.Visibility = viewModel.IsNavigationExpanded ? Visibility.Visible : Visibility.Collapsed;

        // 上書き確認とクリップボードは UI 型を要求するため、UI 層の実装をコールバックへ配線する
        viewModel.ConfirmOverwriteAsync = fileName => ConfirmDialog.ShowAsync(
            _navigation.XamlRoot!, "上書きの確認", $"{fileName} は既に存在します。上書きしますか？");
        viewModel.CopyToClipboardCallback = image => Task.FromResult(CaptureClipboard.Copy(image));
        viewModel.PasteFromClipboardCallback = CaptureClipboard.PasteAsync;

        SelectPage(_currentPage, force: true);

        // ウィンドウ（タスク バー）アイコンを他の UI 版と同じアイコンにする。
        // WinUI 3 はアイコン ファイルのパスしか受け付けないため、共有 SVG から ICO を書き出して渡す
        if (WindowIconFile.Ensure() is { Length: > 0 } iconPath)
        {
            AppWindow.SetIcon(iconPath);
        }

        // 保存済みの位置・サイズ・表示状態を復元する（表示前でも AppWindow は操作できる）
        WindowPlacementStore.Restore(this, settings.Current);

        // ウィンドウの最小サイズを他の UI 版と同じにする（WinUI 3 の標準機能で制限する）
        ApplyMinimumSize();

        AppStyles.ApplyTheme(Content as FrameworkElement, settings.Current.ThemeMode);
        ApplyThemeResources();

        // OS のテーマが変わったとき（System のとき）も色を追随させる
        _navigation.ActualThemeChanged += (_, _) => ApplyThemeResources();
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;

        // 先読みは要素ツリーが実体化した後に行う（表示が済んでから一覧の構築を始める）
        _navigation.Loaded += (_, _) =>
        {
            // DPI が変わると物理ピクセルの最小サイズも変わる（読み込み後に倍率が確定する）
            if (_navigation.XamlRoot is { } root)
            {
                root.Changed += (_, _) => ApplyMinimumSize();
            }

            ApplyMinimumSize();
            HideStandardPaneToggle();
            TryWarmUpPages();
        };
    }

    /// <summary>標準のナビゲーション開閉ボタンを透明にして操作不可にする。</summary>
    /// <remarks>
    /// 標準のボタンはアイコンを差し替えられないため自前のボタンを使うが、<b>領域の確保</b>には必要である<br/>
    /// （収納時に項目が上へ詰まり、自前のボタンと重なる）。 そのため要素は残し、見た目と操作だけを無効化する。<br/>
    /// テンプレートの内部要素は名前で探す（ビジュアル ツリーは読み込み後に確定する）。
    /// </remarks>
    private void HideStandardPaneToggle()
    {
        if (FindDescendant(_navigation, "TogglePaneButton") is not Control toggle)
        {
            return;
        }

        toggle.Opacity = 0;
        toggle.IsHitTestVisible = false;
    }

    /// <summary>ビジュアル ツリーから名前で要素を探す。</summary>
    /// <param name="root">探索の起点。</param>
    /// <param name="name">探す要素の名前。</param>
    /// <returns>見つかった要素。 見つからない場合は null。</returns>
    private static FrameworkElement? FindDescendant(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            if (FindDescendant(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>最小サイズを現在の DPI で画面物理ピクセルへ換算して設定する。</summary>
    /// <remarks>
    /// <see cref="OverlappedPresenter.PreferredMinimumWidth"/> / <c>PreferredMinimumHeight</c> は<br/>
    /// 画面物理ピクセルで解釈されるため、他の UI 版と同じ DIP の見た目にするには DPI 倍率を掛ける<br/>
    /// （掛けないと高 DPI ほど最小サイズが他 UI 版より小さくなる。 実測 125%: 800 × 400 ピクセル = 640 × 320 DIP）。
    /// </remarks>
    private void ApplyMinimumSize()
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        var scaling = _navigation.XamlRoot?.RasterizationScale ?? 1.0;
        presenter.PreferredMinimumWidth = WindowPlacementRule.ToPhysicalLength(AppStyles.MinWindowWidth, scaling);
        presenter.PreferredMinimumHeight = WindowPlacementRule.ToPhysicalLength(AppStyles.MinWindowHeight, scaling);
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

        // 下: ナビゲーション（ページの内容。 ページの先頭行がツールバーになる）
        root.Children.Add(_navigation);

        // 上: ナビゲーションの開閉（左上・常に表示）。 他の UI 版と同じ SVG アイコンを使う
        _navToggle = CreateCaptionButton(AppStyles.NavPanelCompactWidth);
        _navToggle.Content = new ImageIcon
        {
            Source = IconLoader.LoadThemedImage("ic_fluent_arrow_autofit_width_dotted_24_regular.svg", AppStyles.IconRasterSize),
            Width = AppStyles.NavIconSize,
            Height = AppStyles.NavIconSize,
        };
        _navToggle.HorizontalAlignment = HorizontalAlignment.Left;
        _navToggle.VerticalAlignment = VerticalAlignment.Top;
        // アイコンもアプリ名と同じ中心（ツールバーの行の中心）へ揃える
        _navToggle.Margin = new Thickness(0, 7, 0, 0);
        ToolTipService.SetToolTip(_navToggle, "ナビゲーション パネルの開閉");
        _navToggle.Click += (_, _) => Services.Main.IsNavigationExpanded = !Services.Main.IsNavigationExpanded;
        root.Children.Add(_navToggle);

        // アプリ名は開閉ボタンのすぐ右へ置く（他の UI 版と同じ位置・縦位置はアイコンと揃える）。
        // NavigationView の PaneHeader は内部余白のためアイコンから離れるので使わない
        var navTitleText = new TextBlock
        {
            Text = "SaveEvidence",
            FontSize = AppStyles.NavTitleFontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _navTitle = new Border
        {
            Height = AppStyles.TitleBarHeight,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            // 開閉ボタン（幅 NavPanelCompactWidth）のアイコン右端へ寄せる（アイコンはボタン中央の 16 DIP）。
            // 上端は 7 DIP 下げる: ツールバーの行（上端 6 + 高さ 34 = 中心 23）と同じ中心になり、
            // アプリ名とファイル名テンプレートの文字のベースラインが揃う
            Margin = new Thickness(AppStyles.NavPanelCompactWidth - 6, 7, 0, 0),
            Child = navTitleText,
        };
        root.Children.Add(_navTitle);

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

        return root;
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
        var scaling = _navigation.XamlRoot?.RasterizationScale ?? 1.0;
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

    /// <summary>ナビゲーション ペインとメニュー項目を組み立てる。</summary>
    private void BuildNavigation()
    {
        _navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
        _navigation.IsSettingsVisible = false;
        _navigation.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        _navigation.IsTitleBarAutoPaddingEnabled = false;
        // 開閉ボタンは自前（左上へ重ねる）。 標準のボタンはアイコンを他 UI 版と揃えられない。
        // ただし標準のボタンは**領域の確保**に必要なので残す（収納時に項目が上へ詰まって
        // 自前のボタンと重なるのを防ぐ）。 標準のボタンは読み込み後に透明化して操作不可にする
        _navigation.IsPaneToggleButtonVisible = true;
        _navigation.OpenPaneLength = AppStyles.NavPanelExpandedWidth;
        _navigation.CompactPaneLength = AppStyles.NavPanelCompactWidth;
        _navigation.IsPaneOpen = Services.Main.IsNavigationExpanded;

        // ペインの見出し: PaneHeader は NavigationView の内部余白の影響で開閉ボタンから離れた位置に
        // 置かれる（実測: アイコンの右 100 DIP 以上）ため使わない。 アプリ名は BuildRoot が
        // 開閉ボタンのすぐ右へ重ねて置き、縦位置をアイコンと揃える。

        // ペインのフッター: UI 名と実際のバージョン（他の UI 版と同じ位置・書式）
        _navigation.PaneFooter = new TextBlock
        {
            Text = AppVersion.For("WinUI3"),
            FontSize = AppStyles.NavVersionFontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 8),
        };

        foreach (var entry in s_entries)
        {
            _navigation.MenuItems.Add(new NavigationViewItem
            {
                Content = entry.Title,
                Tag = entry.Page,
                // 文字サイズは 4 つの UI 版で共通（既定の 14 では他 UI より大きい）
                FontSize = AppStyles.NavItemFontSize,
                Icon = new ImageIcon { Source = IconLoader.LoadThemedImage(entry.Icon, AppStyles.IconRasterSize) },
            });
        }

        _navigation.SelectionFollowsFocus = NavigationViewSelectionFollowsFocus.Disabled;
        _navigation.ItemInvoked += OnNavigationItemInvoked;
        _navigation.KeyDown += OnRootKeyDown;

        // ナビゲーション ペインの背景をテーマ別に与える。
        // NavigationView のペイン背景はテーマ リソースから解決されるが、ルートの RequestedTheme を
        // 実行時に変えても追随しない（実測: ダークへ切り替えても明色のまま残る）。
        // ThemeDictionaries に入れると ThemeResource の解決がテーマ変更でやり直される。
        var paneResources = new ResourceDictionary();
        foreach (var (key, isDark) in new[] { ("Light", false), ("Dark", true), ("Default", false) })
        {
            var brush = new SolidColorBrush(AppStyles.ToColor(AppPalette.NavPanel(isDark)));
            paneResources.ThemeDictionaries[key] = new ResourceDictionary
            {
                ["NavigationViewDefaultPaneBackground"] = brush,
                ["NavigationViewExpandedPaneBackground"] = brush,
            };
        }

        _navigation.Resources.MergedDictionaries.Add(paneResources);

        // テーマが変わったらラスタライズ済みのアイコンを描き直す（色は画像へ焼き込まれている）
        AppStyles.ThemeChanged += (_, _) => RefreshNavigationIcons();
    }

    /// <summary>コアの ViewModel と設定の変更通知を購読する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    /// <param name="settings">設定サービス。</param>
    private void WireViewModel(MainViewModel viewModel, ISettingsService settings)
    {
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.IsNavigationExpanded), StringComparison.Ordinal))
            {
                _navigation.IsPaneOpen = viewModel.IsNavigationExpanded;
                // アプリ名はパネルを収納したら隠す（アイコンだけの幅に収まらないため）
                _navTitle.Visibility = viewModel.IsNavigationExpanded
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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

    /// <summary>ナビゲーション項目が選ばれたときにページを切り替える。</summary>
    /// <param name="sender">イベントの送信元（ナビゲーション）。</param>
    /// <param name="args">選択された項目の情報。</param>
    private void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is AppPage page)
        {
            SelectPage(page, force: false);
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
        _navigation.Content = content.Content;
        content.OnActivated?.Invoke();

        // ナビの選択状態を合わせる（プログラムからの切り替えでも選択がずれないようにする）
        foreach (var item in _navigation.MenuItems)
        {
            if (item is NavigationViewItem { Tag: AppPage tagged } && tagged == page)
            {
                _navigation.SelectedItem = item;
                break;
            }
        }
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
        foreach (var item in _navigation.MenuItems)
        {
            if (item is not NavigationViewItem { Icon: ImageIcon icon } entry)
            {
                continue;
            }

            var entryIndex = Array.FindIndex(s_entries, e => e.Page is AppPage page && page == (AppPage)entry.Tag);
            if (entryIndex >= 0)
            {
                icon.Source = IconLoader.LoadThemedImage(s_entries[entryIndex].Icon, AppStyles.IconRasterSize);
            }
        }
    }

    /// <summary>初回の表示時にウィンドウ配置の反映とホットキーの登録を行う。</summary>
    /// <param name="sender">イベントの送信元（本ウィンドウ）。</param>
    /// <param name="args">ウィンドウの有効化の情報。</param>
    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
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

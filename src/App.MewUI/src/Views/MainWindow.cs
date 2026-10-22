using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.MewUI.Icons;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using EsUtil.Ui.MewUI.Interop;
using Microsoft.Extensions.Logging;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>アプリのメインウィンドウ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>MainWindow.axaml</c> に相当するが、MewUI は XAML を持たないため C# マークアップで組む。<br/>
/// <br/>
/// <b>構成</b>: 左のナビパネル（アイコン + ページ名）と右のページ内容。<br/>
/// Avalonia 版と同じ見た目にするため、<c>NavigationView</c> は使わず<b>自前の合成</b>で組む<br/>
/// （ナビパネルの余白・区切り線・アイコンの有無を Avalonia 版と同じに制御するため）。<br/>
/// <b>VM 連携</b>: <c>App.Core</c> の <see cref="MainViewModel"/>（<c>INotifyPropertyChanged</c>）を<br/>
/// そのままバインドする（ラッパー不要）。<br/>
/// <b>タイトルバー</b>: 非クライアント領域をクライアント領域へ拡張し（<c>ExtendClientAreaTitleBarHeight</c>）、<br/>
/// ナビパネル上部をドラッグ領域として使う（Avalonia 版の <c>ExtendClientAreaToDecorationsHint</c> と同じ）。<br/>
/// <b>ツールヒント</b>: クリックできるボタンは <see cref="ClickableToolTip"/> で登録する<br/>
/// （MewUI の不具合でクリックが無効になるのを防ぐ）。<br/>
/// <b>設定の永続化</b>: 起動時にウィンドウ位置・サイズ・状態を復元し、終了時（<c>Closed</c>）に保存する。<br/>
/// MewUI のウィンドウ位置・サイズは DIP、設定は画面物理ピクセルのため、変換は <see cref="EsWindowPlacementMath"/> が単一所有する。
/// </remarks>
internal sealed partial class MainWindow
{
    /// <summary>ロガー（先読みの失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(MainWindow));

    /// <summary>復元するサイズが設定されていない場合の既定ウィンドウサイズ（DIP）。</summary>
    /// <remarks>値は <see cref="WindowMetrics"/>（App.Core）が 4 つの UI 版で共通に単一所有する。</remarks>
    private static readonly Size s_defaultWindowSize = new(WindowMetrics.DefaultWidth, WindowMetrics.DefaultHeight);

    /// <summary>ウィンドウ枠を含む最小外寸（DIP）。</summary>
    /// <remarks>値は <see cref="WindowMetrics"/>（App.Core）が 4 つの UI 版で共通に単一所有する。</remarks>
    private static readonly Size s_minWindowSize = new(WindowMetrics.MinimumWidth, WindowMetrics.MinimumHeight);

    /// <summary>タイトルバー領域の高さ（DIP）。 非クライアント領域をこの高さでクライアント領域へ拡張する。</summary>
    private const double TitleBarHeight = 32;

    /// <summary>ナビ領域の一番上に置く開閉ボタンの一辺（DIP）。</summary>
    private const double NavToggleSize = 40;

    /// <summary>ナビの開閉アイコン（開いているときに使う「閉じる」）。</summary>
    private const string NavIconPanelLeftContract = "ic_fluent_panel_left_contract_24_filled.svg";

    /// <summary>ナビの開閉アイコン（閉じているときに使う「開く」）。</summary>
    private const string NavIconPanelRightContract = "ic_fluent_panel_right_contract_24_filled.svg";

    /// <summary>ナビ項目の選択マークの幅（DIP）。 WinUI 3 の選択マークと同じ。</summary>
    private const double NavSelectionMarkWidth = NavPanelMetrics.SelectionMarkWidth;

    /// <summary>ナビ項目の選択マークの高さ（DIP）。 WinUI 3 の選択マークと同じ。</summary>
    private const double NavSelectionMarkHeight = 16;

    /// <summary>ナビ項目のアイコンの一辺（DIP）。</summary>
    private const double NavIconSize = NavPanelMetrics.IconSize;

    /// <summary>ナビ項目の高さ（DIP）。 開閉の状態で変わらない。</summary>
    /// <remarks>
    /// AvaloniaUI 版の項目（アイコンの領域 24 + 上下の余白 8 + 8 = 40）と同じ高さにする。<br/>
    /// 文字の表示を切り替えると内容の高さが変わり、開閉で項目の高さが変わってしまうため固定する。
    /// </remarks>
    private const double NavItemHeight = NavPanelMetrics.ItemHeight;

    /// <summary>ナビ項目のアイコンを生成する解像度（ピクセル）。</summary>
    /// <remarks>表示サイズ（16 DIP）に対して高 DPI でも鮮明になるよう余裕を持たせる。</remarks>
    private const int NavIconRasterSize = 32;

    /// <summary>ナビゲーションページの定義（表示名・内容の生成・アイコン）。</summary>
    /// <param name="page">対応する <see cref="AppPage"/>。</param>
    /// <param name="title">ナビに表示する名前。</param>
    /// <param name="icon">ナビに表示するアイコンの SVG ファイル名。</param>
    /// <param name="content">内容を生成する関数。</param>
    /// <remarks>
    /// <see cref="Array.IndexOf{T}(T[], T) "/> でインデックスを引くため、レコードではなくクラスとし参照の同一性で比較する。<br/>
    /// 値の等価性で比較すると、同じ型・名前のエントリを追加した場合に取り違える。
    /// </remarks>
    private sealed class NavEntry(AppPage page, string title, string icon, Func<PageContent> content)
    {
        /// <summary>対応する <see cref="AppPage"/>。</summary>
        public AppPage Page { get; } = page;

        /// <summary>ナビに表示する名前。</summary>
        public string Title { get; } = title;

        /// <summary>ナビに表示するアイコンの SVG ファイル名。</summary>
        public string Icon { get; } = icon;

        /// <summary>内容を生成する関数。</summary>
        public Func<PageContent> Content { get; } = content;
    }

    /// <summary>メインウィンドウを生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>生成したウィンドウ（<c>Application.Run</c> へ渡す）。</returns>
    /// <remarks>
    /// 1. ナビパネル（自前の合成）を組み、ページ内容は選択時に生成する（起動を軽くする）。<br/>
    /// 2. 生成した内容は保持し、以降の選択では同じ要素を表示する（ページの状態と購読を保つ）。<br/>
    /// 3. 保存済みのウィンドウ位置・サイズ・状態を復元する。<br/>
    /// 4. ナビパネルの開閉を設定から反映する。<br/>
    /// 5. 終了時にウィンドウ配置とナビパネルの開閉を保存する。
    /// </remarks>
    public static Window Create(AppServices services)
    {
        var settingsService = services.Settings;
        var viewModel = services.Main;

        // ナビパネルの開閉は設定から復元する（変更は下の購読が設定へ保存する）。
        // 購読より前に設定するため、復元による保存（無駄な書き込み）は起きない。
        viewModel.IsNavigationExpanded = settingsService.Current.IsNavigationExpanded;

        // ページ内容は選択時に初めて生成する（起動を軽くする）
        NavEntry[] entries =
        [
            new(AppPage.Capture, "キャプチャ", "ic_fluent_scan_table_24_regular.svg", () => CapturePage.Build(services)),
            new(AppPage.FolderView, "フォルダビュー", "ic_fluent_folder_search_24_regular.svg", () => FolderViewPage.Build(services)),
            new(AppPage.Undo, "Undo 一覧", "ic_fluent_arrow_undo_24_regular.svg", () => UndoPage.Build(services.Main, settingsService, services.UndoHistory)),
            new(AppPage.Settings, "設定", "ic_fluent_settings_24_regular.svg", () => SettingsPage.Build(services)),
        ];

        var pages = new PageContent?[entries.Length];
        var activators = new Action?[entries.Length];
        var warmUps = new Action?[entries.Length];

        // 現在選択中のページ（テーマ変更時の再適用で使う）
        var selectedIndex = 0;

        // ページ内容の入れ替え先（各ページは生成時に追加し、選択に応じて表示を切り替える）
        var contentHost = new Grid().Columns("*").Rows("*");

        // ウィンドウは先に生成する（ナビパネル上部のドラッグ領域が本ウィンドウを参照するため）。
        // ページ内容は後から設定する。
        // 枠寸法を測定し、外寸の最小値から差し引いたクライアント最小寸法を設定する。
        var window = new Window()
            .Resizable(
                s_defaultWindowSize.Width, s_defaultWindowSize.Height,
                minWidth: s_minWindowSize.Width, minHeight: s_minWindowSize.Height);

        // ホバー・押下で変化する見た目はスタイルで与える（ローカル値で背景を設定すると状態変化が効かなくなる）
        window.StyleSheet = AppStyles.Create();

        // ページ（ウィンドウ）の背景は Avalonia 版と同じ色へ揃える（明色は白・暗色は黒）
        window.Background = AppStyles.CurrentPageBackground;

        // ナビパネルの要素（開閉に応じて幅・文字の表示を切り替える）
        var navPanel = new Border();

        // ウィンドウ メニュー（アプリ アイコンの押下で出す）
        var windowMenu = new Border
        {
            Child = new StackPanel().Vertical().Spacing(0).Padding(new Thickness(4)),
            IsVisible = false,
            StyleName = AppStyles.MenuSurface,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8, WindowLayout.AppIconRowHeight, 0, 0),
        };

        // ナビが開いている間だけ表示する覆い（ナビ領域の外のクリックで閉じる）。
        // MewUI のイベントは親へ伝播しないため、外側のクリックを拾うには専用の面が要る
        var navDismissLayer = new Border
        {
            Background = MewUIColor.Transparent,
            IsVisible = viewModel.IsNavigationExpanded,
        };
        navDismissLayer.OnMouseDown(_ =>
        {
            CloseWindowMenu();
            CollapseNavigation();
        });

        // タイトル バーのアプリ名（アイコンの右。 開閉状態に依存しない）
        var navTitle = new TextBlock().Text("SaveEvidence").FontSize(14);
        navTitle.FontWeight = FontWeight.Bold;
        // フッターの UI 名とバージョン（下で生成するが、ApplyNavigationLayout から参照するため先に宣言する）
        TextBlock navVersionText;
        var navItemTexts = new TextBlock[entries.Length];
        var navItemImages = new Image[entries.Length];
        var navItemMarks = new Border[entries.Length];
        var navItemButtons = new Button[entries.Length];
        var navToggleIcon = new Image
        {
            Source = LoadNavIcon(NavIconPanelIcon(viewModel.IsNavigationExpanded)),
            Width = NavIconSize,
            Height = NavIconSize,
            VerticalAlignment = VerticalAlignment.Center
        };

        var toggle = new Button
        {
            Content = navToggleIcon.Center(),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Width = NavToggleSize,
            Height = NavToggleSize,
            StyleName = AppStyles.NavToggle,
        };
        toggle.Cursor(CursorType.Hand);
        toggle.WithToolTip("ナビゲーションを開閉", () => viewModel.IsNavigationExpanded = !viewModel.IsNavigationExpanded);

        var items = new StackPanel().Vertical().Spacing(0)
            .Margin(new Thickness(0, NavPanelMetrics.ItemsMarginTop, 0, NavPanelMetrics.ItemsMarginTop));

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var icon = new Image
            {
                Source = LoadNavIcon(entry.Icon),
                Width = NavIconSize,
                Height = NavIconSize,
                VerticalAlignment = VerticalAlignment.Center
            };
            navItemImages[i] = icon;

            var text = new TextBlock().Text(entry.Title);
            // 文字サイズは 4 つの UI 版で共通（NavPanelMetrics が単一所有する）
            text.FontSize(NavPanelMetrics.ItemFontSize);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.IsVisible = viewModel.IsNavigationExpanded;
            navItemTexts[i] = text;

            // 選択中を示す左端のマーク（既定は透明。 選択中だけアクセント色にする）
            var mark = new Border
            {
                Width = NavSelectionMarkWidth,
                Height = NavSelectionMarkHeight,
                CornerRadius = NavSelectionMarkWidth / 2,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = MewUIColor.Transparent,
            };
            navItemMarks[i] = mark;

            var content = new Grid().Columns("Auto,*");
            content.Add(mark.Column(0));
            content.Add(new StackPanel().Horizontal().Spacing(8).Children(icon, text).Column(1));

            // 内容の高さを固定する（文字の表示を切り替えても項目の高さが変わらないようにする）。
            // アイコンだけの収納時も、アイコン + 文字の展開時も同じ高さになる
            content.Height = NavItemHeight - 16; // 上下の余白（8 + 8）を除いた高さ

            var button = new Button
            {
                Content = content,
                // 左の余白は「マーク + 間隔」を含めてアイコンが展開時と同じ位置（12 DIP）に来る幅にする
                Padding = new Thickness(4, 8, 12, 8),
                Margin = new Thickness(2, 1, 2, 1),
                CornerRadius = 4,

                // 文字の表示を切り替えても高さを変えない（開閉で項目の高さが変わらないようにする）
                Height = NavItemHeight,
                MinHeight = NavItemHeight,
                StyleName = AppStyles.NavItem,
            };
            button.Cursor(CursorType.Hand);

            var index = i;
            button.WithToolTip(entry.Title, () =>
            {
                Select(index);
                CollapseNavigation();
            });
            navItemButtons[i] = button;
            items.Add(button);
        }

        // 左上のアプリ アイコン（タスク バーと同じアイコン。 押下でウィンドウ メニューを出す）
        var appIconImage = new Image
        {
            Source = IconLoader.LoadImage(AppIdentity.IconFileName, (int)WindowLayout.AppIconSize, IconColor()),
            Width = WindowLayout.AppIconSize,
            Height = WindowLayout.AppIconSize,
        };
        var appIconButton = new Button
        {
            Content = appIconImage.Center(),
            Width = WindowLayout.AppIconColumnWidth,
            Height = WindowLayout.AppIconRowHeight,
            Padding = new Thickness(0),
            CornerRadius = 0,
            StyleName = AppStyles.FlatButton,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        appIconButton.Cursor(CursorType.Hand);
        appIconButton.Background = MewUIColor.Transparent;
        appIconButton.ToolTip("ナビゲーションを開閉（右クリックでウィンドウ メニュー）");

        // 左クリックでナビを開閉し、右クリックでウィンドウ メニューを出す
        appIconButton.MouseDown += e =>
        {
            e.Handled = true;
            if (e.Button == MouseButton.Right)
            {
                ShowWindowMenu(appIconButton);
                return;
            }

            viewModel.IsNavigationExpanded = !viewModel.IsNavigationExpanded;
        };


        // ナビ領域の一番上はアプリ アイコンとその右のアプリ名（展開時のみ表示）。
        // その下に開閉ボタン（アイコンのみ）
        var titleRow = new Grid().Columns("Auto,*").Rows($"{WindowLayout.AppIconRowHeight}");
        titleRow.Add(appIconButton.Column(0));
        navTitle.VerticalAlignment = VerticalAlignment.Center;
        navTitle.Margin = new Thickness(-8, 0, 0, 0);
        titleRow.Add(navTitle.Column(1));

        var header = new Grid().Rows($"{WindowLayout.AppIconRowHeight},{WindowLayout.NavToggleHeight}");
        header.Add(titleRow.Row(0));
        header.Add(toggle.Row(1));

        // ナビパネルの背景はスタイルで与える（テーマ変更で自動的に再解決される）。
        // ページ領域の上へ重ねるため、幅だけを持たせて左端へ寄せる（残りの列はページ領域が使う）
        navPanel.StyleName = AppStyles.NavPanel;
        navPanel.Width = viewModel.NavPanelWidth;
        navPanel.HorizontalAlignment = HorizontalAlignment.Left;

        // フッター（UI 名と実際のバージョン。 収納時は非表示）。
        // 位置と書式は Avalonia 版と同じ（項目一覧の下・下端へ寄せ、中央寄せ）。
        // バージョンの整形は AppVersion（App.Core）が単一所有する
        var versionText = new TextBlock()
            .Text(AppVersion.For("MewUI"))
            .FontSize(AppStyles.NavVersionFontSize);
        versionText.HorizontalAlignment = HorizontalAlignment.Center;
        versionText.Margin = new Thickness(0, 8, 0, 8);
        versionText.IsVisible = viewModel.IsNavigationExpanded;
        navVersionText = versionText;

        // 項目一覧は残りの高さ（*）を占め、フッターは下端（Auto）へ寄せる（Avalonia 版の Grid と同じ）
        navPanel.Child(new Grid().Rows("Auto,*,Auto")
            .Children(header, items, versionText));

        // 開閉の変更でナビパネルの幅・文字の表示を更新する
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.IsNavigationExpanded), StringComparison.Ordinal))
            {
                settingsService.Current.IsNavigationExpanded = viewModel.IsNavigationExpanded;
                settingsService.Save();
                ApplyNavigationLayout();
            }
        };

        // ページ領域の左の余白（ナビの収納時の幅）。 ナビを開いてもページ領域は動かさない
        var navGutter = new Border { Width = NavPanelMetrics.CompactWidth, Background = MewUIColor.Transparent };




        // ナビパネルとメニューは ページ領域の上へ重ねる（後から追加した要素が前面に描かれる）。
        // タイトル バーの帯は作らない（ページ領域は上端から始まる）
        var root = new Grid().Columns("Auto,*").Rows("*");
        root.Add(navGutter.Column(0));
        root.Add(contentHost.Column(1));
        root.Add(navDismissLayer.Column(0).ColumnSpan(2));
        root.Add(navPanel.Column(0).ColumnSpan(2));
        root.Add(windowMenu.Column(0).ColumnSpan(2));
        window.Content(root);

        // 上端の帯（タイトル バー相当）をドラッグしてウィンドウを移動する。
        // 子の要素がクリックを処理した場合はここへ届かない（各ボタンの操作を妨げない）
        root.OnMouseDown(e =>
        {
            var point = e.GetPosition(root);

            // ナビの列は除く（開閉ボタンの操作をドラッグで奪わない）
            if (point.Y < WindowLayout.TitleBarHeight && point.X >= viewModel.NavPanelWidth)
            {
                window.DragMove();
                e.Handled = true;
            }
        });
        root.OnMouseDoubleClick(e =>
        {
            if (e.GetPosition(root).Y < WindowLayout.TitleBarHeight)
            {
                ToggleMaximize(window);
                e.Handled = true;
            }
        });

        // タイトルバーは MewUI 標準のクライアント領域拡張を使う（Avalonia 版の
        // ExtendClientAreaToDecorationsHint / ExtendClientAreaTitleBarHeightHint と同じ）。
        // タイトル文字列は空にし、タイトルバーへアプリ名を表示しない（Avalonia 版と同じ）。
        window.Title = AppIdentity.ProductName;
        window.ExtendClientAreaTitleBarHeight = TitleBarHeight;

        // ウィンドウ余白（Window 既定スタイルの ContainerPadding）を 0 にして、タイトルバーと
        // ウィンドウ操作ボタンをウィンドウ端へ寄せる（参考実装と同じ）。
        window.Padding = Thickness.Zero;

        // クライアント領域を拡張してもネイティブのウィンドウボタンは描画されないため、
        // タイトルバーの右端に自前で置く（Avalonia 版と同じ位置・役割）
        root.Add(BuildWindowButtons(window).Column(0).ColumnSpan(2).Row(0));

        // タスクバー・Alt+Tab のアイコンは Avalonia 版と同じアイコンを使う
        window.Icon = IconLoader.LoadWindowIcon(AppIdentity.IconFileName, 32, MewUIColor.Black);

        // 同名ファイルが存在する場合の上書き確認は、オーナーウィンドウの中央にダイアログを出して問い合わせる
        // （未設定のままだと SaveImageAsync が確認なしに保存を中止し、操作が無言で無効になる）
        viewModel.ConfirmOverwriteAsync = fileName => ConfirmDialog.ShowAsync(
            window, "上書き確認", $"ファイル「{fileName}」は既に存在します。上書きしますか？");

        RestoreWindowState(window, settingsService);
        ApplyNavigationLayout();

        // 自動切り出し中は Escape でキャンセルする（切り出しモードと同じ操作）。
        // 自動切り出しはオーバーレイコントロールを持たない（ViewModel の状態で表示する）ため、
        // ページ側でキーを受けられず、ウィンドウのトンネル段階で処理する。
        // ページ固有のショートカット（切り出しの Ctrl+R 等）もここから表示中のページへ転送する。
        window.PreviewKeyDown += e =>
        {
            if (e.Key == Key.Escape && viewModel.IsAutoCropActive)
            {
                viewModel.CancelAutoCropCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // 表示中のページにのみ転送する（非表示のページのショートカットを発火させない）
            if (selectedIndex >= 0 && pages[selectedIndex] is { OnKeyDown: { } onKeyDown })
            {
                onKeyDown(e);
            }
        };

        // ウィンドウが非アクティブになったら、表示中のページが開いているポップアップを閉じる
        // （メニューを開いたまま別のウィンドウを操作しても閉じない、との指摘への対応）
        window.Deactivated += () =>
        {
            // ウィンドウの外を操作した場合もナビ領域の外の操作とみなして閉じる
            CollapseNavigation();

            if (selectedIndex >= 0 && pages[selectedIndex] is { OnWindowDeactivated: { } onWindowDeactivated })
            {
                onWindowDeactivated();
            }
        };

        // テーマが変わったらアイコンとパネル背景の色も更新する（Avalonia 版はテーマのブラシを継承するため）
        window.ThemeChanged += (_, _) =>
        {
            window.Background = AppStyles.CurrentPageBackground;
            ApplyThemeColors();

            // スタイルで表せない値（画像アイコンの色・コントロールへ直接設定した色）を使うページへ通知する
            AppStyles.NotifyThemeChanged();
        };

        // 終了時にウィンドウ配置とナビ開閉を保存する（強制終了では保存されない）
        // Closed は Win32 のウィンドウプロシージャから呼ばれる境界のため、例外はここで吸収する
        // （伝播すると CLR がプロセスを異常終了させ、終了ログも残らない）。
        window.Closed += () =>
        {
            try
            {
                SaveWindowPlacement(window, settingsService);
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // 保存できなくても終了は妨げない（次の起動では前回保存された配置を使う）
                s_logger.LogWindowPlacementSaveError(ex);
            }
        };

        // ページ内容は選択時に生成し、生成後は同じ要素を使い回す（ページの状態と購読を保つ）
        void Select(int index)
        {
            EnsurePage(index);

            // 表示から外れるページへ通知する（表示中だけの処理＝サムネイルの背景生成を下げる）
            // ページの要素は IsVisible を切り替えるだけで再利用されるため、要素側では「隠れた」ことを検知できない
            if (index != selectedIndex && pages[selectedIndex] is { } leaving)
            {
                leaving.OnDeactivated?.Invoke();
            }

            for (var i = 0; i < pages.Length; i++)
            {
                if (pages[i] is { } page)
                {
                    page.Content.IsVisible = i == index;
                }
            }

            selectedIndex = index;

            // 選択状態はスタイルの切り替えで示す（ローカル値で背景を設定するとホバーの状態変化が効かなくなる）
            for (var i = 0; i < navItemButtons.Length; i++)
            {
                navItemButtons[i].StyleName = i == index ? AppStyles.NavItemSelected : AppStyles.NavItem;
            }

            ApplySelectionMarks();

            activators[index]?.Invoke();
        }

        // 選択中だけ左端のマークをアクセント色にする（テーマ変更でも呼び直す）
        void ApplySelectionMarks()
        {
            var accent = AppStyles.CurrentNavSelectionAccent;
            for (var i = 0; i < navItemMarks.Length; i++)
            {
                navItemMarks[i].Background = i == selectedIndex ? accent : MewUIColor.Transparent;
            }
        }

        void EnsurePage(int index)
        {
            if (pages[index] is not null)
            {
                return;
            }

            var built = entries[index].Content();
            pages[index] = built;
            activators[index] = built.OnActivated;
            warmUps[index] = built.OnWarmUp;
            contentHost.Add(built.Content);
            built.Content.IsVisible = false;
        }

        // 一覧ページのサムネイルを先に生成しておく（最初の切り替えを待たせない）
        // 初回描画の後（ウィンドウの表示が終わった後）に開始するため、起動は遅くならない。
        // ページを表示せずに一覧を構築し、生成は低負荷（1 件ずつ）で進むため、キャプチャページの操作を妨げない。
        // **1 ページずつ別のディスパッチャー ターンで作る**: まとめて作ると、その間 UI スレッドが
        // 塞がれてキャプチャ ページの操作が引っかかる（実測）。
        void WarmUpPages()
        {
            var pending = new Queue<AppPage>([AppPage.FolderView, AppPage.Undo, AppPage.Settings]);
            ScheduleNext();

            void ScheduleNext()
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || pending.Count == 0)
                {
                    return;
                }

                dispatcher.BeginInvoke(DispatcherPriority.Background, Step);
            }

            void Step()
            {
                try
                {
                    var page = pending.Dequeue();
                    var index = Array.FindIndex(entries, entry => entry.Page == page);
                    if (index >= 0)
                    {
                        EnsurePage(index);
                        warmUps[index]?.Invoke();
                    }
                }
                catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
                {
                    // 先読みの失敗はアプリの動作を妨げない（ページを開いたときに改めて構築される）
                    s_logger.LogThumbnailGenerateError(ex);
                }

                ScheduleNext();
            }
        }

        window.DpiChanged += (_, _) => ApplyMinimumOuterSize(window);
        window.FirstFrameRendered += () =>
        {
            ApplyMinimumOuterSize(window);
            ApplyInitialOuterSize(window, settingsService);
            WarmUpPages();
        };

        // ウィンドウ メニューの項目を組み立てて開く（項目と並びは WindowMenu（App.Core）が単一所有）
        void ShowWindowMenu(FrameworkElement anchor)
        {
            var panel = new StackPanel().Vertical().Spacing(0).Padding(new Thickness(4));
            foreach (var entry in WindowMenu.Items(window.WindowState == WindowState.Maximized))
            {
                if (entry.IsSeparatorBefore)
                {
                    panel.Add(new Border { Height = 1, Margin = new Thickness(6, 4, 6, 4), StyleName = AppStyles.Divider });
                }

                var item = new Button
                {
                    Content = new TextBlock().Text(entry.Label).FontSize(12).VerticalAlignment(VerticalAlignment.Center),
                    Padding = new Thickness(10, 6, 10, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = 0,
                    StyleName = AppStyles.FlatButton,
                };
                item.IsEnabled(entry.IsEnabled);
                item.Cursor(CursorType.Hand);
                var action = entry.Action;
                item.OnClick(() =>
                {
                    CloseWindowMenu();
                    ExecuteWindowMenuAction(action);
                });
                panel.Add(item);
            }

            windowMenu.Child = panel;
            windowMenu.Width = 180;
            windowMenu.IsVisible = true;
            navDismissLayer.IsVisible = true;
            _ = anchor;
        }

        // 開いているウィンドウ メニューを閉じる
        void CloseWindowMenu()
        {
            windowMenu.IsVisible = false;
            navDismissLayer.IsVisible = viewModel.IsNavigationExpanded;
        }

        // ウィンドウ メニューの項目を実行する
        void ExecuteWindowMenuAction(WindowMenuAction action)
        {
            switch (action)
            {
                case WindowMenuAction.Restore:
                    window.WindowState = WindowState.Normal;
                    break;
                case WindowMenuAction.Move:
                    SendSystemCommand(SystemWindowCommands.BeginMove);
                    break;
                case WindowMenuAction.Size:
                    SendSystemCommand(SystemWindowCommands.BeginSize);
                    break;
                case WindowMenuAction.Minimize:
                    window.Minimize();
                    break;
                case WindowMenuAction.Maximize:
                    window.Maximize();
                    break;
                case WindowMenuAction.Close:
                    window.Close();
                    break;
                default:
                    break;
            }
        }

        // ウィンドウへネイティブのシステム コマンドを送る
        void SendSystemCommand(Func<EsPlatformHandle, bool> command)
        {
            if (window.Handle != IntPtr.Zero)
            {
                _ = command(new EsPlatformHandle(window.Handle));
            }
        }
        void ApplyNavigationLayout()
        {
            var expanded = viewModel.IsNavigationExpanded;
            navPanel.Width = viewModel.NavPanelWidth;
            // アプリ名とフッターは展開時だけ表示する（収納時はアイコンだけ）
            navTitle.IsVisible = expanded;
            navVersionText.IsVisible = expanded;
            navDismissLayer.IsVisible = expanded || windowMenu.IsVisible;
            navToggleIcon.Source = LoadNavIcon(NavIconPanelIcon(expanded));

            foreach (var text in navItemTexts)
            {
                text.IsVisible = expanded;
            }
        }

        // 展開中のナビを閉じる（設定への保存とレイアウトの更新は IsNavigationExpanded の購読が行う）
        void CollapseNavigation()
        {
            if (viewModel.IsNavigationExpanded)
            {
                viewModel.IsNavigationExpanded = false;
            }
        }

        void ApplyThemeColors()
        {
            var color = IconColor();
            navToggleIcon.Source = IconLoader.LoadImage(NavIconPanelIcon(viewModel.IsNavigationExpanded), NavIconRasterSize, color);
            appIconImage.Source = IconLoader.LoadImage(AppIdentity.IconFileName, (int)WindowLayout.AppIconSize, color);

            for (var i = 0; i < navItemImages.Length; i++)
            {
                navItemImages[i].Source = IconLoader.LoadImage(entries[i].Icon, NavIconRasterSize, color);
            }

            Select(selectedIndex);
        }

        Select(0);
        return window;
    }

    /// <summary>ウィンドウ操作ボタンのアイコンの大きさ（半径。 DIP）。</summary>
    private const double WindowGlyphSize = 5;

    /// <summary>現在のウィンドウ状態に応じた最大化ボタンのアイコン種別を求める。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <returns>最大化中は「元のサイズに戻す」、それ以外は「最大化」のアイコン種別。</returns>
    private static GlyphKind WindowRestoreKind(Window window)
        => window.WindowState == WindowState.Maximized ? GlyphKind.WindowRestore : GlyphKind.WindowMaximize;

    /// <summary>タイトルバーの最大化ボタンと同じ動作でウィンドウ状態を切り替える（最大化 ⇔ 元のサイズ）。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    private static void ToggleMaximize(Window window)
        => window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    /// <summary>タイトルバー右端のウィンドウ操作ボタン（最小化 / 最大化 / 閉じる）を生成する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <returns>右寄せのボタン群。</returns>
    /// <remarks>
    /// クライアント領域を拡張するとネイティブのウィンドウボタンが描画されないため、同じ役割のボタンを自前で置く。<br/>
    /// アイコンは MewUI のベクターグリフ（<see cref="GlyphElement"/>）を使う<br/>
    /// （文字や画像はインクの位置がフォント・SVG に依存し、領域の中央からずれる）。<br/>
    /// 最大化中は「元のサイズに戻す」アイコンへ切り替わり、同じボタンが元のサイズへの復帰として動作する。
    /// </remarks>
    private static FrameworkElement BuildWindowButtons(Window window)
    {
        var row = new StackPanel().Horizontal().Spacing(0);

        GlyphElement Add(GlyphKind kind, string toolTip, string styleName, Action action)
        {
            var glyph = new GlyphElement { Kind = kind, GlyphSize = WindowGlyphSize };
            var button = new Button
            {
                Content = glyph.Center(),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Width = AppStyles.WindowButtonMinWidth,
                Height = AppStyles.WindowButtonHeight,
                CornerRadius = 0,
                StyleName = styleName,
            };
            button.Cursor(CursorType.Hand);
            button.WithToolTip(toolTip, action);
            row.Add(button);
            return glyph;
        }

        Add(GlyphKind.WindowMinimize, "最小化", AppStyles.WindowButton, window.Minimize);
        var maximizeGlyph = Add(GlyphKind.WindowMaximize, "最大化 / 元のサイズに戻す", AppStyles.WindowButton, () => ToggleMaximize(window));
        Add(GlyphKind.Cross, "閉じる", AppStyles.WindowCloseButton, window.Close);

        // 最大化の切り替えにアイコンを追従させる（最大化中は「元のサイズに戻す」）
        window.WindowStateChanged += _ => maximizeGlyph.Kind = WindowRestoreKind(window);

        row.HorizontalAlignment = HorizontalAlignment.Right;
        row.VerticalAlignment = VerticalAlignment.Top;
        row.Height = TitleBarHeight;
        return row;
    }

    /// <summary>ナビの開閉状態に応じたアイコンのファイル名を取得する。</summary>
    /// <param name="expanded">開いているかどうか。</param>
    /// <returns>開いているときは「閉じる」、閉じているときは「開く」のアイコン。</returns>
    private static string NavIconPanelIcon(bool expanded)
        => expanded ? NavIconPanelLeftContract : NavIconPanelRightContract;

    /// <summary>ナビ項目のアイコン画像を現在のテーマ色で生成する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <returns>生成したアイコン画像。</returns>
    private static ImageSource LoadNavIcon(string fileName)
        => IconLoader.LoadImage(fileName, NavIconRasterSize, IconLoader.ForegroundColor());

    /// <summary>テーマに応じたアイコン・文字の色を取得する。</summary>
    /// <returns>前景色。</returns>
    private static MewUIColor IconColor() => IconLoader.ForegroundColor();

    /// <summary>保存済みのウィンドウ状態（最大化等）を復元する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="settingsService">設定サービス。</param>
    /// <remarks>ウィンドウ状態は UI 非依存のコア型から MewUI 型へ境界で変換する。</remarks>
    private static void RestoreWindowState(Window window, ISettingsService settingsService)
        => window.WindowState = settingsService.Current.MainWindowState.ToMewUIWindowState();

    /// <summary>保存済みのウィンドウ位置・サイズを復元する（ウィンドウの表示前に呼ぶ）。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="settingsService">設定サービス。</param>
    /// <remarks>
    /// 位置・サイズとも DIP は<b>そのウィンドウのモニターの DPI 基準</b>（実測で確認）であるため、<br/>
    /// 保存値（画面物理ピクセル）をモニターのスケールで割って渡す。<br/>
    /// モニターの DPI は <c>Application.Run</c> より前は取得できない（MewUI の <c>DpiHelper</c> は 96 を返す）ため、<br/>
    /// 本メソッドは <c>Application.Run(window, startup)</c> の <c>startup</c>（ディスパッチャ導入後・表示前）から呼ぶ。<br/>
    /// 幅・高さが 0 以下の壊れた値では復元しない（0 サイズのウィンドウで操作不能になるのを防ぐ）。<br/>
    /// <b>最小サイズはここでも指定する</b>（<see cref="Window.WindowSize"/> を置き換えるため、生成時の指定は失われる）。<br/>
    /// 保存値が最小サイズより小さい場合は最小サイズへ引き上げる（Avalonia 版の <c>MinWidth</c> / <c>MinHeight</c> と同じ扱い）。
    /// </remarks>
    public static void RestorePlacement(Window window, ISettingsService settingsService)
    {
        ApplyMinimumOuterSize(window);

        if (!EsWindowPlacementMath.IsUsable(settingsService.Current.MainWindowBounds))
        {
            return;
        }

        var bounds = settingsService.Current.MainWindowBounds!.Value;
        var scale = DpiScaler.NormalizeScaling(DpiHelper.GetDpiForPoint(new Point(bounds.X, bounds.Y)) / 96.0);
        var position = EsWindowPlacementMath.ToDipPoint(new EsPointInt(bounds.X, bounds.Y), scale);
        window.StartManualPosition(position.X, position.Y);
    }

    /// <summary>実際のウィンドウ枠を考慮して外寸の最小サイズを設定する。</summary>
    private static void ApplyMinimumOuterSize(Window window)
    {
        var hwnd = window.Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var frame = WinWindowFrame.GetSize(new EsPlatformHandle(hwnd));
        var scaling = DpiScaler.NormalizeScaling(DpiHelper.GetScaleFactor(hwnd));
        var currentSize = window.WindowSize;

        window.WindowSize = WindowSize.Resizable(
            currentSize.Width,
            currentSize.Height,
            WindowPlacementRule.ToClientLength(WindowMetrics.MinimumWidth, frame.Width, scaling),
            WindowPlacementRule.ToClientLength(WindowMetrics.MinimumHeight, frame.Height, scaling),
            currentSize.MaxWidth,
            currentSize.MaxHeight);
    }

    /// <summary>枠を含む保存サイズまたは既定サイズをクライアント領域へ換算して設定する。</summary>
    private static void ApplyInitialOuterSize(Window window, ISettingsService settingsService)
    {
        var hwnd = window.Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var frame = WinWindowFrame.GetSize(new EsPlatformHandle(hwnd));
        var scaling = DpiScaler.NormalizeScaling(DpiHelper.GetScaleFactor(hwnd));
        var bounds = settingsService.Current.MainWindowBounds;
        var savedScale = EsWindowPlacementMath.IsUsable(bounds)
            ? DpiScaler.NormalizeScaling(DpiHelper.GetDpiForPoint(new Point(bounds!.Value.X, bounds.Value.Y)) / 96.0)
            : scaling;
        var outerWidth = EsWindowPlacementMath.IsUsable(bounds)
            ? EsWindowPlacementMath.ToDipLength(bounds!.Value.Width, savedScale)
            : WindowMetrics.DefaultWidth;
        var outerHeight = EsWindowPlacementMath.IsUsable(bounds)
            ? EsWindowPlacementMath.ToDipLength(bounds!.Value.Height, savedScale)
            : WindowMetrics.DefaultHeight;
        var minimumWidth = window.WindowSize.MinWidth;
        var minimumHeight = window.WindowSize.MinHeight;
        window.WindowSize = WindowSize.Resizable(
            Math.Max(minimumWidth, WindowPlacementRule.ToClientLength(outerWidth, frame.Width, scaling)),
            Math.Max(minimumHeight, WindowPlacementRule.ToClientLength(outerHeight, frame.Height, scaling)),
            minimumWidth,
            minimumHeight,
            window.WindowSize.MaxWidth,
            window.WindowSize.MaxHeight);
    }

    /// <summary>ウィンドウの位置・サイズ・状態とナビパネルの開閉を設定へ保存する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="settingsService">設定サービス。</param>
    /// <remarks>
    /// MewUI の位置・サイズは DIP のため、ウィンドウの DPI スケールを掛けて画面物理ピクセルへ変換してから保存する<br/>
    /// （変換は <see cref="EsWindowPlacementMath"/> へ委譲する）。<br/>
    /// ナビパネルの開閉は <see cref="ISettingsService.Current"/> へ既に反映されている（変更時に保存する）。<br/>
    /// 保存に失敗しても終了処理を妨げない（ファイル入出力の失敗は <c>SettingsService.Save</c> が吸収し、<br/>
    /// それ以外の例外は呼び出し元の <c>Closed</c> ハンドラーが吸収して Warning で記録する）。
    /// </remarks>
    private static void SaveWindowPlacement(Window window, ISettingsService settingsService)
    {
        var scale = DpiScaler.NormalizeScaling(DpiHelper.GetScaleFactor(window.Handle));
        var frame = WinWindowFrame.GetSize(new EsPlatformHandle(window.Handle));
        var position = EsWindowPlacementMath.ToPhysicalPixelPoint(
            new EsPoint(window.Position.X, window.Position.Y), scale);

        settingsService.Current.MainWindowBounds = new EsRectInt(
            position.X,
            position.Y,
            WindowPlacementRule.ToOuterPhysicalLength(window.ClientSize.Width, frame.Width, scale),
            WindowPlacementRule.ToOuterPhysicalLength(window.ClientSize.Height, frame.Height, scale));
        settingsService.Current.MainWindowState = window.WindowState.ToEsWindowState();

        // ナビパネルの開閉は変更時に保存済みのため、ここでは配置と状態のみを保存する
        settingsService.Save();
    }
}

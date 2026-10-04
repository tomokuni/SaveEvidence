using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.SaveEvidence.Platform.MewUI;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.MewUI.Icons;
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
/// MewUI のウィンドウ位置・サイズは DIP、設定は画面物理ピクセルのため、変換は <see cref="WindowPlacementMath"/> が単一所有する。
/// </remarks>
internal sealed class MainWindow
{
    /// <summary>復元するサイズが設定されていない場合の既定ウィンドウサイズ（DIP）。</summary>
    private static readonly Size DefaultWindowSize = new(1200, 800);

    /// <summary>ウィンドウの最小サイズ（DIP）。</summary>
    /// <remarks>Avalonia 版 <c>MainWindow.axaml</c> の <c>MinWidth</c> / <c>MinHeight</c> と同じ値。</remarks>
    private static readonly Size MinWindowSize = new(800, 400);

    /// <summary>タイトルバー領域の高さ（DIP）。 非クライアント領域をこの高さでクライアント領域へ拡張する。</summary>
    private const double TitleBarHeight = 32;

    /// <summary>ナビパネル上部（タイトルバー領域）の高さ（DIP）。</summary>
    /// <remarks>Avalonia 版と同じ 50。 トグルボタン（高さ 28）を中央に置くと上端に余白が生まれ、ツールバーと高さが揃う。</remarks>
    private const double NavHeaderHeight = 50;

    /// <summary>ナビ項目のアイコンの一辺（DIP）。</summary>
    private const double NavIconSize = 16;

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

        // 現在選択中のページ（テーマ変更時の再適用で使う）
        var selectedIndex = 0;

        // ページ内容の入れ替え先（各ページは生成時に追加し、選択に応じて表示を切り替える）
        var contentHost = new Grid().Columns("*").Rows("*");

        // ウィンドウは先に生成する（ナビパネル上部のドラッグ領域が本ウィンドウを参照するため）。
        // ページ内容は後から設定する。
        var window = new Window()
            .Resizable(
                DefaultWindowSize.Width, DefaultWindowSize.Height,
                minWidth: MinWindowSize.Width, minHeight: MinWindowSize.Height);

        // ホバー・押下で変化する見た目はスタイルで与える（ローカル値で背景を設定すると状態変化が効かなくなる）
        window.StyleSheet = AppStyles.Create();

        // ページ（ウィンドウ）の背景は Avalonia 版と同じ色へ揃える（明色は白・暗色は黒）
        window.Background = AppStyles.CurrentPageBackground;

        // ナビパネルの要素（開閉に応じて幅・文字の表示を切り替える）
        var navPanel = new Border();
        var navTitle = new TextBlock().Text("SaveEvidence").FontSize(14);
        var navItemTexts = new TextBlock[entries.Length];
        var navItemImages = new Image[entries.Length];
        var navItemButtons = new Button[entries.Length];
        var navToggleIcon = new Image
        {
            Source = LoadNavIcon("ic_fluent_arrow_autofit_width_dotted_24_regular.svg"),
            Width = NavIconSize,
            Height = NavIconSize
        };

        var toggle = new Button
        {
            Content = navToggleIcon.Center(),
            Margin = new Thickness(6, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Width = AppStyles.NavToggleSize,
            Height = AppStyles.NavToggleSize,
            StyleName = AppStyles.NavToggle,
        };
        toggle.Cursor(CursorType.Hand);
        toggle.WithToolTip("ナビゲーションを開閉", () => viewModel.IsNavigationExpanded = !viewModel.IsNavigationExpanded);

        var items = new StackPanel().Vertical().Spacing(0).Margin(new Thickness(0, 8, 0, 8));

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var icon = new Image
            {
                Source = LoadNavIcon(entry.Icon),
                Width = NavIconSize,
                Height = NavIconSize
            };
            navItemImages[i] = icon;

            var text = new TextBlock().Text(entry.Title);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.IsVisible = viewModel.IsNavigationExpanded;
            navItemTexts[i] = text;

            var button = new Button
            {
                Content = new StackPanel().Horizontal().Spacing(8).Children(icon, text),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0),
                CornerRadius = 0,
                StyleName = AppStyles.NavItem,
            };
            button.Cursor(CursorType.Hand);

            var index = i;
            button.WithToolTip(entry.Title, () => Select(index));
            navItemButtons[i] = button;
            items.Add(button);
        }

        var header = new Grid().Columns("Auto,*").Rows($"{NavHeaderHeight}");
        header.Add(toggle.Column(0));
        navTitle.VerticalAlignment = VerticalAlignment.Center;
        navTitle.Margin = new Thickness(6, 0, 0, 0);
        navTitle.IsVisible = viewModel.IsNavigationExpanded;

        // アプリ名の列全体をタイトルバーのドラッグ領域にする（収納時もアプリ名の領域でドラッグできる）。
        // トグルボタンとウィンドウ操作ボタンは含めない（各ボタンがクリックを処理する）。
        var dragArea = new Border
        {
            Child = navTitle,
            Background = MewUIColor.Transparent,
        };
        dragArea.OnMouseDown(_ => window.DragMove());
        dragArea.OnMouseDoubleClick(_ => ToggleMaximize(window));
        header.Add(dragArea.Column(1));

        // ナビパネルの背景はスタイルで与える（テーマ変更で自動的に再解決される）
        navPanel.StyleName = AppStyles.NavPanel;
        navPanel.Width = viewModel.NavPanelWidth;
        navPanel.Child(new StackPanel().Vertical().Children(header, items));

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

        var root = new Grid().Columns("Auto,*").Rows("*");
        root.Add(navPanel.Column(0));
        root.Add(contentHost.Column(1));
        window.Content(root);

        // タイトルバーは MewUI 標準のクライアント領域拡張を使う（Avalonia 版の
        // ExtendClientAreaToDecorationsHint / ExtendClientAreaTitleBarHeightHint と同じ）。
        // タイトル文字列は空にし、タイトルバーへアプリ名を表示しない（Avalonia 版と同じ）。
        window.Title = "";
        window.ExtendClientAreaTitleBarHeight = TitleBarHeight;

        // ウィンドウ余白（Window 既定スタイルの ContainerPadding）を 0 にして、タイトルバーと
        // ウィンドウ操作ボタンをウィンドウ端へ寄せる（参考実装と同じ）。
        window.Padding = Thickness.Zero;

        // クライアント領域を拡張してもネイティブのウィンドウボタンは描画されないため、
        // タイトルバーの右端に自前で置く（Avalonia 版と同じ位置・役割）
        root.Add(BuildWindowButtons(window).Column(0).ColumnSpan(2));

        // タスクバー・Alt+Tab のアイコンは Avalonia 版と同じアイコンを使う
        window.Icon = IconLoader.LoadWindowIcon("ic_fluent_scan_table_24_regular.svg", 32, MewUIColor.Black);

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

        // テーマが変わったらアイコンとパネル背景の色も更新する（Avalonia 版はテーマのブラシを継承するため）
        window.ThemeChanged += (_, _) =>
        {
            window.Background = AppStyles.CurrentPageBackground;
            ApplyThemeColors();

            // スタイルで表せない値（画像アイコンの色・コントロールへ直接設定した色）を使うページへ通知する
            AppStyles.NotifyThemeChanged();
        };

        // 終了時にウィンドウ配置とナビ開閉を保存する（強制終了では保存されない）
        window.Closed += () => SaveWindowPlacement(window, settingsService);

        // ページ内容は選択時に生成し、生成後は同じ要素を使い回す（ページの状態と購読を保つ）
        void Select(int index)
        {
            EnsurePage(index);

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

            activators[index]?.Invoke();
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
            contentHost.Add(built.Content);
            built.Content.IsVisible = false;
        }

        void ApplyNavigationLayout()
        {
            var expanded = viewModel.IsNavigationExpanded;
            navPanel.Width = viewModel.NavPanelWidth;
            navTitle.IsVisible = expanded;

            foreach (var text in navItemTexts)
            {
                text.IsVisible = expanded;
            }
        }

        void ApplyThemeColors()
        {
            var color = IconColor();
            navToggleIcon.Source = IconLoader.LoadImage("ic_fluent_arrow_autofit_width_dotted_24_regular.svg", NavIconRasterSize, color);

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
        if (!WindowPlacementMath.IsUsable(settingsService.Current.MainWindowBounds))
        {
            return;
        }

        var bounds = settingsService.Current.MainWindowBounds!.Value;
        var scale = DpiHelper.GetDpiForPoint(new Point(bounds.X, bounds.Y)) / 96.0;

        window.WindowSize = WindowSize.Resizable(
            Math.Max(MinWindowSize.Width, WindowPlacementMath.ToDipLength(bounds.Width, scale)),
            Math.Max(MinWindowSize.Height, WindowPlacementMath.ToDipLength(bounds.Height, scale)),
            MinWindowSize.Width, MinWindowSize.Height);

        var position = WindowPlacementMath.ToDipPoint(new EsPointInt(bounds.X, bounds.Y), scale);
        window.StartManualPosition(position.X, position.Y);
    }

    /// <summary>ウィンドウの位置・サイズ・状態とナビパネルの開閉を設定へ保存する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="settingsService">設定サービス。</param>
    /// <remarks>
    /// MewUI の位置・サイズは DIP のため、ウィンドウの DPI スケールを掛けて画面物理ピクセルへ変換してから保存する<br/>
    /// （変換は <see cref="WindowPlacementMath"/> へ委譲する）。<br/>
    /// ナビパネルの開閉は <see cref="ISettingsService.Current"/> へ既に反映されている（変更時に保存する）。<br/>
    /// 保存に失敗しても終了処理を妨げない（<c>SettingsService.Save</c> が内部で例外を吸収する）。
    /// </remarks>
    private static void SaveWindowPlacement(Window window, ISettingsService settingsService)
    {
        var scale = DpiHelper.GetScaleFactor(window.Handle);
        var position = WindowPlacementMath.ToPhysicalPixelPoint(
            new EsPoint(window.Position.X, window.Position.Y), scale);
        var size = WindowPlacementMath.ToPhysicalPixelSize(
            new EsSize(window.ClientSize.Width, window.ClientSize.Height), scale);

        settingsService.Current.MainWindowBounds = new EsRectInt(position.X, position.Y, size.Width, size.Height);
        settingsService.Current.MainWindowState = window.WindowState.ToEsWindowState();

        // ナビパネルの開閉は変更時に保存済みのため、ここでは配置と状態のみを保存する
        settingsService.Save();
    }
}

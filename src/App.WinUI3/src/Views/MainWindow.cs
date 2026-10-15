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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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
        Title = "SaveEvidence";

        var viewModel = services.Main;
        var settings = services.Settings;

        // ナビの開閉は設定から復元する（変更は購読が設定へ保存する）
        viewModel.IsNavigationExpanded = settings.Current.IsNavigationExpanded;

        BuildNavigation();
        WireViewModel(viewModel, settings);
        Content = _navigation;

        // 上書き確認とクリップボードは UI 型を要求するため、UI 層の実装をコールバックへ配線する
        viewModel.ConfirmOverwriteAsync = fileName => ConfirmDialog.ShowAsync(
            _navigation.XamlRoot!, "上書きの確認", $"{fileName} は既に存在します。上書きしますか？");
        viewModel.CopyToClipboardCallback = image => Task.FromResult(CaptureClipboard.Copy(image));
        viewModel.PasteFromClipboardCallback = CaptureClipboard.PasteAsync;

        SelectPage(_currentPage, force: true);

        // 保存済みの位置・サイズ・表示状態を復元する（表示前でも AppWindow は操作できる）
        WindowPlacementStore.Restore(this, settings.Current);

        AppStyles.ApplyTheme(_navigation, settings.Current.ThemeMode);
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;

        // 先読みは要素ツリーが実体化した後に行う（表示が済んでから一覧の構築を始める）
        _navigation.Loaded += (_, _) => TryWarmUpPages();
    }

    /// <summary>アプリ全体で共有するコアオブジェクトを取得する。</summary>
    /// <value>ページへ渡すサービス束。</value>
    public AppServices Services { get; }

    /// <summary>現在表示中のページの内容を取得する。</summary>
    /// <value>表示中のページ。 未生成の場合は null。</value>
    private PageContent? CurrentPageContent => _pages[(int)_currentPage];

    /// <summary>ナビゲーション ペインとメニュー項目を組み立てる。</summary>
    private void BuildNavigation()
    {
        _navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
        _navigation.IsSettingsVisible = false;
        _navigation.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        _navigation.IsTitleBarAutoPaddingEnabled = false;
        _navigation.OpenPaneLength = AppStyles.NavPanelExpandedWidth;
        _navigation.CompactPaneLength = AppStyles.NavPanelCompactWidth;
        _navigation.IsPaneOpen = Services.Main.IsNavigationExpanded;

        foreach (var entry in s_entries)
        {
            _navigation.MenuItems.Add(new NavigationViewItem
            {
                Content = entry.Title,
                Tag = entry.Page,
                Icon = new ImageIcon { Source = IconLoader.LoadThemedImage(entry.Icon, AppStyles.IconRasterSize) },
            });
        }

        _navigation.IsPaneToggleButtonVisible = true;
        _navigation.SelectionFollowsFocus = NavigationViewSelectionFollowsFocus.Disabled;
        _navigation.ItemInvoked += OnNavigationItemInvoked;
        _navigation.KeyDown += OnRootKeyDown;

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
            }
        };

        settings.Current.PropertyChanged += (_, e) =>
        {
            if (!string.Equals(e.PropertyName, nameof(Settings.ThemeMode), StringComparison.Ordinal))
            {
                return;
            }

            AppStyles.ApplyTheme(_navigation, settings.Current.ThemeMode);
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

    /// <summary>一覧ページを表示せずに構築し、サムネイルの低負荷生成を始める。</summary>
    /// <remarks>
    /// 最初の切り替えで生成済みのサムネイルが並ぶようにする（キャプチャ ページの操作は妨げない）。<br/>
    /// 先読みの失敗はアプリの動作を妨げない（ページを開いたときに改めて構築される）。
    /// </remarks>
    private void WarmUpPages()
    {
        try
        {
            foreach (var page in (AppPage[])[AppPage.FolderView, AppPage.Undo])
            {
                var index = Array.FindIndex(s_entries, entry => entry.Page == page);
                if (index >= 0)
                {
                    EnsurePage(index).OnWarmUp?.Invoke();
                }
            }
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogThumbnailGenerateError(ex);
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

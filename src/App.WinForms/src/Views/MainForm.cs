using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.WinForms.Icons;
using EsUtil.SaveEvidence.App.WinForms.Logging;
using EsUtil.SaveEvidence.Platform.WinForms;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>アプリのメイン ウィンドウ（WinForms 版）。</summary>
/// <remarks>
/// <b>構成</b>: 左のナビゲーション パネル（アイコン + ページ名）と右のページ内容。<br/>
/// ページの内容は<b>選択時に 1 度だけ生成</b>し、以降は同じコントロールを表示 / 非表示で切り替える<br/>
/// （ページの状態と変更購読を保つため）。<br/>
/// <b>グローバル ホットキー</b>: ウィンドウ ハンドルの確定後（<c>Shown</c>）に登録する。 登録できない組み合わせは<br/>
/// 記録だけ行い、アプリは継続する（他のアプリとの競合で起動を止めない）。<br/>
/// <b>ウィンドウ配置</b>: 起動時に位置・サイズ・状態を復元し、終了時に保存する。 設定の座標は画面物理ピクセルであり、<br/>
/// WinForms のウィンドウ矩形も同じ座標系のため、変換せずに受け渡す。<br/>
/// <b>テーマ</b>: ページ背景とナビゲーション パネルはテーマに追従し、ページにも変更を通知する。
/// </remarks>
internal sealed class MainForm : Form
{
    /// <summary>ロガー（ウィンドウ配置の保存の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(MainForm));

    /// <summary>復元するサイズが設定されていない場合の既定ウィンドウ サイズ（ピクセル）。</summary>
    private static readonly Size s_defaultWindowSize = new(1200, 800);

    /// <summary>ウィンドウの最小サイズ（ピクセル）。</summary>
    private static readonly Size s_minWindowSize = new(800, 400);

    /// <summary>ナビゲーション パネルのヘッダーの高さ（DIP）。</summary>
    private const int NavHeaderHeight = 34;

    /// <summary>ナビゲーション項目の高さ（DIP）。</summary>
    private const int NavItemHeight = 32;

    /// <summary>アプリ全体で共有するコアオブジェクト。</summary>
    private readonly AppServices _services;

    /// <summary>グローバル ホットキーの登録と押下の配信。</summary>
    private readonly IGlobalHotKeyService<CaptureType> _hotKeys = new WinGlobalHotKeyService<CaptureType>();

    /// <summary>ナビゲーションの項目（表示名・内容の生成・アイコン）。</summary>
    private readonly NavEntry[] _entries;

    /// <summary>生成済みのページ内容（選択時に生成し、以降は使い回す）。</summary>
    private readonly PageContent?[] _pages;

    /// <summary>ナビゲーションのボタン。</summary>
    private readonly ToolButton[] _navButtons;

    /// <summary>ナビゲーション パネル。</summary>
    private readonly Panel _navPanel = new() { Dock = DockStyle.Left, Padding = new Padding(0, 4, 0, 4) };

    /// <summary>ナビゲーション項目を並べる入れ物。</summary>
    private readonly FlowLayoutPanel _navItems = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
    };

    /// <summary>ページ内容の入れ物。</summary>
    private readonly Panel _contentHost = new() { Dock = DockStyle.Fill };

    /// <summary>ナビゲーション パネルの開閉ボタン。</summary>
    private readonly ToolButton _navToggle;

    /// <summary>現在選択中のページ。</summary>
    private int _selectedIndex = -1;

    /// <summary>起動後の先読みを実行済みかどうか。</summary>
    private bool _isWarmedUp;

    /// <summary>メイン ウィンドウを生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が null の場合。</exception>
    /// <remarks>
    /// 1. ナビゲーション パネルを組み、ページ内容は選択時に生成する（起動を軽くする）。<br/>
    /// 2. 保存済みのウィンドウ配置とナビゲーションの開閉を復元する。<br/>
    /// 3. ハンドルの確定後にホットキーを登録し、最初の描画の後に一覧の先読みを始める。<br/>
    /// 4. 終了時にウィンドウ配置とナビゲーションの開閉を保存する。
    /// </remarks>
    public MainForm(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;

        var viewModel = services.Main;
        Text = "SaveEvidence";
        Icon = SystemIcons.Application;
        MinimumSize = s_minWindowSize;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = false;

        // ナビゲーションの開閉は設定から復元する（変更は下の購読が設定へ保存する）
        viewModel.IsNavigationExpanded = services.Settings.Current.IsNavigationExpanded;

        _entries =
        [
            new(AppPage.Capture, "キャプチャ", AppIcon.ScreenSelect, () => CapturePage.Build(services, this)),
            new(AppPage.FolderView, "フォルダビュー", AppIcon.Folder, () => FolderViewPage.Build(services)),
            new(AppPage.Undo, "Undo 一覧", AppIcon.Undo, () => UndoPage.Build(services)),
            new(AppPage.Settings, "設定", AppIcon.Settings, () => SettingsPage.Build(services)),
        ];
        _pages = new PageContent?[_entries.Length];
        _navButtons = new ToolButton[_entries.Length];

        _navToggle = UiFactory.CreateToolButton(AppIcon.NavToggle, "ナビゲーション パネルの開閉", ToggleNavigation);
        _navToggle.Location = new Point(4, 4);
        _navToggle.Size = new Size(26, 26);

        for (var i = 0; i < _entries.Length; i++)
        {
            var index = i;
            _navButtons[index] = new ToolButton(
                _entries[index].Icon, _entries[index].Title, _entries[index].Title)
            {
                Height = NavItemHeight,
                Width = 160,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 1, 0, 1),
            };
            _navButtons[index].Click += (_, _) => Select(index);
        }

        _navPanel.Controls.Add(_navItems);
        _navPanel.Controls.Add(_navToggle);
        _navItems.Controls.AddRange(_navButtons);
        _navItems.Padding = new Padding(4, NavHeaderHeight, 4, 4);

        Controls.Add(_contentHost);
        Controls.Add(_navPanel);

        RestorePlacement();

        AppStyles.ThemeChanged += (_, _) => ApplyAppearance();
        AppStyles.ApplyTheme(services.Settings.Current.ThemeMode);
        ApplyAppearance();
        ApplyNavigationLayout();

        ActiveControl = null;
        Select(0);

        Shown += OnShown;
        FormClosing += OnFormClosing;
        FormClosed += OnFormClosed;
    }

    /// <summary>ページ表示の定義。</summary>
    /// <param name="Page">対応する <see cref="AppPage"/>。</param>
    /// <param name="Title">ナビゲーションに表示する名前。</param>
    /// <param name="Icon">ナビゲーションに表示するアイコン。</param>
    /// <param name="Content">内容を生成する関数。</param>
    private sealed record NavEntry(AppPage Page, string Title, AppIcon Icon, Func<PageContent> Content);

    /// <summary>表示中のページのショートカット キーを処理する。</summary>
    /// <param name="msg">処理するメッセージ。</param>
    /// <param name="keyData">押されたキー。</param>
    /// <returns>処理した場合は true。</returns>
    /// <remarks>
    /// ページ固有のショートカット（保存・コピー・貼り付け・履歴・切り出し）は<br/>
    /// 表示中のページへだけ転送する（非表示のページのショートカットを発火させない）。
    /// </remarks>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_selectedIndex >= 0 && _pages[_selectedIndex] is { OnShortcut: { } onShortcut } && onShortcut(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>表示後にホットキーを登録し、一覧の先読みを始める。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnShown(object? sender, EventArgs e)
    {
        _services.Settings.Current.PropertyChanged += OnSettingsChanged;
        RegisterHotKeys();

        if (_isWarmedUp)
        {
            return;
        }

        _isWarmedUp = true;

        // 最初の描画の後に先読みを始める（起動を遅くしない）
        BeginInvoke(() =>
        {
            WarmUp(AppPage.FolderView);
            WarmUp(AppPage.Undo);
        });
    }

    /// <summary>終了時にウィンドウ配置とナビゲーションの開閉を保存する。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// ウィンドウの終了は Win32 のウィンドウプロシージャから呼ばれる境界であり、例外を伝播させない<br/>
    /// （伝播すると CLR がプロセスを異常終了させ、終了ログも保存結果も残らない）。
    /// </remarks>
    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            SavePlacement();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogWindowPlacementSaveError(ex);
        }
    }

    /// <summary>終了時にホットキーの登録を解除する。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _services.Settings.Current.PropertyChanged -= OnSettingsChanged;
        _hotKeys.Dispose();
    }

    /// <summary>設定の変更（ホットキー）を登録へ反映する。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">変更されたプロパティ。</param>
    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(App.Core.Models.Settings.SelectScreenHotKey)
            or nameof(App.Core.Models.Settings.WindowSelectHotKey)
            or nameof(App.Core.Models.Settings.AreaSelectHotKey))
        {
            RegisterHotKeys();
        }
    }

    /// <summary>設定のホットキーを登録し、押下時にキャプチャを開始するよう配線する。</summary>
    /// <remarks>
    /// 1. ウィンドウ ハンドルをサービスへ渡す（<c>WM_HOTKEY</c> の受信先。 ウィンドウ プロシージャのサブクラス化を含む）。<br/>
    /// 2. 3 種のキャプチャに割り当てられた設定を登録する（未設定のものはスキップする）。<br/>
    /// 3. 押下時は <c>MainViewModel.StartSelectionMode</c> を経由してキャプチャを開始する<br/>
    ///    （ツールバーのボタンと同じ経路に一本化し、開始処理を二重所有しない）。<br/>
    /// 4. 登録できなかった組み合わせは Warning で記録する（他のアプリとの競合で 1 件だけ機能しない場合がある）。
    /// </remarks>
    private void RegisterHotKeys()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        _ = _hotKeys.Subscribe(type => _services.Main.StartSelectionMode?.Invoke(type));

        try
        {
            _hotKeys.Initialize(Handle);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // ウィンドウ プロシージャを差し替えられない場合はホットキーを使えない（アプリは継続する）
            s_logger.LogUnhandledException(ex, "MainForm.RegisterHotKeys");
            return;
        }

        var settings = _services.Settings.Current;
        List<(CaptureType Action, HotKeySetting Setting)> registrations = [];
        Add(CaptureType.ScreenSelect, settings.SelectScreenHotKey);
        Add(CaptureType.WindowSelect, settings.WindowSelectHotKey);
        Add(CaptureType.AreaSelect, settings.AreaSelectHotKey);

        foreach (var (action, setting) in _hotKeys.RegisterAll(registrations))
        {
            s_logger.LogHotKeyRegisterFailed(action.ToString(), setting.ToString());
        }

        void Add(CaptureType action, HotKeySetting setting)
        {
            // 未設定（修飾キーもキーも無い）は登録しない
            if (setting.Key != EsKey.None || setting.Modifiers != HotKeyModifiers.None)
            {
                registrations.Add((action, setting));
            }
        }
    }

    /// <summary>ページを選択する（未生成なら生成する）。</summary>
    /// <param name="index">選択するページの位置。</param>
    private void Select(int index)
    {
        EnsurePage(index);

        // 表示から外れるページへ通知する（表示中だけの処理＝背景の生成を低負荷へ戻す）
        if (index != _selectedIndex && _selectedIndex >= 0 && _pages[_selectedIndex] is { } leaving)
        {
            leaving.OnDeactivated?.Invoke();
        }

        for (var i = 0; i < _pages.Length; i++)
        {
            if (_pages[i] is { } page)
            {
                page.Content.Visible = i == index;
            }
        }

        _selectedIndex = index;

        for (var i = 0; i < _navButtons.Length; i++)
        {
            _navButtons[i].IsSelected = i == index;
        }

        _pages[index]!.OnActivated?.Invoke();
    }

    /// <summary>ページの内容を生成する（生成済みの場合は何もしない）。</summary>
    /// <param name="index">生成するページの位置。</param>
    private void EnsurePage(int index)
    {
        if (_pages[index] is not null)
        {
            return;
        }

        var built = _entries[index].Content();
        _pages[index] = built;
        built.Content.Visible = false;
        _contentHost.Controls.Add(built.Content);
    }

    /// <summary>一覧ページのサムネイルを先に生成しておく（最初の切り替えを待たせない）。</summary>
    /// <param name="page">先読みするページ。</param>
    /// <remarks>
    /// ページを表示せずに一覧を構築し、生成は低負荷（1 件ずつ）で進むため、キャプチャページの操作を妨げない。
    /// </remarks>
    private void WarmUp(AppPage page)
    {
        try
        {
            var index = Array.FindIndex(_entries, entry => entry.Page == page);
            if (index < 0)
            {
                return;
            }

            EnsurePage(index);
            _pages[index]!.OnWarmUp?.Invoke();
            s_logger.LogThumbnailWarmUp(page.ToString(), _pages[index] is { } content ? content.Content.Controls.Count : 0);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 先読みの失敗はアプリの動作を妨げない（ページを開いたときに改めて構築される）
            s_logger.LogThumbnailGenerateError(ex);
        }
    }

    /// <summary>ナビゲーション パネルの開閉を切り替える。</summary>
    private void ToggleNavigation()
    {
        var expanded = !_services.Main.IsNavigationExpanded;
        _services.Main.IsNavigationExpanded = expanded;
        _services.Settings.Current.IsNavigationExpanded = expanded;
        _services.Settings.Save();
        ApplyNavigationLayout();
    }

    /// <summary>ナビゲーション パネルの幅と文言の表示を設定へ合わせる。</summary>
    private void ApplyNavigationLayout()
    {
        var expanded = _services.Main.IsNavigationExpanded;
        _navPanel.Width = (int)Math.Round(_services.Main.NavPanelWidth);

        for (var i = 0; i < _navButtons.Length; i++)
        {
            _navButtons[i].Text = expanded ? _entries[i].Title : "";
            _navButtons[i].Width = expanded ? Math.Max(40, _navPanel.Width - 12) : 32;
        }
    }

    /// <summary>配色を現在のテーマへ合わせる。</summary>
    private void ApplyAppearance()
    {
        BackColor = AppStyles.PageBackground;
        _navPanel.BackColor = AppStyles.NavBackground;
        _navItems.BackColor = AppStyles.NavBackground;
        _contentHost.BackColor = AppStyles.PageBackground;
        _navToggle.BackColor = AppStyles.NavBackground;

        for (var i = 0; i < _navButtons.Length; i++)
        {
            // 選択中の背景色はボタン自身が持つ（テーマ変更で描き直す）
            _navButtons[i].IsSelected = i == _selectedIndex;
        }
    }

    /// <summary>保存済みのウィンドウ配置を復元する。</summary>
    /// <remarks>
    /// 設定の座標は画面物理ピクセルであり、WinForms のウィンドウ矩形も同じ座標系のため変換しない。<br/>
    /// 幅・高さが 1 未満の壊れた値は既定サイズへフォールバックする（0 サイズのウィンドウを作らない）。
    /// </remarks>
    private void RestorePlacement()
    {
        var settings = _services.Settings.Current;
        var bounds = settings.MainWindowBounds;
        if (bounds is { } value && WindowPlacementMath.IsUsable(value))
        {
            SetBounds(value.X, value.Y, value.Width, value.Height);
        }
        else
        {
            Size = s_defaultWindowSize;
        }

        if (settings.MainWindowState.IsRepresentable())
        {
            WindowState = settings.MainWindowState.ToFormWindowState();
        }
    }

    /// <summary>現在のウィンドウ配置を設定へ保存する。</summary>
    private void SavePlacement()
    {
        var settings = _services.Settings.Current;

        // 最大化・最小化中でも復元できるよう、通常表示の矩形を保存する
        var bounds = RestoreBounds;
        settings.MainWindowBounds = new EsRectInt(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        settings.MainWindowState = WindowState.ToEsWindowState();
        settings.IsNavigationExpanded = _services.Main.IsNavigationExpanded;
        _services.Settings.Save();
    }
}

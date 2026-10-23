using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Window;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.WinForms.Icons;
using EsUtil.SaveEvidence.App.WinForms.Logging;
using EsUtil.Ui.WinForms.Interop;
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

    /// <summary>復元するサイズが設定されていない場合の既定ウィンドウ サイズ（DIP）。</summary>
    /// <remarks>値は <see cref="WindowMetrics"/>（App.Core）が 4 つの UI 版で共通に単一所有する。</remarks>
    private static readonly Size s_defaultWindowSizeDip = new((int)WindowMetrics.DefaultWidth, (int)WindowMetrics.DefaultHeight);


    /// <summary>ナビゲーション項目の高さ（DIP）。 4 つの UI 版で共通。</summary>
    /// <remarks>値は <see cref="NavPanelMetrics"/>（App.Core）が単一所有する。</remarks>
    private const int NavItemHeight = (int)NavPanelMetrics.ItemHeight;

    /// <summary>ナビゲーション パネルのフッター（UI 名とバージョン）の高さ（DIP）。</summary>
    private const int NavFooterHeight = 28;

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

    /// <summary>ナビゲーション パネル（ページ領域の上へ重ねる。 位置と大きさは <see cref="LayoutNavigation"/> が決める）。</summary>
    private readonly Panel _navPanel = new() { Padding = new Padding(0, 0, 0, 0) };

    /// <summary>ナビ領域の外の押下でナビゲーションを閉じるメッセージ フィルター。</summary>
    private readonly NavigationDismissFilter _navDismissFilter;

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


    /// <summary>ナビゲーション パネルのフッター（UI 名とバージョン）。 収納時は非表示。</summary>
    private readonly Label _navFooter = new()
    {
        Dock = DockStyle.Bottom,
        Height = NavFooterHeight,
        TextAlign = ContentAlignment.MiddleCenter,
        AutoSize = false,
        Text = AppVersion.For("WinForms"),
    };

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
        Text = AppIdentity.ProductName;
        // アイコンは他の UI 版と同じものを使う（共有する SVG から描く）
        Icon = IconLoader.CreateWindowIcon();
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = false;

        // ナビゲーションの開閉は設定から復元する（変更は下の購読が設定へ保存する）
        viewModel.IsNavigationExpanded = services.Settings.Current.IsNavigationExpanded;

        _entries =
        [
            new(AppPage.Capture, "キャプチャ", AppIcon.Capture, () => CapturePage.Build(services, this)),
            new(AppPage.FolderView, "フォルダビュー", AppIcon.Folder, () => FolderViewPage.Build(services)),
            new(AppPage.Undo, "Undo 一覧", AppIcon.Undo, () => UndoPage.Build(services)),
            new(AppPage.Settings, "設定", AppIcon.Settings, () => SettingsPage.Build(services)),
        ];
        _pages = new PageContent?[_entries.Length];
        _navButtons = new ToolButton[_entries.Length];

        _navDismissFilter = new NavigationDismissFilter(this);

        // ナビゲーションの開閉は他のナビ項目と同じ外観にする（ツールバーのボタンの面・枠線は付けない）
        _navToggle = new ToolButton(
            viewModel.IsNavigationExpanded ? AppIcon.PanelClose : AppIcon.PanelOpen,
            "ナビゲーション パネルの開閉");
        _navToggle.Click += (_, _) => ToggleNavigation();
        UiFactory.AttachToolTip(_navToggle, "ナビゲーション パネルの開閉");
        // 大きさは DPI に合わせて ApplyNavigationLayout が設定する（AutoScaleMode は実行時の代入を拡大しない）
        _navToggle.Location = new Point(0, 0);

        for (var i = 0; i < _entries.Length; i++)
        {
            var index = i;
            _navButtons[index] = new ToolButton(
                _entries[index].Icon, _entries[index].Title, _entries[index].Title)
            {
                Height = NavItemHeight,
                Width = 160,
                TextAlign = ContentAlignment.MiddleLeft,
                // アイコンと文字を左端から並べる（中央寄せだと幅が足りないとき両側が欠ける）
                ImageAlign = ContentAlignment.MiddleLeft,
                // 左の 12 DIP は選択中のマーク（4..7 DIP）と間隔に使う（他の UI 版と同じ位置）
                ShowSelectionMark = true,
                Padding = new Padding(12, 0, 0, 0),
                Margin = new Padding(0, 1, 0, 1),
                // 文字サイズは 4 つの UI 版で共通（DIP → pt へ換算する。 NavPanelMetrics が単一所有する）
                Font = new Font(
                    AppStyles.UiFont.FontFamily,
                    (float)DpiScaler.ToPoint(NavPanelMetrics.ItemFontSize),
                    AppStyles.UiFont.Style),
            };
            _navButtons[index].Click += (_, _) =>
            {
                Select(index);
                CollapseNavigation();
            };
        }

        _navPanel.Controls.Add(_navItems);
        _navPanel.Controls.Add(_navFooter);
        _navPanel.Controls.Add(_navToggle);

        // 開閉ボタンは一番上に置く（ドッキングする要素より前面へ出す）。
        // アプリ名は OS 標準のタイトル バーが左上のアイコンの右へ表示する（本 UI 版は自前の帯を持たない）
        _navToggle.BringToFront();
        _navItems.Controls.AddRange(_navButtons);

        // 開閉ボタンの領域はナビゲーション パネルの Padding で確保する（ドッキングする項目一覧は
        // Padding の内側へ配置されるため、ボタンが一覧の下へ隠れない）。
        // フッターは下端へ寄せる（他の UI 版と同じ位置）
        // 開閉ボタンとアプリ名はパネルの左上に固定し、項目一覧はその下から並べる
        _navPanel.Padding = new Padding(0);

        // ナビゲーション パネルはページ領域の上へ重ねる（ページ領域は開閉で動かない）。
        // 前面へ置くため、ページ領域を追加した後に追加する
        Controls.Add(_contentHost);
        Controls.Add(_navPanel);
        _navPanel.BringToFront();

        // 本 UI 版はダークテーマを使用しない（常に明色）。 配色は明色で 1 回だけ適用する
        ApplyAppearance();
        ApplyNavigationLayout();

        ActiveControl = null;
        Select(0);

        Shown += OnShown;
        FormClosing += OnFormClosing;
        FormClosed += OnFormClosed;
    }

    /// <summary>ウィンドウの最小サイズ（DIP）。</summary>
    /// <remarks>値は <see cref="WindowMetrics"/>（App.Core）が 4 つの UI 版で共通に単一所有する。</remarks>
    private static readonly Size s_minWindowSizeDip = new((int)WindowMetrics.MinimumWidth, (int)WindowMetrics.MinimumHeight);

    /// <summary>自動スケーリングの後（読み込み時）に最小サイズと保存済みの配置を反映する。</summary>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// <b>読み込み時に行う理由</b>: <c>AutoScaleMode.Dpi</c> のとき、<br/>
    /// コンストラクタで設定した位置とサイズは読み込み時に DPI 倍率で<b>もう一度</b>掛け算される<br/>
    /// （実測: 800 × 1.25 = 1000 になる）。 保存値は画面物理ピクセルのため、拡大の後の<br/>
    /// ここで設定して倍率の二重適用を避ける。 最小サイズも同じ理由でここで設定する<br/>
    /// （<see cref="Form.MinimumSize"/> は画面物理ピクセルで解釈される）。
    /// </remarks>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        ApplyMinimumSize();
        RestorePlacement();

        // 拡大の後（DPI が確定した後）にナビゲーションの寸法を合わせ直す
        ApplyNavigationLayout();

        // 子コントロールの押下はフォームへ届かないため、メッセージを横取りして外側の押下を拾う
        Application.AddMessageFilter(_navDismissFilter);
    }

    /// <summary>ウィンドウの大きさが変わったらナビゲーションの高さを合わせる。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        LayoutNavigation();
    }

    /// <summary>ウィンドウが非アクティブになったら展開中のナビゲーションを閉じる。</summary>
    /// <param name="e">イベント引数。</param>
    /// <remarks>ウィンドウの外の操作もナビ領域の外の操作とみなす（他の UI 版と同じ）。</remarks>
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        CollapseNavigation();
    }

    /// <summary>DPI が変わったときに最小サイズを掛け直す。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyMinimumSize();
    }

    /// <summary>最小サイズを現在の DPI で画面物理ピクセルへ換算して設定する。</summary>
    private void ApplyMinimumSize()
    {
        var scaling = DpiScaler.ToScaling(DeviceDpi);
        MinimumSize = new Size(
            EsWindowPlacementMath.ToPhysicalLength(s_minWindowSizeDip.Width, scaling),
            EsWindowPlacementMath.ToPhysicalLength(s_minWindowSizeDip.Height, scaling));
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

        // キャプチャ ページの描画後、先読み対象を 1 ページずつ生成する。
        // BeginInvoke を直後に呼ぶと、投稿されたメッセージが最初の描画（WM_PAINT）より先に処理され、
        // キャプチャ ページが表示されるまで待たされる（実測）。 短いタイマーを 1 回だけ待つと、
        // 最初の描画が済んでから先読みが始まる。
        var pending = new Queue<AppPage>([AppPage.FolderView, AppPage.Undo]);
        var warmUpDelay = new System.Windows.Forms.Timer { Interval = 200 };
        warmUpDelay.Tick += (_, _) =>
        {
            warmUpDelay.Stop();
            warmUpDelay.Dispose();
            ScheduleNextWarmUp();
        };
        warmUpDelay.Start();

        // 1 ページ作るごとにメッセージ ループへ戻る（まとめて作ると、その間 UI スレッドが塞がれる）
        void ScheduleNextWarmUp()
        {
            if (pending.Count == 0 || IsDisposed)
            {
                return;
            }

            BeginInvoke(Step);

            void Step()
            {
                if (IsDisposed)
                {
                    return;
                }

                WarmUp(pending.Dequeue());
                ScheduleNextWarmUp();
            }
        }
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
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            s_logger.LogWindowPlacementSaveError(ex);
        }
    }

    /// <summary>終了時にホットキーの登録を解除する。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(_navDismissFilter);
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
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
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
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
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
    /// <remarks>
    /// 幅はコア（<see cref="NavPanelMetrics"/>）が DIP で単一所有する。 WinForms の <c>Width</c> は<br/>
    /// 画面物理ピクセルのため、実行時に代入する値は DPI 倍率で換算する（<c>AutoScaleMode</c> は<br/>
    /// デザイン時の値だけを拡大する）。
    /// </remarks>
    private void ApplyNavigationLayout()
    {
        var expanded = _services.Main.IsNavigationExpanded;
        var scaling = DpiScaler.ToScaling(DeviceDpi);
        _navPanel.Width = DpiScaler.ScaleToPixel(_services.Main.NavPanelWidth, scaling);

        // 開閉ボタンと項目一覧の開始位置（他の UI 版と同じ 40 DIP のボタンの下に 4 DIP 空ける）
        var toggleSize = DpiScaler.ScaleToPixel((int)WindowLayout.NavToggleHeight, scaling);
        _navToggle.Size = new Size(toggleSize, toggleSize);
        _navToggle.Image = IconLoader.Create(
            expanded ? AppIcon.PanelClose : AppIcon.PanelOpen,
            DpiScaler.ScaleToPixel(NavPanelMetrics.IconSize, scaling),
            AppStyles.Foreground);
        _navItems.Padding = new Padding(
            0, toggleSize + DpiScaler.ScaleToPixel(4, scaling), 0, DpiScaler.ScaleToPixel(4, scaling));

        // 項目は開閉・文字の有無で大きさを変えない（収納時もアイコンが中央へ来る幅にする）
        var itemHeight = DpiScaler.ScaleToPixel(NavItemHeight, scaling);
        for (var i = 0; i < _navButtons.Length; i++)
        {
            _navButtons[i].Text = expanded ? _entries[i].Title : "";
            _navButtons[i].Height = itemHeight;
            _navButtons[i].Width = expanded
                ? Math.Max(DpiScaler.ScaleToPixel(40, scaling), _navPanel.Width - DpiScaler.ScaleToPixel(2, scaling))
                : _navPanel.Width;
            // 収納時はアイコンだけを中央へ置く（文字が無いときにアイコンが左へ寄らないようにする）
            _navButtons[i].ImageAlign = expanded ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
            _navButtons[i].Padding = expanded ? new Padding(DpiScaler.ScaleToPixel(12, scaling), 0, 0, 0) : Padding.Empty;
        }

        _navFooter.Visible = expanded;
        LayoutNavigation();
    }

    /// <summary>ナビゲーション パネルとページ領域の配置を更新する。</summary>
    /// <remarks>
    /// ナビゲーション パネルは<b>ページ領域の上へ重ねる</b>（開いてもページ領域は動かない）。<br/>
    /// ページ領域は収納時の幅（<see cref="NavPanelMetrics.CompactWidth"/>）だけ左へ余白を取る<br/>
    /// （収納時のアイコンがページの操作を隠さない位置）。
    /// </remarks>
    private void LayoutNavigation()
    {
        var scaling = DpiScaler.ToScaling(DeviceDpi);
        _navPanel.Bounds = new Rectangle(0, 0, _navPanel.Width, ClientSize.Height);
        _contentHost.Padding = new Padding(
            DpiScaler.ScaleToPixel(NavPanelMetrics.CompactWidth, scaling), 0, 0, 0);
    }

    /// <summary>展開中のナビゲーションを閉じる。</summary>
    /// <remarks>設定への保存とレイアウトの更新は <see cref="ToggleNavigation"/> と同じ経路で行う。</remarks>
    private void CollapseNavigation()
    {
        if (!_services.Main.IsNavigationExpanded)
        {
            return;
        }

        _services.Main.IsNavigationExpanded = false;
        _services.Settings.Current.IsNavigationExpanded = false;
        _services.Settings.Save();
        ApplyNavigationLayout();
    }

    /// <summary>ナビ領域の外の押下でナビゲーションを閉じる。</summary>
    /// <param name="screenPoint">押下位置（画面座標）。</param>
    private void CollapseNavigationIfOutside(Point screenPoint)
    {
        if (!_services.Main.IsNavigationExpanded)
        {
            return;
        }

        if (!_navPanel.Bounds.Contains(PointToClient(screenPoint)))
        {
            CollapseNavigation();
        }
    }

    /// <summary>ナビ領域の外の押下でナビゲーションを閉じるメッセージ フィルター。</summary>
    /// <remarks>
    /// 子コントロールの押下はフォームへ届かない（WinForms のマウス イベントは親へ伝播しない）ため、<br/>
    /// アプリケーションのメッセージを横取りして押下位置だけを見る。 押下自体は妨げない。
    /// </remarks>
    /// <param name="owner">通知先のフォーム。</param>
    private sealed class NavigationDismissFilter(MainForm owner) : IMessageFilter
    {
        /// <summary>左ボタン・右ボタン・中央ボタンの押下のメッセージ。</summary>
        private static readonly int[] s_buttonDownMessages = [0x0201, 0x0204, 0x0207];

        /// <summary>メッセージを処理する。</summary>
        /// <param name="m">メッセージ。</param>
        /// <returns>常に false（メッセージはそのまま渡す）。</returns>
        public bool PreFilterMessage(ref Message m)
        {
            if (Array.IndexOf(s_buttonDownMessages, m.Msg) >= 0)
            {
                owner.CollapseNavigationIfOutside(Cursor.Position);
            }

            return false;
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
        _navFooter.BackColor = AppStyles.NavBackground;
        _navFooter.ForeColor = AppStyles.Foreground;

        for (var i = 0; i < _navButtons.Length; i++)
        {
            // 選択中の背景色はボタン自身が持つ（テーマ変更で描き直す）
            _navButtons[i].IsSelected = i == _selectedIndex;
        }
    }

    /// <summary>保存済みのウィンドウ配置を復元する。</summary>
    /// <remarks>
    /// 設定の座標は画面物理ピクセルであり、WinForms のウィンドウ矩形も同じ座標系のため変換しない。<br/>
    /// 復元してよいかの判定（壊れたサイズ・画面外の座標・最小化）は共有規則（<see cref="EsWindowPlacementMath"/>）が<br/>
    /// 単一所有する。 画面外の座標を復元するとウィンドウが見えないまま起動するため、その場合は位置だけを<br/>
    /// 表示領域の内側へ収める。
    /// </remarks>
    private void RestorePlacement()
    {
        var settings = _services.Settings.Current;
        var visibleArea = SystemInformation.VirtualScreen.ToEsRectInt();
        if (EsWindowPlacementMath.CanRestore(settings.MainWindowBounds, visibleArea))
        {
            var bounds = EsWindowPlacementMath.ClampToVisibleArea(settings.MainWindowBounds!.Value, visibleArea);
            SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
        else
        {
            var scaling = DpiScaler.ToScaling(DeviceDpi);
            Size = new Size(
                EsWindowPlacementMath.ToPhysicalLength(s_defaultWindowSizeDip.Width, scaling),
                EsWindowPlacementMath.ToPhysicalLength(s_defaultWindowSizeDip.Height, scaling));
        }

        // 最小化は復元しない（最小化のまま起動するとウィンドウが表示されない）
        var state = EsWindowPlacementMath.ToRestorableState(settings.MainWindowState);
        if (state.IsRepresentable())
        {
            WindowState = state.ToFormWindowState();
        }
    }

    /// <summary>現在のウィンドウ配置を設定へ保存する。</summary>
    /// <remarks>
    /// <b><see cref="Form.RestoreBounds"/> を使わない理由</b>: 最大化・最小化を一度もしていないウィンドウでは、<br/>
    /// <see cref="Form.RestoreBounds"/> が復元した時点の矩形を返したまま更新されない（実測 2026-10-17:<br/>
    /// 移動後も起動時の矩形が保存される）。 通常表示のときは現在の <see cref="Control.Bounds"/> を使う。
    /// </remarks>
    private void SavePlacement()
    {
        var settings = _services.Settings.Current;

        // 最大化・最小化中でも復元できるよう、通常表示の矩形を保存する
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        settings.MainWindowBounds = new EsRectInt(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        settings.MainWindowState = WindowState.ToEsWindowState();
        settings.IsNavigationExpanded = _services.Main.IsNavigationExpanded;
        _services.Settings.Save();
    }
}

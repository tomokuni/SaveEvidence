using app.Converters;
using app.Enum;
using app.Helpers;
using app.Models;
using app.Services;
using app.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;

namespace app.Views;

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
    private Models.HotKeyManager _hotKeyManager = null!;

    // ルーペ制御
    private bool _isLoupeVisible;
    private CancellationTokenSource? _loupeCts;

    // 切り出し選択
    private bool _cropModeActive;
    private bool _autoCropStarted;
    private Avalonia.Point _cropStartPoint;
    private Rect _cropRect;
    private CropDragHandle _activeHandle = CropDragHandle.None;
    private Avalonia.Point _dragStartPoint;
    private Rect _dragStartRect;

    // WM_SETCURSOR で使用する Win32 カーソルハンドル（切出しモード中の細かいカーソル制御用）
    private IntPtr _cropCursorOverride = IntPtr.Zero;

    private const double HandleSize = 8;
    private const double HandleHitRadius = 12;
    private const double MinCropSize = 20;

    private enum CropDragHandle { None, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight, Move }

    // 切出しキーボード微調整用（カーソルキーで切出し矩形を1px移動）
    private double _cropKeyboardOffsetX;
    private double _cropKeyboardOffsetY;
    private Rect _cropBaseRect;
    private bool _cropDragInProgress;   // ドラッグ中（新規選択作成中）
    private Avalonia.Point _cropLastDragPos; // ドラッグ中の最新マウス位置
    private bool _cropStartPointSet;    // クリックまたはキー操作で始点が設定済み
    // 切出しキーボード微調整時のルーペ表示位置（null=マウスカーソルを使用）
    private PixelPoint? _cropKeyboardLoupeScreenPos;

    // 画像のUndoスタック（最大5段階）
    private const int MaxUndoCount = 5;
    private readonly List<Avalonia.Media.Imaging.Bitmap> _undoStack = new(MaxUndoCount);
    private bool _isUndoing;

    // 選択ウィンドウ参照
    private SelectionWindow? _selectionWindow;

    // キャプチャ元の100%画像と画面領域（ルーペで使用、画面再キャプチャを回避）
    private System.Drawing.Bitmap? _originalCaptureSource;
    private System.Drawing.Rectangle? _originalCaptureBounds;

    // キャプチャ中フラグ（PropertyChanged で _originalCaptureSource の上書きを防ぐ）
    private bool _isCaptureInProgress;

    // 前回の市松模様サイズ（変更検知用）
    private int _lastCheckerSize = -1;

    // ナビゲーション状態（0=キャプチャ, 1=フォルダビュー, 2=設定）
    private int _navSelectedIndex;

    // キャプチャ前のウィンドウ状態（復元用）
    private WindowState _prevWindowState = WindowState.Normal;

    // Win32 WndProc サブクラス化（WM_HOTKEY 受信用）
    private IntPtr _prevWndProc;
    private WndProcDelegate? _wndProcDelegate;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_HOTKEY = 0x0312;

    [System.Runtime.InteropServices.LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();

    [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    private static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ── 切出しカーソル用 Win32 ──
    private const uint WM_SETCURSOR = 0x0020;
    private const int HTCLIENT = 1;

    private const int IDC_SIZENWSE = 32642;
    private const int IDC_SIZENESW = 32643;
    private const int IDC_SIZEWE = 32644;
    private const int IDC_SIZENS = 32645;
    private const int IDC_SIZEALL = 32646;
    private const int IDC_CROSS = 32515;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetCursor(IntPtr hCursor);

    private static readonly IntPtr _hCurSizeNWSE = LoadCursor(IntPtr.Zero, IDC_SIZENWSE);
    private static readonly IntPtr _hCurSizeNESW = LoadCursor(IntPtr.Zero, IDC_SIZENESW);
    private static readonly IntPtr _hCurSizeWE = LoadCursor(IntPtr.Zero, IDC_SIZEWE);
    private static readonly IntPtr _hCurSizeNS = LoadCursor(IntPtr.Zero, IDC_SIZENS);
    private static readonly IntPtr _hCurSizeAll = LoadCursor(IntPtr.Zero, IDC_SIZEALL);
    private static readonly IntPtr _hCurCross = LoadCursor(IntPtr.Zero, IDC_CROSS);

    /// <summary>
    /// MainWindow を初期化する。
    /// </summary>
    /// <param name="viewModel">メインViewModel</param>
    /// <param name="settingsService">設定サービス</param>
    public MainWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        if (Avalonia.Controls.Design.IsDesignMode) return;

        DataContext = viewModel;
        _viewModel = viewModel;
        _settingsService = (SettingsService)settingsService;

        // ViewModel にコールバックを設定
        SetupViewModelCallbacks();

        // ViewModel のプロパティ変更時にズーム設定を適用する
        _viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.PreviewImage):
                    OnPreviewImageChanged();
                    ApplyZoom();
                    break;
                case nameof(MainViewModel.ZoomPercent):
                    ApplyZoom();
                    SyncZoomComboBox();
                    break;
            }
        };

        // グローバルホットキーを登録（ネイティブウィンドウハンドルが必要）
        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is not null)
        {
            _hotKeyManager = new Models.HotKeyManager(platformHandle.Handle);
            RegisterHotKeys();
            // WM_HOTKEY メッセージを受信するためウィンドウプロシージャをサブクラス化
            SetupWndProc(platformHandle.Handle);

            // ホットキーはキャプチャページ表示時 (NavigateTo(0)) にのみ登録する
        }
        else
        {
            _hotKeyManager = null!;
        }

        // ウィンドウ位置・サイズの復元
        RestoreWindowBounds();

        // キーボードショートカット
        KeyDown += OnMainWindowKeyDown;
        // 切出し矢印キーはButton/ScrollViewerに横取りされるため handledEventsToo で強制捕捉
        // KeyDownEvent は bubble-only のため Tunnel 戦略は機能しない
        AddHandler(InputElement.KeyDownEvent, OnCropPreviewKeyDownTunnel, RoutingStrategies.Bubble, handledEventsToo: true);

        // Ctrl+マウスホイールによるズーム
        PreviewImageControl.PointerWheelChanged += OnImagePointerWheelChanged;

        // 切り出しのPointerイベントを常時アタッチ（ドラッグで自動開始）
        PreviewImageControl.PointerPressed += OnCropPointerPressed;
        PreviewImageControl.PointerMoved += OnCropPointerMoved;
        PreviewImageControl.PointerReleased += OnCropPointerReleased;
        PreviewImageControl.PointerExited += OnCropPointerExited;

        // 設定変更時に市松模様背景を更新
        SubscribeCheckerSizeChange();

        // 設定再読込時も市松模様を再購読＋更新
        _viewModel.AfterSettingsReloaded += () =>
        {
            SubscribeCheckerSizeChange();
            UpdateCheckerboardBackground();
        };

        // 初期ページ（キャプチャ）を選択状態にする
        NavigateTo(0);

        // 初期のズーム率を ComboBox に反映
        SyncZoomComboBox();

        // ScrollViewer リサイズ時に Fit 表示を再適用
        ImageScrollViewer.SizeChanged += OnImageScrollViewerSizeChanged;

        // アンロード時の処理
        Closing += OnMainWindowClosing;
        Activated += OnMainWindowActivated;
        Deactivated += OnMainWindowDeactivated;

        // 初期の市松模様背景を設定
        UpdateCheckerboardBackground();
    }

    /// <summary>ViewModel の PreviewImage 変更時に元画像参照を同期する。</summary>
    private void OnPreviewImageChanged()
    {
        // 画面キャプチャ直後は既に _originalCaptureSource/bounds が設定済みのためスキップ
        if (_isCaptureInProgress)
            return;

        // 切出し / ペースト / Undo: PreviewImage から _originalCaptureSource を再作成
        _originalCaptureBounds = null;
        _originalCaptureSource?.Dispose();
        _originalCaptureSource = _viewModel.PreviewImage?.ToDrawingBitmap();
    }

    /// <summary>市松模様サイズの変更通知を購読する。</summary>
    private void SubscribeCheckerSizeChange()
    {
        _viewModel.Settings.PropertyChanged += OnCheckerSizePropertyChanged;
    }

    /// <summary>設定の市松模様サイズ変更を監視する。</summary>
    private void OnCheckerSizePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.PreviewCheckerSize))
        {
            UpdateCheckerboardBackground();
        }
    }

    /// <summary>プレビュー領域の背景に市松模様を設定する。</summary>
    /// <remarks>
    /// 薄いグレー（#CCCCCC）とより薄いグレー（#E8E8E8）のタイルを
    /// <see cref="Settings.PreviewCheckerSize"/> の矩形サイズで描画する。<br/>
    /// </remarks>
    private void UpdateCheckerboardBackground()
    {
        try
        {
            var checkerSize = _viewModel.Settings.PreviewCheckerSize;
            if (checkerSize < 1) checkerSize = 2;

            var lightColor = Color.FromArgb(255, 204, 204, 204); // #CCCCCC 薄いグレー
            var lighterColor = Color.FromArgb(255, 232, 232, 232); // #E8E8E8 より薄いグレー

            var lightBrush = new SolidColorBrush(lightColor);
            var lighterBrush = new SolidColorBrush(lighterColor);

            var drawingGroup = new DrawingGroup();
            // ベース全体をより薄いグレーで塗りつぶし
            drawingGroup.Children.Add(new GeometryDrawing
            {
                Brush = lighterBrush,
                Geometry = new RectangleGeometry(new Rect(0, 0, checkerSize * 2, checkerSize * 2))
            });
            // 左上のタイルを薄いグレーで上書き
            drawingGroup.Children.Add(new GeometryDrawing
            {
                Brush = lightBrush,
                Geometry = new RectangleGeometry(new Rect(0, 0, checkerSize, checkerSize))
            });
            // 右下のタイルを薄いグレーで上書き
            drawingGroup.Children.Add(new GeometryDrawing
            {
                Brush = lightBrush,
                Geometry = new RectangleGeometry(new Rect(checkerSize, checkerSize, checkerSize, checkerSize))
            });

            ImageScrollViewer.Background = new DrawingBrush
            {
                Drawing = drawingGroup,
                TileMode = TileMode.Tile,
                DestinationRect = new RelativeRect(
                    0, 0, checkerSize * 2, checkerSize * 2, RelativeUnit.Absolute)
            };

            _lastCheckerSize = checkerSize;
        }
        catch
        {
            // 背景設定の失敗は無視（UI に影響しない）
        }
    }

    /// <summary>ViewModel に UI 操作用のコールバックを設定する。</summary>
    private void SetupViewModelCallbacks()
    {
        _viewModel.StartSelectionMode = StartSelectionMode;
        _viewModel.CopyToClipboardAsync = async (Avalonia.Media.Imaging.Bitmap? bitmap) =>
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
            return await ShowConfirmDialogAsync(
                "上書き確認",
                $"ファイル「{fileName}」は既に存在します。上書きしますか？");
        };

        // 画像編集前に Undo スタックに現在の画像を保存
        _viewModel.BeforeEditAction = PushUndo;
    }

    /// <summary>ホットキーを登録する。</summary>
    private void RegisterHotKeys()
    {
        _hotKeyManager.RegisterAll(_settingsService.Current);
    }

    /// <summary>Win32 ウィンドウプロシージャをサブクラス化し、WM_HOTKEY メッセージを捕捉する。</summary>
    /// <remarks>
    /// Avalonia は WndProc の直接オーバーライドを提供しないため、<br/>
    /// SetWindowLongPtr でネイティブウィンドウのプロシージャを置き換える。<br/>
    /// WM_HOTKEY (0x0312) を受信した場合、HotKeyManager で対応する CaptureType を取得し、<br/>
    /// 該当する選択キャプチャモードを開始する。<br/>
    /// </remarks>
    private void SetupWndProc(IntPtr hWnd)
    {
        _wndProcDelegate = (hW, msg, wParam, lParam) =>
        {
            if (msg == WM_HOTKEY)
            {
                CaptureLogger.Log("[MainWindow] WndProc: WM_HOTKEY received");
                var captureType = _hotKeyManager?.ProcessHotKeyMessage(msg, wParam);
                if (captureType.HasValue)
                {
                    CaptureLogger.Log("[MainWindow] WndProc: hotkey -> {0}", captureType.Value);
                    StartSelectionMode(captureType.Value);
                }
                return new IntPtr(1);
            }

            // 切出しモード中のカーソルオーバーライド
            if (msg == WM_SETCURSOR && _cropCursorOverride != IntPtr.Zero)
            {
                int hitTest = (int)(lParam.ToInt64() & 0xFFFF);
                if (hitTest == HTCLIENT)
                {
                    SetCursor(_cropCursorOverride);
                    return new IntPtr(1); // デフォルト処理を抑制
                }
            }

            return CallWindowProc(_prevWndProc, hW, msg, wParam, lParam);
        };
        _prevWndProc = SetWindowLongPtr(hWnd, GWLP_WNDPROC, System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    /// <summary>元のウィンドウプロシージャを復元する。</summary>
    private void RestoreWndProc()
    {
        if (_prevWndProc != IntPtr.Zero)
        {
            var platformHandle = TryGetPlatformHandle();
            if (platformHandle is not null)
            {
                SetWindowLongPtr(platformHandle.Handle, GWLP_WNDPROC, _prevWndProc);
                _prevWndProc = IntPtr.Zero;
            }
        }
    }

    /// <summary>設定保存時にウィンドウ位置を記憶する。</summary>
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowBounds();
        _hotKeyManager?.Dispose();
        RestoreWndProc();
        foreach (var img in _undoStack) { img.Dispose(); }
        _undoStack.Clear();
        _originalCaptureSource?.Dispose();
        _loupeCts?.Cancel();
    }

    /// <summary>アクティブ時にルーペ更新を再開する。</summary>
    private void OnMainWindowActivated(object? sender, EventArgs e)
    {
        CaptureLogger.Log("[MainWindow] Activated (ルーペ再開のみ、キャプチャは行わない)");
        if (_isLoupeVisible)
        {
            StartLoupeUpdate();
        }
    }

    /// <summary>非アクティブ時にルーペ更新を停止する。</summary>
    private void OnMainWindowDeactivated(object? sender, EventArgs e)
    {
        CaptureLogger.Log("[MainWindow] Deactivated");
        StopLoupeUpdate();
    }

    /// <summary>ウィンドウ位置・サイズを復元する。</summary>
    private void RestoreWindowBounds()
    {
        var bounds = _settingsService.Current.MainWindowBounds;
        if (bounds is not null)
        {
            Position = new PixelPoint(bounds.X, bounds.Y);
            Width = bounds.Width;
            Height = bounds.Height;
        }

        var state = _settingsService.Current.MainWindowState;
        WindowState = state switch
        {
            WindowStateValue.Maximized => Avalonia.Controls.WindowState.Maximized,
            WindowStateValue.Minimized => Avalonia.Controls.WindowState.Minimized,
            _ => Avalonia.Controls.WindowState.Normal,
        };
    }

    /// <summary>ウィンドウ位置・サイズを保存する。</summary>
    private void SaveWindowBounds()
    {
        _settingsService.Current.MainWindowBounds = new WindowBounds(
            Position.X, Position.Y, (int)Width, (int)Height);

        _settingsService.Current.MainWindowState = WindowState switch
        {
            Avalonia.Controls.WindowState.Maximized => WindowStateValue.Maximized,
            Avalonia.Controls.WindowState.Minimized => WindowStateValue.Minimized,
            _ => WindowStateValue.Normal,
        };

        _settingsService.Save();
    }

    /// <summary>ネイティブウィンドウハンドルを取得する。</summary>
    private static nint GetNativeHandle(TopLevel topLevel)
    {
        var platformHandle = topLevel.TryGetPlatformHandle();
        return platformHandle?.Handle ?? nint.Zero;
    }

    // ===================== ナビゲーション =====================

    /// <summary>指定されたインデックスのページに遷移する。</summary>
    /// <param name="index">遷移先ページインデックス（0=キャプチャ, 1=フォルダビュー, 2=設定）</param>
    private void NavigateTo(int index)
    {
        if (_navSelectedIndex == index) return;
        _navSelectedIndex = index;

        // 全ページを非表示
        CaptureContent.IsVisible = false;
        FolderViewContent.IsVisible = false;
        SettingsContent.IsVisible = false;

        // 選択中のページのみ表示
        switch (index)
        {
            case 0:
                CaptureContent.IsVisible = true;
                UpdateNavItemSelection(NavCapture);
                break;
            case 1:
                FolderViewContent.IsVisible = true;
                UpdateNavItemSelection(NavFolderView);
                break;
            case 2:
                SettingsContent.IsVisible = true;
                UpdateNavItemSelection(NavSettings);
                break;
        }

        // グローバルホットキーはキャプチャページ表示時のみ有効
        // （他ページでの誤キャプチャを防ぐ）
        if (index == 0)
        {
            RegisterHotKeys();
        }
        else
        {
            _hotKeyManager?.UnregisterAll();
        }
    }

    /// <summary>キャプチャページに遷移する（FolderViewPage からの呼び出し用）。</summary>
    public void NavigateToCapturePage() => NavigateTo(0);

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
    private void OnNavCaptureClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => NavigateTo(0);

    /// <summary>ナビ「フォルダビュー」クリック。</summary>
    private void OnNavFolderViewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => NavigateTo(1);

    /// <summary>ナビ「設定」クリック。</summary>
    private void OnNavSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => NavigateTo(2);

    // ===================== キャプチャ関連 =====================

    /// <summary>選択モードを開始する。</summary>
    private async void StartSelectionMode(CaptureType captureType)
    {
        CaptureLogger.Log("[MainWindow] StartSelectionMode: {0}", captureType);
        if (_selectionWindow is { IsVisible: true })
        {
            _selectionWindow.Close();
        }

        _selectionWindow = new SelectionWindow(captureType, OnSelectionComplete);

        var settings = _settingsService.Current;
        bool hideWindow = settings.HideMainWindowOnCapture;

        // ── ① MainWindow を非表示にする場合 ──
        if (hideWindow)
        {
            // 1) 最小化で DWM に確実に再合成させる（Hide()のみではフレームバッファが残る）
            _prevWindowState = WindowState;
            WindowState = WindowState.Minimized;
            DwmFlush();
            await Task.Delay(200);

            // 2) 背景キャプチャ（最小化により MainWindow は完全に画面から消えている）
            CaptureFixBackgroundFor(_selectionWindow);

            // 3) さらに確実に非表示にする（すでに最小化済みのためユーザーには見えない）
            this.Hide();

            // 4) オーナーなしで SelectionWindow を表示 → Activate で最前面へ
            _selectionWindow.Show();
            _selectionWindow.Activate();
            _selectionWindow.Closed += OnSelectionWindowClosed;
        }
        // ── ② MainWindow を表示したまま ──
        else
        {
            // SelectionWindow 表示前に背景キャプチャ
            CaptureFixBackgroundFor(_selectionWindow);
            // オーナー指定で表示（従来の動作）
            _selectionWindow.Show(this);
        }
    }

    /// <summary>現在の全スクリーンをキャプチャし、SelectionWindow の Fix背景として設定する。</summary>
    /// <remarks>
    /// このメソッドの呼び出し時点では SelectionWindow は未表示のため、<br/>
    /// キャプチャ結果に SelectionWindow が映り込むことはない。<br/>
    /// Hide 後に呼び出せば MainWindow も映り込まない。<br/>
    /// </remarks>
    private void CaptureFixBackgroundFor(SelectionWindow selectionWindow)
    {
        var captures = new List<(System.Drawing.Rectangle Bounds, System.Drawing.Bitmap Bitmap)>();
        var avaloniaBitmaps = new List<(System.Drawing.Rectangle Bounds, Avalonia.Media.Imaging.Bitmap Bitmap)>();
        foreach (var screen in Screens.All)
        {
            var sr = new System.Drawing.Rectangle(
                screen.Bounds.X, screen.Bounds.Y,
                screen.Bounds.Width, screen.Bounds.Height);
            if (sr.Width > 0 && sr.Height > 0)
            {
                var bmp = CaptureManager.CaptureArea(sr);
                captures.Add((sr, bmp));
                avaloniaBitmaps.Add((sr, bmp.ToAvaloniaBitmap()));
            }
        }
        selectionWindow.SetFixedBackgroundCaptures(captures, avaloniaBitmaps);
    }

    /// <summary>キャプチャ完了時のコールバック。</summary>
    private void OnSelectionComplete(System.Drawing.Bitmap? capturedBitmap, System.Drawing.Rectangle? sourceBounds)
    {
        CaptureLogger.Log("[MainWindow] OnSelectionComplete: bitmap={0}, bounds={1}",
            capturedBitmap is not null ? $"{capturedBitmap.Width}x{capturedBitmap.Height}" : "null",
            sourceBounds?.ToString() ?? "null");
        if (capturedBitmap is not null)
        {
            // Undoスタックに現在の画像をコピー保存
            PushUndo();

            // 100%元画像を保持（ルーペで画面再キャプチャせずに切り出し可能にする）
            _originalCaptureSource?.Dispose();
            _originalCaptureSource = new System.Drawing.Bitmap(capturedBitmap);
            _originalCaptureBounds = sourceBounds;

            _isCaptureInProgress = true;
            _viewModel.SetPreviewImage(capturedBitmap.ToAvaloniaBitmap());
            _isCaptureInProgress = false;
            capturedBitmap.Dispose();
        }

        // 非表示にしたメインウィンドウを復元
        if (!this.IsVisible)
        {
            CaptureLogger.Log("[MainWindow] OnSelectionComplete: メインウィンドウを再表示（state={0}）", _prevWindowState);
            this.Show();
            if (_prevWindowState != WindowState.Minimized)
            {
                WindowState = _prevWindowState;
            }
        }
    }

    /// <summary>SelectionWindow が閉じられた時のフォールバック復元処理。</summary>
    private void OnSelectionWindowClosed(object? sender, EventArgs e)
    {
        if (_selectionWindow is not null)
        {
            _selectionWindow.Closed -= OnSelectionWindowClosed;
        }
        if (!this.IsVisible)
        {
            CaptureLogger.Log("[MainWindow] OnSelectionWindowClosed: メインウィンドウを再表示（state={0}）", _prevWindowState);
            this.Show();
            if (_prevWindowState != WindowState.Minimized)
            {
                WindowState = _prevWindowState;
            }
        }
    }

    // ===================== メニューイベント =====================

    private void OnExitMenuClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnPasteMenuClick(object? sender, RoutedEventArgs e)
    {
        try {
            // ペースト後は元のキャプチャ画像とは無関係になるため
            // _originalCaptureSource/bounds は OnPreviewImageChanged で再生成される
            _originalCaptureBounds = null;
            _originalCaptureSource?.Dispose();
            _originalCaptureSource = null;
            await _viewModel.PasteFromClipboard();
        }
        catch { /* 貼り付け失敗は無視 */ }
    }

    private void OnUndoMenuClick(object? sender, RoutedEventArgs e) => RestoreUndo();

    private void OnCaptureScreenClick(object? sender, RoutedEventArgs e)
    {
        CaptureLogger.Log("[MainWindow] メニュー「全画面」クリック");
        StartSelectionMode(CaptureType.SelectScreen);
    }

    private void OnCaptureWindowClick(object? sender, RoutedEventArgs e)
    {
        CaptureLogger.Log("[MainWindow] メニュー「ウィンドウ」クリック");
        StartSelectionMode(CaptureType.WindowSelect);
    }

    private void OnCaptureAreaClick(object? sender, RoutedEventArgs e)
    {
        CaptureLogger.Log("[MainWindow] メニュー「範囲選択」クリック");
        StartSelectionMode(CaptureType.AreaSelect);
    }

    private void OnCropMenuClick(object? sender, RoutedEventArgs e) => OnCropButtonClick(sender, e);

    private void OnFolderViewMenuClick(object? sender, RoutedEventArgs e) => NavigateTo(1);

    private void OnSettingsMenuClick(object? sender, RoutedEventArgs e) => NavigateTo(2);

    private void OnHotkeyMenuClick(object? sender, RoutedEventArgs e) => NavigateTo(2);

    private void OnAboutMenuClick(object? sender, RoutedEventArgs e) => ShowAboutDialog();

    // ===================== ツールバーボタン =====================

    private async void OnSaveButtonClick(object? sender, RoutedEventArgs e)
        => await _viewModel.SaveImageAsync();

    private async void OnCopyButtonClick(object? sender, RoutedEventArgs e)
        => await _viewModel.CopyToClipboard();

    private async void OnPasteButtonClick(object? sender, RoutedEventArgs e)
    {
        // ペースト後は元のキャプチャ画像とは無関係になるため
        // _originalCaptureSource/bounds は OnPreviewImageChanged で再生成される
        _originalCaptureBounds = null;
        _originalCaptureSource?.Dispose();
        _originalCaptureSource = null;
        await _viewModel.PasteFromClipboard();
    }

    private void OnUndoButtonClick(object? sender, RoutedEventArgs e) => RestoreUndo();

    private void OnCropButtonClick(object? sender, RoutedEventArgs e)
    {
        if (!_cropModeActive)
        {
            // 切出しモード開始
            EnterCropMode();
        }
        else
        {
            // 選択範囲があれば実行、なければモード終了
            if (_cropRect.Width >= MinCropSize && _cropRect.Height >= MinCropSize)
            {
                ExecuteCrop();
            }
            else
            {
                ExitCropMode();
            }
        }
    }

    private void OnLoupeToggleClick(object? sender, RoutedEventArgs e) => ToggleLoupe();

    private void OnZoomInButtonClick(object? sender, RoutedEventArgs e) => ZoomIn();

    private void OnZoomOutButtonClick(object? sender, RoutedEventArgs e) => ZoomOut();

    // ===================== 切り出し (Crop) =====================

    /// <summary>切出しモードを開始する。</summary>
    private void EnterCropMode()
    {
        _cropModeActive = true;
        _autoCropStarted = false;
        _cropRect = new Rect();
        _cropCursorOverride = _hCurCross;
        Cursor = new Cursor(StandardCursorType.Cross);
        _cropStartPointSet = false;
        _cropKeyboardOffsetX = 0;
        _cropKeyboardOffsetY = 0;
        _cropKeyboardLoupeScreenPos = null;
        UpdateCropButtonText();
        UpdateCropOverlay();
    }

    /// <summary>切出しモードを終了する。</summary>
    private void ExitCropMode()
    {
        _cropModeActive = false;
        _autoCropStarted = false;
        _cropCursorOverride = IntPtr.Zero;
        Cursor = new Cursor(StandardCursorType.Arrow);
        _cropRect = new Rect();
        _cropKeyboardLoupeScreenPos = null;
        _cropStartPointSet = false;
        UpdateCropButtonText();
        UpdateCropOverlay();
    }

    /// <summary>現在の選択範囲で切り出しを実行する。</summary>
    private void ExecuteCrop()
    {
        if (_cropRect.Width < MinCropSize || _cropRect.Height < MinCropSize) return;

        var srcBmp = _viewModel.PreviewImage;
        if (srcBmp is null) return;

        PushUndo();

        // _cropRect は PreviewImageControl の DIP 座標。
        // 画像ピクセル座標に変換する（ズーム/Fitモードに依存しない）。
        double srcW = srcBmp.Size.Width;
        double srcH = srcBmp.Size.Height;
        double dispW = PreviewImageControl.Bounds.Width;
        double dispH = PreviewImageControl.Bounds.Height;

        if (dispW <= 0 || dispH <= 0) return;

        // Stretch=Uniform 時のレターボックス分のオフセットを計算
        double renderW = dispW, renderH = dispH;
        double offX = 0, offY = 0;
        if (PreviewImageControl.Stretch == Avalonia.Media.Stretch.Uniform && srcW > 0 && srcH > 0)
        {
            double aspect = srcW / srcH;
            double dispAspect = dispW / dispH;
            if (dispAspect > aspect)
            {
                renderH = dispH;
                renderW = dispH * aspect;
                offX = (dispW - renderW) / 2.0;
            }
            else
            {
                renderW = dispW;
                renderH = dispW / aspect;
                offY = (dispH - renderH) / 2.0;
            }
        }

        // 選択矩形を画像表示領域にクリップ
        double selX = Math.Max(_cropRect.X - offX, 0);
        double selY = Math.Max(_cropRect.Y - offY, 0);
        double selR = Math.Min(_cropRect.X + _cropRect.Width - offX, renderW);
        double selB = Math.Min(_cropRect.Y + _cropRect.Height - offY, renderH);

        // DIP→ピクセル変換
        double scaleX = srcW / Math.Max(renderW, 1);
        double scaleY = srcH / Math.Max(renderH, 1);

        var bounds = new System.Drawing.Rectangle(
            (int)(selX * scaleX),
            (int)(selY * scaleY),
            (int)((selR - selX) * scaleX),
            (int)((selB - selY) * scaleY));

        _viewModel.CropPreviewCommand.Execute(bounds);

        ExitCropMode();
    }

    /// <summary>ボタンテキストを現在の状態に合わせて更新する。</summary>
    private void UpdateCropButtonText()
    {
        if (CropButtonText is not null)
        {
            CropButtonText.Text = _cropModeActive ? "✂ 切出し確定" : "✂ 切出し開始";
        }
        if (CropButton is not null)
        {
            ToolTip.SetTip(CropButton, _cropModeActive
                ? "切り出し（確定して切り抜く）"
                : "切り出し（切出しモード開始）");
        }
    }

    /// <summary>画像上でPointer押下。</summary>
    private void OnCropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewImageControl);
        var pos = e.GetPosition(PreviewImageControl);

        // キーボードオフセットはリセットするが、始点位置は保持する
        _cropKeyboardOffsetX = 0;
        _cropKeyboardOffsetY = 0;

        // 右クリック → コンテキストメニュー表示（切出しモード時のみ）
        if (point.Properties.IsRightButtonPressed)
        {
            if (_cropModeActive)
            {
                ShowCropContextMenu();
                e.Handled = true;
            }
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;

        if (!_cropModeActive)
        {
            // ドラッグで自動的に切出しモード開始
            _cropModeActive = true;
            _autoCropStarted = true;
            _cropCursorOverride = _hCurCross;
            Cursor = new Cursor(StandardCursorType.Cross);
            UpdateCropButtonText();
        }

        // 既存の選択矩形がある場合、ハンドルまたは移動のヒットテスト
        if (_cropRect.Width >= HandleHitRadius && _cropRect.Height >= HandleHitRadius)
        {
            var handle = HitTestHandle(pos, _cropRect);
            if (handle != CropDragHandle.None)
            {
                _activeHandle = handle;
                _dragStartPoint = pos;
                _dragStartRect = _cropRect;
                return;
            }
        }

        // 新規選択開始（既存の矩形は置き換え）
        _activeHandle = CropDragHandle.None;
        _cropDragInProgress = false;
        _cropKeyboardLoupeScreenPos = null;

        // キーボードで始点が調整済みなら、その位置を維持する
        if (_cropStartPointSet && _cropRect.Width <= 0 && _cropRect.Height <= 0)
        {
            // 始点はそのまま（キーボード調整位置）、ドラッグ開始
            _cropRect = new Rect(_cropStartPoint, new Size(0, 0));
            _cropBaseRect = _cropRect;
        }
        else
        {
            _cropStartPointSet = true;
            _cropStartPoint = pos;
            _cropRect = new Rect(pos, new Size(0, 0));
            _cropBaseRect = _cropRect;
        }
        UpdateCropOverlay();
    }

    /// <summary>画像上でPointer移動。</summary>
    private void OnCropPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewImageControl);
        var pos = e.GetPosition(PreviewImageControl);

        if (!point.Properties.IsLeftButtonPressed)
        {
            // マウス移動時はキーボード微調整をキャンセル
            _cropBaseRect = _cropRect;
            _cropKeyboardOffsetX = 0;
            _cropKeyboardOffsetY = 0;
            _cropKeyboardLoupeScreenPos = null;

            // ボタン未押下 → ハンドル/領域検出でカーソル形状を変更
            if (_cropModeActive && _cropRect.Width >= HandleHitRadius && _cropRect.Height >= HandleHitRadius)
            {
                UpdateCursorForHandle(pos);
            }
            else if (_cropModeActive)
            {
                _cropCursorOverride = _hCurCross; // 切出しモード中（選択なし）→ 十字
            }
            else
            {
                _cropCursorOverride = IntPtr.Zero; // 通常モード → デフォルト
            }
            return;
        }

        // アクティブハンドルによるリサイズ/移動
        if (_activeHandle != CropDragHandle.None)
        {
            double dx = pos.X - _dragStartPoint.X;
            double dy = pos.Y - _dragStartPoint.Y;
            _cropRect = ComputeDragRect(_dragStartRect, dx, dy, _activeHandle);
            _cropBaseRect = _cropRect;
            _cropKeyboardOffsetX = 0;
            _cropKeyboardOffsetY = 0;
            _cropKeyboardLoupeScreenPos = null;
            UpdateCropOverlay();
            return;
        }

        // 新規選択作成（キーボードオフセットで終点を微調整）
        _cropDragInProgress = true;
        _cropLastDragPos = pos;
        var effectivePos = new Avalonia.Point(pos.X + _cropKeyboardOffsetX, pos.Y + _cropKeyboardOffsetY);
        _cropRect = new Rect(
            Math.Min(_cropStartPoint.X, effectivePos.X),
            Math.Min(_cropStartPoint.Y, effectivePos.Y),
            Math.Abs(effectivePos.X - _cropStartPoint.X),
            Math.Abs(effectivePos.Y - _cropStartPoint.Y));
        _cropBaseRect = _cropRect;
        UpdateCropOverlay();
    }

    /// <summary>画像上でPointer解放。</summary>
    private void OnCropPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // リサイズ/移動中はハンドル解除のみ
        if (_activeHandle != CropDragHandle.None)
        {
            _activeHandle = CropDragHandle.None;
            UpdateCropOverlay();
            return;
        }

        // サイズが小さすぎる場合はリセット
        if (_cropRect.Width < 5 || _cropRect.Height < 5)
        {
            if (_autoCropStarted)
            {
                // 単一クリック（ドラッグなし）→ 始点モード維持、切出しモードは終了しない
                _autoCropStarted = false;
                _cropRect = new Rect(_cropStartPoint, new Size(0, 0));
                _cropBaseRect = _cropRect;
                UpdateCropOverlay();
            }
            else
            {
                _cropRect = new Rect();
                UpdateCropOverlay();
            }
            return;
        }

        // 新規選択のドラッグ完了
        _cropDragInProgress = false;
        _cropBaseRect = _cropRect;
        _cropKeyboardOffsetX = 0;
        _cropKeyboardOffsetY = 0;

        // 自動開始の場合はここで確定はせず、オーバーレイは残す
        if (_autoCropStarted)
        {
            _autoCropStarted = false;
        }
        UpdateCropOverlay();
    }

    /// <summary>ポインタが画像プレビューエリアから離れたときにカーソルオーバーライドを解除する。</summary>
    private void OnCropPointerExited(object? sender, PointerEventArgs e)
    {
        _cropCursorOverride = IntPtr.Zero;
    }

    /// <summary>指定座標がどのドラッグハンドルに当たるかを判定する。</summary>
    private static CropDragHandle HitTestHandle(Point pos, Rect rect)
    {
        if (IsNear(pos, rect.TopLeft, HandleHitRadius)) return CropDragHandle.TopLeft;
        if (IsNear(pos, rect.TopRight, HandleHitRadius)) return CropDragHandle.TopRight;
        if (IsNear(pos, rect.BottomLeft, HandleHitRadius)) return CropDragHandle.BottomLeft;
        if (IsNear(pos, rect.BottomRight, HandleHitRadius)) return CropDragHandle.BottomRight;
        if (IsNear(pos, new Point(rect.Center.X, rect.Top), HandleHitRadius)) return CropDragHandle.TopCenter;
        if (IsNear(pos, new Point(rect.Center.X, rect.Bottom), HandleHitRadius)) return CropDragHandle.BottomCenter;
        if (IsNear(pos, new Point(rect.Left, rect.Center.Y), HandleHitRadius)) return CropDragHandle.MiddleLeft;
        if (IsNear(pos, new Point(rect.Right, rect.Center.Y), HandleHitRadius)) return CropDragHandle.MiddleRight;
        if (rect.Contains(pos)) return CropDragHandle.Move;
        return CropDragHandle.None;
    }

    /// <summary>2点が指定半径以内か判定する。</summary>
    private static bool IsNear(Point p1, Point p2, double radius)
        => Math.Abs(p1.X - p2.X) <= radius && Math.Abs(p1.Y - p2.Y) <= radius;

    /// <summary>ドラッグハンドル種別に対応する Win32 カーソルハンドルを取得する。</summary>
    private static IntPtr GetCursorHandle(CropDragHandle handle) => handle switch
    {
        CropDragHandle.TopLeft or CropDragHandle.BottomRight => _hCurSizeNWSE,
        CropDragHandle.TopRight or CropDragHandle.BottomLeft => _hCurSizeNESW,
        CropDragHandle.TopCenter or CropDragHandle.BottomCenter => _hCurSizeNS,
        CropDragHandle.MiddleLeft or CropDragHandle.MiddleRight => _hCurSizeWE,
        CropDragHandle.Move => _hCurSizeAll,
        _ => IntPtr.Zero,
    };

    /// <summary>ハンドル位置に応じて <see cref="_cropCursorOverride"/> を更新する。</summary>
    private void UpdateCursorForHandle(Point pos)
    {
        var handle = HitTestHandle(pos, _cropRect);
        _cropCursorOverride = handle != CropDragHandle.None
            ? GetCursorHandle(handle)
            : _hCurSizeAll; // 選択領域の内側（ハンドル以外）→ 十字矢印
    }

    /// <summary>ドラッグハンドル操作による矩形の変形を計算する。</summary>
    private static Rect ComputeDragRect(Rect original, double dx, double dy, CropDragHandle handle)
    {
        double l = original.Left;
        double t = original.Top;
        double r = original.Right;
        double b = original.Bottom;

        switch (handle)
        {
            case CropDragHandle.TopLeft:         l += dx; t += dy; break;
            case CropDragHandle.TopCenter:       t += dy; break;
            case CropDragHandle.TopRight:        r += dx; t += dy; break;
            case CropDragHandle.MiddleLeft:      l += dx; break;
            case CropDragHandle.MiddleRight:     r += dx; break;
            case CropDragHandle.BottomLeft:      l += dx; b += dy; break;
            case CropDragHandle.BottomCenter:    b += dy; break;
            case CropDragHandle.BottomRight:     r += dx; b += dy; break;
            case CropDragHandle.Move:            l += dx; t += dy; r += dx; b += dy; break;
        }

        // 最小サイズを確保してフリップを防止
        if (r - l < MinCropSize)
        {
            if (handle is CropDragHandle.TopLeft or CropDragHandle.MiddleLeft or CropDragHandle.BottomLeft)
                l = r - MinCropSize;
            else
                r = l + MinCropSize;
        }
        if (b - t < MinCropSize)
        {
            if (handle is CropDragHandle.TopLeft or CropDragHandle.TopCenter or CropDragHandle.TopRight)
                t = b - MinCropSize;
            else
                b = t + MinCropSize;
        }

        return new Rect(l, t, r - l, b - t);
    }

    /// <summary>切出しオーバーレイ（暗転マスク＋選択枠＋ハンドル）を更新する。</summary>
    /// <remarks>
    /// 切出しモード中は全面を暗転させる。<br/>
    /// 選択範囲がある場合は、選択範囲のみ完全透明（＝元の画像が透けて見える）になるよう<br/>
    /// 周囲4辺に暗転矩形を配置する「穴あけマスク」方式を採用する。<br/>
    /// ドラッグ中は <c>InvalidateVisual</c> ではなく <c>Canvas.Children</c> の追加削除で描画する。<br/>
    /// </remarks>
    private void UpdateCropOverlay()
    {
        CropOverlay.Children.Clear();

        if (!_cropModeActive) return;

        // PreviewImageControl の CropOverlay に対する位置を取得
        var origin = PreviewImageControl.TranslatePoint(default, CropOverlay);
        if (!origin.HasValue) return;

        double ox = origin.Value.X;
        double oy = origin.Value.Y;
        double cw = CropOverlay.Bounds.Width;
        double ch = CropOverlay.Bounds.Height;

        var settings = _settingsService.Current;
        // 暗転用ブラシ（アルファ160 = 63%不透明で確実に暗くする）
        var overlayBrush = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
        var borderColor = Color.TryParse(settings.CaptureBorderColor, out var bc) ? bc : Colors.White;
        var borderBrush = new SolidColorBrush(borderColor, Math.Clamp(settings.CaptureBorderOpacity, 0.1, 1.0));

        // 選択範囲なし
        if (_cropRect.Width < 1 || _cropRect.Height < 1)
        {
            if (_cropModeActive)
            {
                // 始点のみ設定されている → 全面暗転＋始点に十字マーカー
                AddOverlayRect(0, 0, cw, ch, overlayBrush);
                var cx = ox + _cropStartPoint.X;
                var cy = oy + _cropStartPoint.Y;
                const double markerSize = 8;
                var markerColor = new SolidColorBrush(Colors.White);
                CropOverlay.Children.Add(new Line
                {
                    StartPoint = new Avalonia.Point(cx - markerSize, cy),
                    EndPoint = new Avalonia.Point(cx + markerSize, cy),
                    Stroke = markerColor,
                    StrokeThickness = 2,
                });
                CropOverlay.Children.Add(new Line
                {
                    StartPoint = new Avalonia.Point(cx, cy - markerSize),
                    EndPoint = new Avalonia.Point(cx, cy + markerSize),
                    Stroke = markerColor,
                    StrokeThickness = 2,
                });
            }
            else
            {
                AddOverlayRect(0, 0, cw, ch, overlayBrush);
            }
            return;
        }

        double sx = ox + _cropRect.X;
        double sy = oy + _cropRect.Y;
        double sw = _cropRect.Width;
        double sh = _cropRect.Height;

        // 穴あけマスク：選択範囲の周囲4辺のみ暗転、選択範囲は完全透明
        if (sy > 0)
            AddOverlayRect(0, 0, cw, sy, overlayBrush);                                  // 上
        if (sy + sh < ch)
            AddOverlayRect(0, sy + sh, cw, ch - sy - sh, overlayBrush);                  // 下
        if (sx > 0)
            AddOverlayRect(0, sy, sx, sh, overlayBrush);                                 // 左
        if (sx + sw < cw)
            AddOverlayRect(sx + sw, sy, cw - sx - sw, sh, overlayBrush);                 // 右

        // 選択枠
        CropOverlay.Children.Add(new Rectangle
        {
            Stroke = borderBrush,
            StrokeThickness = 2,
            Fill = null,
            [Canvas.LeftProperty] = sx,
            [Canvas.TopProperty] = sy,
            Width = sw,
            Height = sh,
        });

        // 左上に枠サイズ表示
        var sizeText = $"{(int)_cropRect.Width} x {(int)_cropRect.Height}";
        var fontSize = 14.0;
        var ft = new FormattedText(
            sizeText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            fontSize,
            Brushes.White);
        var bgAlpha = (byte)Math.Clamp(settings.CaptureOverlayOpacity * 255, 0, 255);
        var textBg = new SolidColorBrush(Color.FromArgb(bgAlpha, 0, 0, 0));
        var textPad = 4.0;
        var textRect = new Rect(sx + textPad, sy + textPad, ft.Width + textPad * 2, ft.Height + textPad * 2);
        CropOverlay.Children.Add(new Rectangle
        {
            Fill = textBg,
            [Canvas.LeftProperty] = textRect.X,
            [Canvas.TopProperty] = textRect.Y,
            Width = textRect.Width,
            Height = textRect.Height,
        });
        CropOverlay.Children.Add(new TextBlock
        {
            Text = sizeText,
            Foreground = Brushes.White,
            FontSize = fontSize,
            FontFamily = new FontFamily("Consolas"),
            [Canvas.LeftProperty] = textRect.X + textPad,
            [Canvas.TopProperty] = textRect.Y + textPad,
        });

        // 8つのドラッグハンドル（四隅＋四辺中点）
        var handleFill = new SolidColorBrush(Colors.White);
        double hs = HandleSize;
        AddHandle(sx - hs / 2, sy - hs / 2, hs, hs, handleFill, borderBrush, 1.5);              // TL
        AddHandle(sx + sw / 2 - hs / 2, sy - hs / 2, hs, hs, handleFill, borderBrush, 1.5);    // TC
        AddHandle(sx + sw - hs / 2, sy - hs / 2, hs, hs, handleFill, borderBrush, 1.5);        // TR
        AddHandle(sx - hs / 2, sy + sh / 2 - hs / 2, hs, hs, handleFill, borderBrush, 1.5);    // ML
        AddHandle(sx + sw - hs / 2, sy + sh / 2 - hs / 2, hs, hs, handleFill, borderBrush, 1.5); // MR
        AddHandle(sx - hs / 2, sy + sh - hs / 2, hs, hs, handleFill, borderBrush, 1.5);        // BL
        AddHandle(sx + sw / 2 - hs / 2, sy + sh - hs / 2, hs, hs, handleFill, borderBrush, 1.5); // BC
        AddHandle(sx + sw - hs / 2, sy + sh - hs / 2, hs, hs, handleFill, borderBrush, 1.5);    // BR
    }

    /// <summary>オーバーレイ用の半透明矩形を追加する。</summary>
    private void AddOverlayRect(double x, double y, double w, double h, SolidColorBrush brush)
    {
        if (w <= 0 || h <= 0) return;
        CropOverlay.Children.Add(new Rectangle
        {
            Fill = brush,
            [Canvas.LeftProperty] = x,
            [Canvas.TopProperty] = y,
            Width = w,
            Height = h,
        });
    }

    /// <summary>ドラッグハンドル用の矩形を追加する。</summary>
    private void AddHandle(double x, double y, double w, double h, IBrush fill, IBrush stroke, double strokeThickness)
    {
        CropOverlay.Children.Add(new Rectangle
        {
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            Width = w,
            Height = h,
            [Canvas.LeftProperty] = x,
            [Canvas.TopProperty] = y,
        });
    }

    /// <summary>右クリックのコンテキストメニューを表示する。</summary>
    private void ShowCropContextMenu()
    {
        var menu = new ContextMenu();
        var confirmItem = new MenuItem { Header = "切出し確定" };
        confirmItem.Click += (_, _) => { if (_cropRect.Width >= MinCropSize && _cropRect.Height >= MinCropSize) ExecuteCrop(); };
        var cancelItem = new MenuItem { Header = "キャンセル" };
        cancelItem.Click += (_, _) => ExitCropMode();
        menu.Items.Add(confirmItem);
        menu.Items.Add(cancelItem);
        menu.Open(CropOverlay);
    }

    /// <summary>切り出し選択矩形を描画する（従来のRenderはCanvasベースに移行）。</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
    }

    // ===================== Undo =====================

    /// <summary>現在の画像をUndoスタックにプッシュする（最大5段階）。</summary>
    private void PushUndo()
    {
        if (_isUndoing) return;

        if (_viewModel.PreviewImage is not null)
        {
            using var ms = new MemoryStream();
            _viewModel.PreviewImage.Save(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var copy = new Avalonia.Media.Imaging.Bitmap(ms);
            _undoStack.Add(copy);
            if (_undoStack.Count > MaxUndoCount)
            {
                _undoStack[0].Dispose();
                _undoStack.RemoveAt(0);
            }
        }
    }

    /// <summary>Undoスタックから画像を復元する。</summary>
    private void RestoreUndo()
    {
        if (_undoStack.Count == 0) return;

        _isUndoing = true;
        var last = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _viewModel.PreviewImage?.Dispose();
        _viewModel.SetPreviewImage(last);
        // Undo後は元の100%画像が不明になるため
        // _originalCaptureSource/bounds は OnPreviewImageChanged で再生成される
        _originalCaptureBounds = null;
        _originalCaptureSource?.Dispose();
        _originalCaptureSource = null;
        _isUndoing = false;
    }

    // ===================== ルーペ =====================

    private void ToggleLoupe()
    {
        _isLoupeVisible = !_isLoupeVisible;
        LoupeBorder.IsVisible = _isLoupeVisible;

        if (_isLoupeVisible)
        {
            StartLoupeUpdate();
        }
        else
        {
            StopLoupeUpdate();
        }
    }

    private void StartLoupeUpdate()
    {
        _loupeCts?.Cancel();
        _loupeCts = new CancellationTokenSource();
        _ = UpdateLoupeLoopAsync(_loupeCts.Token);
    }

    private void StopLoupeUpdate()
    {
        _loupeCts?.Cancel();
        _loupeCts = null;
        LoupeBorder.IsVisible = false;
    }

    private async Task UpdateLoupeLoopAsync(CancellationToken ct)
    {
        // メモリ上の100%元画像から切り出してルーペ描画（画面再キャプチャは行わない）
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 画像抽出用の座標（キーボード微調整時は始点/終点、通常はマウスカーソル）
                var loupeSourcePos = _cropKeyboardLoupeScreenPos ?? GetCursorScreenPoint();
                // ルーペ枠の配置用の座標（常にマウスカーソルに追従）
                var loupeBorderPos = GetCursorScreenPoint();
                var settings = _settingsService.Current;

                // ルーペ全体で必要なソースピクセル数
                var srcSize = (int)Math.Ceiling((double)settings.LoupeSize / settings.LoupeZoomLevel);

                // メモリ上の元画像から切り出し（画像外なら null）
                using var croppedRegion = ExtractSourceRegion(loupeSourcePos, srcSize);

                // ルーペの描画を UI スレッドで実行（画像ありは拡大描画、なしは市松模様）
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Bitmap loupeBmp;
                    if (croppedRegion is not null)
                    {
                        loupeBmp = LoupeDrawingHelper.DrawLoupe(
                            croppedRegion,
                            settings.LoupeSize,
                            settings.LoupeZoomLevel,
                            settings.LoupeCrossColor,
                            settings.LoupeCrossOpacity,
                            settings.PreviewCheckerSize);
                    }
                    else
                    {
                        // 画像がない / 画像表示領域外 → 市松模様のみ
                        loupeBmp = LoupeDrawingHelper.DrawLoupeCheckerboardOnly(
                            settings.LoupeSize,
                            settings.LoupeCrossColor,
                            settings.LoupeCrossOpacity,
                            settings.PreviewCheckerSize);
                    }

                    LoupeImage.Source = loupeBmp;
                    LoupeBorder.IsVisible = true;

                    // 枠の位置は常にマウスカーソルに追従（キーボード微調整中でもカーソル位置に配置）
                    if (LoupeBorder.Parent is Visual parentVisual)
                    {
                        var loupeSize = settings.LoupeSize;
                        var scaling = RenderScaling;
                        var parentScreenOrigin = parentVisual.PointToScreen(default);
                        var parentWidth = parentVisual.Bounds.Width;
                        var parentHeight = parentVisual.Bounds.Height;

                        // 物理ピクセル → DIP 変換後、カーソルの右下に固定オフセットで配置
                        var offset = 20.0;
                        double relX = Math.Clamp(
                            (loupeBorderPos.X - parentScreenOrigin.X) / scaling + offset,
                            0.0, parentWidth - loupeSize);
                        double relY = Math.Clamp(
                            (loupeBorderPos.Y - parentScreenOrigin.Y) / scaling + offset,
                            0.0, parentHeight - loupeSize);

                        LoupeBorder.Margin = new Thickness(relX, relY, 0, 0);
                    }
                });

                // 50ms ごとに更新
                await Task.Delay(50, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(100, ct);
            }
        }
    }

    /// <summary>
    /// メモリ上の100%元画像からカーソル位置周辺の <paramref name="srcSize"/>×<paramref name="srcSize"/> 領域を切り出す。
    /// </summary>
    /// <remarks>
    /// 画面を再キャプチャせずにメモリ内の <see cref="_originalCaptureSource"/> からクローンする。<br/>
    /// カーソル位置は Win32 <c>GetCursorPos</c>（物理ピクセル）で取得し、<br/>
    /// <c>PreviewImageControl.PointToScreen</c> で画像コントロールの画面位置を求め、<br/>
    /// <c>Bounds × RenderScaling</c> で表示物理サイズを計算して元画像ピクセル座標に変換する。<br/>
    /// これにより、画像の表示拡大率（Fit/ Fill/任意%）やディスプレイのDPI倍率に依存せず
    /// 常に正しいピクセルを切り出せる。<br/>
    /// カーソルが画像表示領域外にある場合や、元画像がない場合は <c>null</c> を返す（
    /// 呼び出し元で市松模様表示に切り替える）。<br/>
    /// </remarks>
    private System.Drawing.Bitmap? ExtractSourceRegion(PixelPoint cursorPos, int srcSize)
    {
        // メモリ上の元画像がなければ画像がない → null（市松模様を表示）
        if (_originalCaptureSource is null) return null;

        // PreviewImageControl の画面上の位置（物理ピクセル）を取得
        var imgScreenOrigin = PreviewImageControl.PointToScreen(default);
        int relScreenX = cursorPos.X - imgScreenOrigin.X;
        int relScreenY = cursorPos.Y - imgScreenOrigin.Y;

        var imgBounds = PreviewImageControl.Bounds;
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0) return null;

        // RenderScaling = ウィンドウのDPI倍率（1.0=96dpi, 1.5=144dpi）
        var scaling = RenderScaling;
        double displayPxW = imgBounds.Width * scaling;
        double displayPxH = imgBounds.Height * scaling;
        if (displayPxW <= 0 || displayPxH <= 0) return null;

        // カーソルが画像コントロールの表示領域外なら画像表示領域外 → null
        if (relScreenX < 0 || relScreenY < 0 ||
            relScreenX >= displayPxW || relScreenY >= displayPxH)
            return null;

        // 物理ピクセル上の表示サイズ → 元画像ピクセル へのスケール
        double scaleX = _originalCaptureSource.Width / displayPxW;
        double scaleY = _originalCaptureSource.Height / displayPxH;

        int bmpX = (int)(relScreenX * scaleX);
        int bmpY = (int)(relScreenY * scaleY);

        // カーソル位置が元画像のピクセル範囲外なら null（市松模様）
        if (bmpX < 0 || bmpX >= _originalCaptureSource.Width ||
            bmpY < 0 || bmpY >= _originalCaptureSource.Height)
            return null;

        // 理想的な切り出し開始位置（カーソルを中心に srcSize×srcSize）
        int idealCropX = bmpX - srcSize / 2;
        int idealCropY = bmpY - srcSize / 2;

        // 常に srcSize×srcSize のビットマップを作成し、
        // 画像範囲外のピクセルは透明（0,0,0,0）で埋める → DrawLoupe でスキップして市松模様が透ける
        var sourceW = _originalCaptureSource.Width;
        var sourceH = _originalCaptureSource.Height;
        var result = new System.Drawing.Bitmap(srcSize, srcSize,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var srcData = _originalCaptureSource.LockBits(
            new System.Drawing.Rectangle(0, 0, sourceW, sourceH),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(
            new System.Drawing.Rectangle(0, 0, srcSize, srcSize),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var srcStride = srcData.Stride;
        var dstStride = dstData.Stride;
        var srcPixels = new byte[srcStride * sourceH];
        var dstPixels = new byte[dstStride * srcSize];
        Marshal.Copy(srcData.Scan0, srcPixels, 0, srcPixels.Length);

        // 初期状態は全て透明（0,0,0,0） ← new byte[] のデフォルト値
        // 画像範囲内のピクセルのみコピー
        for (var dy = 0; dy < srcSize; dy++)
        {
            int srcY = idealCropY + dy;
            if (srcY < 0 || srcY >= sourceH) continue;

            for (var dx = 0; dx < srcSize; dx++)
            {
                int srcX = idealCropX + dx;
                if (srcX < 0 || srcX >= sourceW) continue;

                var srcOff = srcY * srcStride + srcX * 4;
                var dstOff = dy * dstStride + dx * 4;
                dstPixels[dstOff] = srcPixels[srcOff];
                dstPixels[dstOff + 1] = srcPixels[srcOff + 1];
                dstPixels[dstOff + 2] = srcPixels[srcOff + 2];
                dstPixels[dstOff + 3] = srcPixels[srcOff + 3];
            }
        }

        Marshal.Copy(dstPixels, 0, dstData.Scan0, dstPixels.Length);
        result.UnlockBits(dstData);
        _originalCaptureSource.UnlockBits(srcData);

        return result;
    }

    private static PixelPoint GetCursorScreenPoint()
    {
        // Win32 GetCursorPos の呼び出し
        GetCursorPos(out var pt);
        return new PixelPoint(pt.X, pt.Y);
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Win32Point lpPoint);

    /// <summary>Win32 POINT 構造体（blittable 型）。</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Win32Point
    {
        /// <summary>X 座標。</summary>
        public int X;
        /// <summary>Y 座標。</summary>
        public int Y;
    }

    // ===================== ズーム =====================

    private void ZoomIn()
    {
        var current = _viewModel.ZoomPercent;
        var levels = _viewModel.ZoomLevels;
        var idx = Array.IndexOf(levels, current);
        if (idx < levels.Length - 1)
        {
            _viewModel.ZoomPercent = levels[idx + 1];
            ApplyZoom();
        }
    }

    private void ZoomOut()
    {
        var current = _viewModel.ZoomPercent;
        var levels = _viewModel.ZoomLevels;
        var idx = Array.IndexOf(levels, current);
        if (idx > 0)
        {
            _viewModel.ZoomPercent = levels[idx - 1];
            ApplyZoom();
        }
    }

    private void ApplyZoom()
    {
        var zoom = _viewModel.ZoomPercent;
        var maxLevel = _viewModel.ZoomLevels[^1];

        if (zoom <= 0 || _viewModel.PreviewImage is null)
        {
            // Fit 表示: ScrollViewer のビューポートサイズを MaxWidth/MaxHeight に設定
            PreviewImageControl.Stretch = Avalonia.Media.Stretch.Uniform;
            PreviewImageControl.MaxWidth = ImageScrollViewer.Viewport.Width;
            PreviewImageControl.MaxHeight = ImageScrollViewer.Viewport.Height;
            PreviewImageControl.Width = double.NaN;
            PreviewImageControl.Height = double.NaN;
            UpdateImageAlignment();
        }
        else
        {
            // Fill で明示サイズにフィットさせる（None だと画像が拡大縮小されない）
            PreviewImageControl.Stretch = Avalonia.Media.Stretch.Fill;
            PreviewImageControl.MaxWidth = double.PositiveInfinity;
            PreviewImageControl.MaxHeight = double.PositiveInfinity;
            PreviewImageControl.Width = _viewModel.PreviewImage.Size.Width * zoom / 100.0;
            PreviewImageControl.Height = _viewModel.PreviewImage.Size.Height * zoom / 100.0;
            UpdateImageAlignment();
        }

        _viewModel.CanZoomIn = zoom < maxLevel;
        _viewModel.CanZoomOut = zoom > 0;
    }

    /// <summary>ScrollViewer リサイズ時に Fit モードなら MaxWidth/MaxHeight を更新する。</summary>
    private void OnImageScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_viewModel.ZoomPercent <= 0 && _viewModel.PreviewImage is not null)
        {
            PreviewImageControl.MaxWidth = e.NewSize.Width;
            PreviewImageControl.MaxHeight = e.NewSize.Height;
        }
    }

    /// <summary>ComboBox 選択時に ZoomPercent を更新する。</summary>
    private void OnZoomComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingZoom) return;
        if (sender is ComboBox { SelectedIndex: >= 0 } cb)
        {
            var levels = _viewModel.ZoomLevels;
            if (cb.SelectedIndex < levels.Length)
            {
                _viewModel.ZoomPercent = levels[cb.SelectedIndex];
            }
        }
    }

    private bool _isSyncingZoom;

    /// <summary>ZoomPercent の変更を ComboBox の選択に反映する。</summary>
    private void SyncZoomComboBox()
    {
        if (_isSyncingZoom) return;
        _isSyncingZoom = true;
        try
        {
            var idx = Array.IndexOf(_viewModel.ZoomLevels, _viewModel.ZoomPercent);
            if (idx >= 0 && ZoomComboBox.SelectedIndex != idx)
            {
                ZoomComboBox.SelectedIndex = idx;
            }
        }
        finally
        {
            _isSyncingZoom = false;
        }
    }

    /// <summary>中央寄せ設定に応じて画像の水平・垂直配置を更新する。</summary>
    private void UpdateImageAlignment()
    {
        var isCenter = _viewModel.Settings.CenterAlign;
        PreviewImageControl.HorizontalAlignment = isCenter
            ? Avalonia.Layout.HorizontalAlignment.Center
            : Avalonia.Layout.HorizontalAlignment.Left;
        PreviewImageControl.VerticalAlignment = isCenter
            ? Avalonia.Layout.VerticalAlignment.Center
            : Avalonia.Layout.VerticalAlignment.Top;
    }

    // ===================== キーボードショートカット =====================

    /// <summary>マウスカーソルがプレビュー画像領域内にあるかを判定する。</summary>
    private bool IsPointerOverPreviewImage()
    {
        var cursorScreen = GetCursorScreenPoint();
        var imgOrigin = PreviewImageControl.PointToScreen(default);
        var scaling = RenderScaling;
        var imgBounds = PreviewImageControl.Bounds;
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0) return false;

        double displayPxW = imgBounds.Width * scaling;
        double displayPxH = imgBounds.Height * scaling;

        double relX = cursorScreen.X - imgOrigin.X;
        double relY = cursorScreen.Y - imgOrigin.Y;

        return relX >= 0 && relY >= 0 && relX < displayPxW && relY < displayPxH;
    }

    /// <summary>切出しモード中のカーソルキーを横取りして捕捉。</summary>
    /// <remarks>
    /// 状態に応じて以下の動作をする：<br/>
    /// - ドラッグ前（始点のみ）→ 始点を移動<br/>
    /// - ドラッグ中（新規選択作成）→ 終点を移動<br/>
    /// - ドラッグ後（矩形確定）→ 選択領域全体を移動<br/>
    /// - ハンドルドラッグ中 → ハンドルに応じてサイズ変更<br/>
    /// プレビュー画像領域外では横取りせず、ComboBox 等のフォーカス制御を優先する。<br/>
    /// 切出しモード未開始でも、プレビュー上なら自動的に切出しモードを開始する。<br/>
    /// </remarks>
    private void OnCropPreviewKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        // プレビュー画像領域外では横取りせず、ComboBox 等のフォーカス制御を優先
        if (!IsPointerOverPreviewImage()) return;

        // 切出しモード未開始でも、プレビュー上なら自動開始（ドラッグ／arrow key のいずれでも）
        if (!_cropModeActive)
        {
            if (_viewModel.PreviewImage is null) return;
            _cropModeActive = true;
            _autoCropStarted = true;
            _cropCursorOverride = _hCurCross;
            Cursor = new Cursor(StandardCursorType.Cross);
            _cropStartPointSet = false;
            _cropKeyboardOffsetX = 0;
            _cropKeyboardOffsetY = 0;
            _cropKeyboardLoupeScreenPos = null;
            _cropRect = new Rect();
            UpdateCropButtonText();
        }

        double step = 1.0;
        double dx = 0, dy = 0;
        switch (e.Key)
        {
            case Key.Left:  dx = -step; break;
            case Key.Right: dx =  step; break;
            case Key.Up:    dy = -step; break;
            case Key.Down:  dy =  step; break;
            default: return;
        }

        if (_activeHandle != CropDragHandle.None)
        {
            // ハンドルドラッグ中 → ハンドルに応じてサイズ変更
            _cropRect = ComputeDragRect(_cropRect, dx, dy, _activeHandle);
            _cropKeyboardLoupeScreenPos = null;
        }
        else if (_cropDragInProgress)
        {
            // ドラッグ中（新規選択）→ 終点移動
            _cropKeyboardOffsetX += dx;
            _cropKeyboardOffsetY += dy;
            var effectivePos = new Avalonia.Point(
                _cropLastDragPos.X + _cropKeyboardOffsetX,
                _cropLastDragPos.Y + _cropKeyboardOffsetY);
            _cropRect = new Rect(
                Math.Min(_cropStartPoint.X, effectivePos.X),
                Math.Min(_cropStartPoint.Y, effectivePos.Y),
                Math.Abs(effectivePos.X - _cropStartPoint.X),
                Math.Abs(effectivePos.Y - _cropStartPoint.Y));
            _cropBaseRect = _cropRect;
            // 終点の画面座標をルーペ表示位置として設定
            _cropKeyboardLoupeScreenPos = PreviewImageControl.PointToScreen(effectivePos);
        }
        else if (_cropRect.Width > 0 && _cropRect.Height > 0)
        {
            // ドラッグ完了後 → 選択領域全体移動
            _cropKeyboardOffsetX += dx;
            _cropKeyboardOffsetY += dy;
            _cropRect = new Rect(
                _cropBaseRect.X + _cropKeyboardOffsetX,
                _cropBaseRect.Y + _cropKeyboardOffsetY,
                _cropBaseRect.Width,
                _cropBaseRect.Height);
            _cropKeyboardLoupeScreenPos = null;
        }
        else
        {
            // ドラッグ前（始点未設定／始点のみ）→ 始点移動
            // 始点が未設定の場合は現在のマウスカーソル位置で初期化
            if (!_cropStartPointSet)
            {
                var cursorScreen = GetCursorScreenPoint();
                var imgOrigin = PreviewImageControl.PointToScreen(default);
                var scaling = RenderScaling;
                var mouseX = (cursorScreen.X - imgOrigin.X) / scaling;
                var mouseY = (cursorScreen.Y - imgOrigin.Y) / scaling;
                var imgBounds = PreviewImageControl.Bounds;
                if (mouseX >= 0 && mouseX < imgBounds.Width &&
                    mouseY >= 0 && mouseY < imgBounds.Height)
                {
                    _cropStartPoint = new Avalonia.Point(mouseX, mouseY);
                }
                _cropStartPointSet = true;
                _cropModeActive = true;
            }
            _cropStartPoint = new Avalonia.Point(
                _cropStartPoint.X + dx,
                _cropStartPoint.Y + dy);
            _cropRect = new Rect(_cropStartPoint, new Size(0, 0));
            _cropBaseRect = _cropRect;
            // 始点の画面座標をルーペ表示位置として設定
            _cropKeyboardLoupeScreenPos = PreviewImageControl.PointToScreen(_cropStartPoint);
        }

        UpdateCropOverlay();
        e.Handled = true;
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            switch (e.Key)
            {
            case Key.C:
                    _ = _viewModel.CopyToClipboard();
                    e.Handled = true;
                    break;
                case Key.V:
                    // ペースト後は元のキャプチャ画像とは無関係になるため
                    // _originalCaptureSource/bounds は OnPreviewImageChanged で再生成される
                    _originalCaptureBounds = null;
                    _originalCaptureSource?.Dispose();
                    _originalCaptureSource = null;
                    _ = _viewModel.PasteFromClipboard();
                    e.Handled = true;
                    break;
                case Key.S:
                    _ = _viewModel.SaveImageAsync();
                    e.Handled = true;
                    break;
                case Key.Z:
                    RestoreUndo();
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                case Key.Add:
                    ZoomIn();
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    ZoomOut();
                    e.Handled = true;
                    break;
            }
        }

        // 切出しモード中にEscape → キャンセル
        if (e.Key == Key.Escape && _cropModeActive)
        {
            ExitCropMode();
            e.Handled = true;
        }
    }

    /// <summary>Ctrl+マウスホイールでズームイン／アウト。</summary>
    private void OnImagePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            if (e.Delta.Y > 0) ZoomIn();
            else if (e.Delta.Y < 0) ZoomOut();
            e.Handled = true;
        }
    }

    // ===================== ダイアログ表示 =====================

    private void ShowAboutDialog()
    {
        var about = new Window
        {
            Title = "バージョン情報",
            Width = 400, Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = "SaveEvidence", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = "Version 1.0.0", Margin = new Thickness(0, 10, 0, 0) },
                    new TextBlock { Text = "画面キャプチャツール", Margin = new Thickness(0, 5, 0, 0) },
                    new TextBlock { Text = "Avalonia UI 版", Margin = new Thickness(0, 5, 0, 0) },
                }
            }
        };
        about.ShowDialog(this);
    }

    /// <summary>簡易確認ダイアログを表示する。</summary>
    private async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new Window
        {
            Title = title,
            Width = 420, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.BorderOnly,
        };

        var yesButton = new Button { Content = "はい", Width = 80, Margin = new Thickness(5) };
        var noButton = new Button { Content = "いいえ", Width = 80, Margin = new Thickness(5) };

        yesButton.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        noButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Children = { yesButton, noButton }
                }
            }
        };

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }
}

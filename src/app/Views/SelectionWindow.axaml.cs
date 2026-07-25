using System.Globalization;
using System.Runtime.InteropServices;
using app.Converters;
using app.Enum;
using app.Helpers;
using app.Models;
using app.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace app.Views;

/// <summary>
/// 画面キャプチャ用の選択オーバーレイウィンドウ。
/// </summary>
/// <remarks>
/// 全画面表示の透明ウィンドウとして動作し、マウス操作による領域選択や
/// ウィンドウ選択を実現する。<br/>
/// 3種類のキャプチャモード（全画面、ウィンドウ選択、範囲選択）に対応する。<br/>
/// Win32 API（EnumWindows, PrintWindow 等）を使用して実際のキャプチャを実行する。<br/>
/// </remarks>
public sealed partial class SelectionWindow : Window
{
    private readonly CaptureType _captureType;
    private readonly Action<System.Drawing.Bitmap?, System.Drawing.Rectangle?> _onComplete;
    private Avalonia.Point _startPoint;
    private Rect _selectionRect;
    private bool _isSelecting;
    private bool _hasSelection;

    // WindowSelect のハイライト用
    private Rect _highlightRect;
    private bool _hasHighlight;

    // 再キャプチャ防止（Hide()→Close() 後にウィンドウが生き残る事象への対策）
    private bool _isCapturing;
    private bool _hasCompleted;

    // 選択枠描画用のペンとブラシ
    private static readonly SolidColorBrush s_overlayBrush = new(Color.FromArgb(60, 0, 0, 0));

    private readonly Pen _selectionPen;
    private readonly string _borderColor;
    private readonly double _borderOpacity;
    private readonly double _textBgOpacity;
    private readonly double _scaling;

    // SelectScreen の現在のスクリーンインデックス（-1=なし）
    private int _screenIndex = -1;
    // WindowSelect の選択中ウィンドウタイトル
    private string _windowTitle = "";

    // Fixモード用の個別スクリーンキャプチャ（オーバーレイ表示時に各スクリーンを個別に静止画キャプチャ）
    private readonly List<(System.Drawing.Rectangle Bounds, System.Drawing.Bitmap Bitmap)> _screenCaptures = [];
    private readonly List<(System.Drawing.Rectangle Bounds, Avalonia.Media.Imaging.Bitmap Bitmap)> _screenAvaloniaBitmaps = [];

    // 領域選択用拡大鏡
    private Bitmap? _magnifierImage;
    private Avalonia.Point _magnifierCursorPos;
    private DateTime _lastMagnifierUpdate;

    // キーボード微調整用（カーソルキーで選択終点を1px移動）
    private Avalonia.Point _lastMouseDipPos;
    private double _keyboardOffsetDipX;
    private double _keyboardOffsetDipY;
    private readonly int _loupeSize;
    private readonly int _zoomLevel;
    private readonly string _crossColor;
    private readonly double _crossOpacity;
    private readonly int _checkerSize;
    private readonly WindowCaptureMode _captureMode;

    /// <summary>
    /// SelectionWindow を初期化する。
    /// </summary>
    /// <param name="captureType">キャプチャモード</param>
    /// <param name="onComplete">キャプチャ完了時のコールバック</param>
    public SelectionWindow(CaptureType captureType, Action<System.Drawing.Bitmap?, System.Drawing.Rectangle?> onComplete)
    {
        InitializeComponent();
        _captureType = captureType;
        _onComplete = onComplete;

        // 設定から選択枠の色と不透明度を取得
        var settings = Program.ServiceProvider.GetRequiredService<ISettingsService>().Current;
        _borderColor = settings.CaptureBorderColor;
        _borderOpacity = settings.CaptureBorderOpacity;
        _textBgOpacity = settings.CaptureOverlayOpacity;
        _loupeSize = settings.LoupeSize;
        _zoomLevel = settings.LoupeZoomLevel;
        _crossColor = settings.LoupeCrossColor;
        _crossOpacity = settings.LoupeCrossOpacity;
        _checkerSize = settings.PreviewCheckerSize;
        _captureMode = settings.CaptureMode;
        var borderBrush = new SolidColorBrush(Color.TryParse(_borderColor, out var bc) ? bc : Colors.White, _borderOpacity);
        _selectionPen = new Pen(borderBrush, 2);

        // 全画面で表示するため、全てのスクリーンをカバー
        var screens = Screens.All;
        if (screens.Count > 0)
        {
            var bounds = screens[0].Bounds;
            foreach (var screen in screens)
            {
                bounds = bounds.Union(screen.Bounds);
            }
            // DPIスケーリング係数（DIP↔物理ピクセル変換に使用）
            // Screen.Scaling は画面上の DPI 倍率（1.0=96dpi, 1.5=144dpi, 2.0=192dpi）
            _scaling = screens[0].Scaling > 0 ? screens[0].Scaling : 1.0;
            Position = bounds.Position;
            // Width/Height は DIP 単位のため、物理ピクセルから変換する
            Width = bounds.Width / _scaling;
            Height = bounds.Height / _scaling;
        }
        else
        {
            _scaling = 1.0;
        }

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;

        // Fixモードの背景キャプチャは MainWindow.StartSelectionMode から明示的に呼び出されるため、
        // ここでは Opened イベントではキャプチャしない。
    }

    // ────────────────────────────────
    // Win32 P/Invoke (blittable 構造体を使用)
    // ────────────────────────────────

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr WindowFromPoint(POINT pt);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextLengthW(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    /// <summary>Win32 POINT 構造体と互換性のある blittable 構造体。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct POINT(int x, int y)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
    }

    /// <summary>Win32 RECT 構造体と互換性のある blittable 構造体。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RECT(int left, int top, int right, int bottom)
    {
        public int Left { get; } = left;
        public int Top { get; } = top;
        public int Right { get; } = right;
        public int Bottom { get; } = bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    private const uint GA_ROOT = 2;

    /// <summary>ウィンドウの可視矩形（影/透明境界を除外）を取得する。</summary>
    /// <param name="hWnd">ウィンドウハンドル</param>
    /// <param name="rect">出力矩形</param>
    /// <returns>取得成功時 true</returns>
    private static bool GetWindowVisibleRect(IntPtr hWnd, out RECT rect)
    {
        rect = default;
        var hr = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, ref rect, Marshal.SizeOf<RECT>());
        return hr >= 0 && rect.Left < rect.Right && rect.Top < rect.Bottom;
    }

    /// <summary>マウスボタン押下時の処理。</summary>
    private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // ── 再キャプチャ防止ガード ──
            // Hide()→Close() 後にウィンドウが閉じずに生き残り、
            // マウスクリックが再び SelectionWindow に届くことがある。
            // ガードフラグで異なるキャプチャタイプを含む全ての再入を防止する。
            if (_isCapturing || _hasCompleted)
            {
                CaptureLogger.Log("[SelectionWindow] 再キャプチャ防止ガード: isCapturing={0}, hasCompleted={1}", _isCapturing, _hasCompleted);
                return;
            }
            _isCapturing = true;

            // WindowSelect はクリック即キャプチャ
            if (_captureType == CaptureType.WindowSelect)
            {
                CaptureLogger.Log("[SelectionWindow] WindowSelect: PointerPressed");
                // EnumWindows で自分自身を除外してカーソル下のウィンドウを検出
                var hWnd = FindTopWindowUnderCursor();
                CaptureLogger.Log("[SelectionWindow] WindowSelect: hWnd=0x{0:X}", hWnd);
                if (hWnd != IntPtr.Zero)
                {
                    CaptureLogger.Log("[SelectionWindow] WindowSelect: Hide()して50ms待機後にキャプチャ");
                    // オーバーレイ自身や PrintWindow のフラッシュを避けるため非表示にしてからキャプチャ
                    this.Hide();
                    await Task.Delay(50);
                    System.Drawing.Bitmap? bmp;
                    var sourceRect = System.Drawing.Rectangle.Empty;
                    if (GetWindowRect(hWnd, out var wndRect))
                    {
                        var rect = new System.Drawing.Rectangle(wndRect.Left, wndRect.Top, wndRect.Width, wndRect.Height);
                        // 設定に応じてキャプチャ方式を切替
                        if (_captureMode == WindowCaptureMode.PrintWindow)
                        {
                            bmp = CaptureManager.CaptureWindow(hWnd, _captureMode);
                        }
                        else
                        {
                            bmp = ExtractFromFixedBackground(rect);
                        }
                        sourceRect = rect;
                    }
                    else
                    {
                        bmp = null;
                    }
                    CaptureLogger.Log("[SelectionWindow] WindowSelect: capture done, calling _onComplete");
                    _hasCompleted = true;
                    _onComplete(bmp, sourceRect);
                    Close();
                    CaptureLogger.Log("[SelectionWindow] WindowSelect: Close()呼び出し完了");
                    return;
                }
                CaptureLogger.Log("[SelectionWindow] WindowSelect: no window found under cursor");
                _isCapturing = false;
                return;
            }

            // SelectScreen はクリックされたスクリーンを検出してキャプチャ
            if (_captureType == CaptureType.SelectScreen)
            {
                CaptureLogger.Log("[SelectionWindow] SelectScreen: PointerPressed");
                var screenPt = GetCursorScreenPoint();
                foreach (var screen in Screens.All)
                {
                    if (screen.Bounds.Contains(new PixelPoint(screenPt.X, screenPt.Y)))
                    {
                        CaptureLogger.Log("[SelectionWindow] SelectScreen: Hide()して50ms待機後にキャプチャ");
                        // オーバーレイ自身をキャプチャしないよう非表示にしてからキャプチャ
                        this.Hide();
                        await Task.Delay(50);
                        var screenRect = new System.Drawing.Rectangle(
                            screen.Bounds.X, screen.Bounds.Y,
                            screen.Bounds.Width, screen.Bounds.Height);
                        // 事前キャプチャ画像から切り出し
                        var bmp = ExtractFromFixedBackground(screenRect);
                        CaptureLogger.Log("[SelectionWindow] SelectScreen: capture done, calling _onComplete");
                        _hasCompleted = true;
                        _onComplete(bmp, screenRect);
                        CaptureLogger.Log("[SelectionWindow] SelectScreen: Close()呼び出し");
                        Close();
                        CaptureLogger.Log("[SelectionWindow] SelectScreen: Close()完了");
                        return;
                    }
                }
                _isCapturing = false;
                return;
            }

            // 範囲選択（キーボードオフセット反映 → 始点に加算してリセット）
            var clickPos = e.GetPosition(this);
            var adjustedStart = new Avalonia.Point(
                clickPos.X + _keyboardOffsetDipX,
                clickPos.Y + _keyboardOffsetDipY);
            _keyboardOffsetDipX = 0;
            _keyboardOffsetDipY = 0;
            CaptureLogger.Log("[SelectionWindow] AreaSelect: PointerPressed, start=({0:F0},{1:F0})",
                adjustedStart.X, adjustedStart.Y);
            _startPoint = adjustedStart;
            _selectionRect = new Rect(_startPoint, new Size(0, 0));
            _isSelecting = true;
            _hasSelection = false;
            _isCapturing = false; // AreaSelect は PointerReleased まで待つためここで解除
        }
        else if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            // 右クリックでキャンセル
            CancelCapture();
        }
    }

    /// <summary>マウス移動時の処理。</summary>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isSelecting)
        {
            var currentPoint = e.GetPosition(this);
            _lastMouseDipPos = currentPoint;
            // マウス移動時はキーボード微調整をキャンセル
            _keyboardOffsetDipX = 0;
            _keyboardOffsetDipY = 0;
            UpdateSelectionRectAndMagnifier(currentPoint);
        }
        else if (_captureType == CaptureType.AreaSelect)
        {
            // ドラッグ開始前のホバー中も拡大鏡を表示
            // マウス移動時はキーボード微調整をキャンセル
            var currentPoint = e.GetPosition(this);
            _lastMouseDipPos = currentPoint;
            _keyboardOffsetDipX = 0;
            _keyboardOffsetDipY = 0;
            var now = DateTime.UtcNow;
            if ((now - _lastMagnifierUpdate).TotalMilliseconds >= 100)
            {
                UpdateMagnifier(currentPoint);
                _lastMagnifierUpdate = now;
            }
            InvalidateVisual();
        }
        else if (_captureType == CaptureType.WindowSelect)
        {
            // EnumWindows で自分自身を除外してカーソル下のウィンドウを検出しハイライト表示
            var hWnd = FindTopWindowUnderCursor();
            if (hWnd != IntPtr.Zero && GetWindowVisibleRect(hWnd, out var wndRect))
            {
                // ウィンドウタイトルを取得
                _windowTitle = GetWindowTitle(hWnd);
                // 可視矩形（影/透明境界を除外）でハイライト枠を設定
                _highlightRect = new Rect(
                    (wndRect.Left - Position.X) / _scaling,
                    (wndRect.Top - Position.Y) / _scaling,
                    wndRect.Width / _scaling,
                    wndRect.Height / _scaling);
                _hasHighlight = true;
            }
            else
            {
                _hasHighlight = false;
                _windowTitle = "";
            }
            InvalidateVisual();
        }
        else if (_captureType == CaptureType.SelectScreen)
        {
            // マウスカーソル下のスクリーンをハイライト表示
            var screenPt = GetCursorScreenPoint();
            _screenIndex = -1;
            var screens = Screens.All;
            for (int i = 0; i < screens.Count; i++)
            {
                var screen = screens[i];
                if (screen.Bounds.Contains(new PixelPoint(screenPt.X, screenPt.Y)))
                {
                    _screenIndex = i;
                    // Screen.Bounds は物理ピクセル → DIPに変換し、Position(物理ピクセル)からのオフセットを計算
                    _selectionRect = new Rect(
                        (screen.Bounds.X - Position.X) / _scaling,
                        (screen.Bounds.Y - Position.Y) / _scaling,
                        screen.Bounds.Width / _scaling,
                        screen.Bounds.Height / _scaling);
                    _hasSelection = true;
                    InvalidateVisual();
                    break;
                }
            }
        }
    }

    /// <summary>マウスボタン解放時の処理。</summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 拡大鏡をクリア
        ClearMagnifier();

        if (!_isSelecting || !_hasSelection) return;

        _isSelecting = false;

        if (_selectionRect.Width < 10 || _selectionRect.Height < 10)
        {
            CancelCapture();
            return;
        }

        // 選択領域をスクリーン物理ピクセル座標に変換してキャプチャ
        // _selectionRect: e.GetPosition(this)由来のDIP座標
        // Position: Window.Position（物理ピクセル）
        // → DIP部のみ _scaling で物理ピクセルに変換し、Position を加算
        var bounds = new System.Drawing.Rectangle(
            (int)(_selectionRect.X * _scaling + Position.X),
            (int)(_selectionRect.Y * _scaling + Position.Y),
            (int)(_selectionRect.Width * _scaling),
            (int)(_selectionRect.Height * _scaling));
        CaptureLogger.Log("[SelectionWindow] AreaSelect: PointerReleased -> CaptureAndCompleteAsync bounds={0}", bounds);
        _ = CaptureAndCompleteAsync(bounds);
    }

    /// <summary>キー入力処理（Escapeでキャンセル、カーソルキーで選択終点を1px微調整）。</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelCapture();
            return;
        }

        // カーソルキー微調整（1物理ピクセル = 1/_scaling DIP）
        // ドラッグ前は始点調整、ドラッグ中は終点調整
        if (_captureType == CaptureType.AreaSelect)
        {
            double step = 1.0 / _scaling;
            switch (e.Key)
            {
                case Key.Left:
                    _keyboardOffsetDipX -= step;
                    e.Handled = true;
                    break;
                case Key.Right:
                    _keyboardOffsetDipX += step;
                    e.Handled = true;
                    break;
                case Key.Up:
                    _keyboardOffsetDipY -= step;
                    e.Handled = true;
                    break;
                case Key.Down:
                    _keyboardOffsetDipY += step;
                    e.Handled = true;
                    break;
            }
            if (e.Handled)
            {
                var effectivePos = new Avalonia.Point(
                    _lastMouseDipPos.X + _keyboardOffsetDipX,
                    _lastMouseDipPos.Y + _keyboardOffsetDipY);
                if (_isSelecting)
                {
                    // ドラッグ中 → 終点を微調整
                    UpdateSelectionRectAndMagnifier(effectivePos);
                }
                else
                {
                    // ドラッグ前 → 始点位置の確認（拡大鏡のみ更新）
                    var now = DateTime.UtcNow;
                    if ((now - _lastMagnifierUpdate).TotalMilliseconds >= 100)
                    {
                        UpdateMagnifier(effectivePos);
                        _lastMagnifierUpdate = now;
                    }
                    InvalidateVisual();
                }
            }
        }
    }

    /// <summary>選択矩形と拡大鏡を更新する（キーボード/マウス共用）。</summary>
    /// <param name="effectiveEnd">実効的な終点DIP座標（マウス位置＋キーボードオフセット）</param>
    private void UpdateSelectionRectAndMagnifier(Avalonia.Point effectiveEnd)
    {
        _selectionRect = new Rect(
            Math.Min(_startPoint.X, effectiveEnd.X),
            Math.Min(_startPoint.Y, effectiveEnd.Y),
            Math.Abs(effectiveEnd.X - _startPoint.X) + 1,
            Math.Abs(effectiveEnd.Y - _startPoint.Y) + 1);
        _hasSelection = true;

        var now = DateTime.UtcNow;
        if ((now - _lastMagnifierUpdate).TotalMilliseconds >= 100)
        {
            UpdateMagnifier(effectiveEnd);
            _lastMagnifierUpdate = now;
        }

        InvalidateVisual();
    }

    /// <summary>拡大鏡の表示内容を更新する（カーソル周辺の画面領域をキャプチャ→LoupeDrawingHelperで拡大描画）。</summary>
    private void UpdateMagnifier(Point cursorDIP)
    {
        // DIP → 物理ピクセル変換
        int physX = (int)(cursorDIP.X * _scaling + Position.X);
        int physY = (int)(cursorDIP.Y * _scaling + Position.Y);

        // キャプチャ領域（物理ピクセル）— カーソル中心の capSize×capSize
        int capSize = (int)Math.Ceiling((double)_loupeSize / _zoomLevel);
        int halfCap = capSize / 2;
        var captureBounds = new System.Drawing.Rectangle(
            physX - halfCap,
            physY - halfCap,
            capSize,
            capSize);

        try
        {
            // 事前キャプチャ画像から切り出し → オーバーレイ無しのクリーンな画像
            // ExtractMagnifierSource は画面範囲外を透明ピクセルでパディングする
            var srcBmp = ExtractMagnifierSource(captureBounds, capSize);

            if (srcBmp is null) return;

            using var srcBmpDispose = srcBmp;
            var newImg = LoupeDrawingHelper.DrawLoupe(
                srcBmp,
                _loupeSize,
                _zoomLevel,
                _crossColor,
                _crossOpacity,
                _checkerSize);

            _magnifierImage?.Dispose();
            _magnifierImage = newImg;
            _magnifierCursorPos = cursorDIP;
        }
        catch
        {
            // キャプチャ失敗時は何もしない（古い画像を維持）
        }
    }

    /// <summary>拡大鏡をクリアする。</summary>
    private void ClearMagnifier()
    {
        _magnifierImage?.Dispose();
        _magnifierImage = null;
    }

    /// <summary>キャプチャをキャンセルしてウィンドウを閉じる。</summary>
    private void CancelCapture()
    {
        if (_hasCompleted)
        {
            CaptureLogger.Log("[SelectionWindow] CancelCapture: スキップ（_hasCompleted済み）");
            return;
        }
        CaptureLogger.Log("[SelectionWindow] CancelCapture");
        _hasCompleted = true;
        _onComplete(null, null);
        Close();
        CaptureLogger.Log("[SelectionWindow] CancelCapture: Close()完了");
    }

    /// <summary>指定矩形領域をキャプチャして完了コールバックを呼ぶ。</summary>
    private async Task CaptureAndCompleteAsync(System.Drawing.Rectangle? bounds)
    {
        CaptureLogger.Log("[SelectionWindow] CaptureAndCompleteAsync: bounds={0}", bounds?.ToString() ?? "null");
        System.Drawing.Bitmap? result = null;

        try
        {
            if (bounds.HasValue)
            {
                // 事前キャプチャ画像から切り出し
                result = ExtractFromFixedBackground(bounds.Value);
            }
            else
            {
                // 全画面キャプチャ（ウィンドウ自身の位置とサイズを使用）
                // Window.Position は物理ピクセル、Width/Height は DIP なので _scaling で変換
                var screenBounds = new System.Drawing.Rectangle(
                    Position.X,
                    Position.Y,
                    (int)(Width * _scaling),
                    (int)(Height * _scaling));
                result = ExtractFromFixedBackground(screenBounds);
            }
        }
        catch (Exception ex)
        {
            // キャプチャ失敗時は null を返す
            CaptureLogger.Log("[SelectionWindow] キャプチャ失敗: {0}", ex.Message);
        }

        CaptureLogger.Log("[SelectionWindow] CaptureAndCompleteAsync: result={0}, bounds={1}",
            result is not null ? $"{result.Width}x{result.Height}" : "null",
            bounds?.ToString() ?? "null");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CaptureLogger.Log("[SelectionWindow] CaptureAndCompleteAsync: Dispatcherで_onComplete→Close");
            _hasCompleted = true;
            _onComplete(result, bounds);
            CaptureLogger.Log("[SelectionWindow] CaptureAndCompleteAsync: Close()呼び出し");
            Close();
            CaptureLogger.Log("[SelectionWindow] CaptureAndCompleteAsync: Close()完了");
        });
    }

    /// <summary>指定されたウィンドウハンドルの内容を PrintWindow API でキャプチャする。</summary>
    private static System.Drawing.Bitmap? CaptureWindowByHandle(IntPtr hWnd)
    {
        if (!GetWindowRect(hWnd, out var rect)) return null;

        var width = rect.Width;
        var height = rect.Height;
        if (width <= 0 || height <= 0) return null;

        var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        var hdc = g.GetHdc();

        try
        {
            // 通常の PrintWindow を試行
            if (!PrintWindow(hWnd, hdc, 0))
            {
                // 失敗した場合は DWM 経由で再試行
                PrintWindow(hWnd, hdc, 3); // PW_RENDERFULLCONTENT
            }
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }

        return bmp;
    }

    /// <summary>カーソルのスクリーン座標を取得する。</summary>
    private static Avalonia.PixelPoint GetCursorScreenPoint()
    {
        GetCursorPos(out var pt);
        return new Avalonia.PixelPoint(pt.X, pt.Y);
    }

    /// <summary>このウィンドウの Win32 ウィンドウハンドルを取得する。</summary>
    private IntPtr GetWindowHandle()
        => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    /// <summary>
    /// EnumWindows を使用してカーソル位置にある最前面ウィンドウを取得する。
    /// 自分自身（SelectionWindow）は除外するため、Hide 無しで正しいウィンドウを検出できる。
    /// </summary>
    private IntPtr FindTopWindowUnderCursor()
    {
        var screenPt = GetCursorScreenPoint();
        var selfHandle = GetWindowHandle();
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, _) =>
        {
            if (hWnd == selfHandle) return true; // 自分自身をスキップ
            if (!IsWindowVisible(hWnd)) return true;

            if (GetWindowRect(hWnd, out var rect))
            {
                if (screenPt.X >= rect.Left && screenPt.X < rect.Right &&
                    screenPt.Y >= rect.Top && screenPt.Y < rect.Bottom)
                {
                    // EnumWindows は Z 順（手前→奥）で列挙されるため、最初の一致が最前面
                    found = hWnd;
                    return false; // 列挙終了
                }
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>ウィンドウハンドルからウィンドウタイトルを取得する。</summary>
    /// <param name="hWnd">取得対象のウィンドウハンドル</param>
    /// <returns>ウィンドウタイトル文字列（取得失敗時は空文字列）</returns>
    private static string GetWindowTitle(IntPtr hWnd)
    {
        var len = GetWindowTextLengthW(hWnd);
        if (len <= 0) return "";
        var titleChars = new char[len + 1];
        GetWindowText(hWnd, titleChars, titleChars.Length);
        return new string(titleChars, 0, len);
    }

    /// <summary>Fixモード用の固定背景キャプチャを設定する。</summary>
    /// <remarks>
    /// MainWindow 側で Hide → DWM完了後にキャプチャした結果を受け取り、<br/>
    /// <see cref="_screenCaptures"/> と <see cref="_screenAvaloniaBitmaps"/> を設定する。<br/>
    /// SelectionWindow 表示前にキャプチャ済みのデータを受け取るため、表示後にキャプチャする必要はない。<br/>
    /// </remarks>
    /// <param name="captures">各スクリーンの System.Drawing.Bitmap キャプチャ（切り出し用）</param>
    /// <param name="avaloniaBitmaps">各スクリーンの Avalonia Bitmap キャプチャ（背景描画用）</param>
    public void SetFixedBackgroundCaptures(
        List<(System.Drawing.Rectangle Bounds, System.Drawing.Bitmap Bitmap)> captures,
        List<(System.Drawing.Rectangle Bounds, Avalonia.Media.Imaging.Bitmap Bitmap)> avaloniaBitmaps)
    {
        _screenCaptures.Clear();
        _screenCaptures.AddRange(captures);
        _screenAvaloniaBitmaps.Clear();
        _screenAvaloniaBitmaps.AddRange(avaloniaBitmaps);
        InvalidateVisual();
    }

    /// <summary>Fixモード用に固定背景から指定矩形領域を切り出す。</summary>
    /// <param name="rect">切り出し矩形（画面物理ピクセル座標）</param>
    /// <returns>切り出したビットマップ。失敗時は null。</returns>
    /// <remarks>
    /// 全スクリーンの個別キャプチャから <paramref name="rect"/> と交差する領域をそれぞれ切り出し、<br/>
    /// 1枚のビットマップに合成する。これにより、ウィンドウがマルチモニターの境界をまたぐ場合でも<br/>
    /// 正しくキャプチャできる。<br/>
    /// rect が全スクリーンに一致する場合は <see cref="ComposeFullScreenCapture"/> に委譲する。<br/>
    /// </remarks>
    private System.Drawing.Bitmap? ExtractFromFixedBackground(System.Drawing.Rectangle rect)
    {
        if (_screenCaptures.Count == 0) return null;
        try
        {
            // 全スクリーンに一致する場合 → 全スクリーンから合成
            var totalBounds = _screenCaptures[0].Bounds;
            foreach (var (b, _) in _screenCaptures)
                totalBounds = System.Drawing.Rectangle.Union(totalBounds, b);
            if (totalBounds == rect)
                return ComposeFullScreenCapture();

            // 全スクリーンキャプチャから rect と交差する領域をそれぞれ切り出して合成
            var result = new System.Drawing.Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using var g = System.Drawing.Graphics.FromImage(result);
            foreach (var (bounds, bitmap) in _screenCaptures)
            {
                var inter = System.Drawing.Rectangle.Intersect(rect, bounds);
                if (inter.IsEmpty) continue;

                var srcRect = new System.Drawing.Rectangle(
                    inter.X - bounds.X, inter.Y - bounds.Y,
                    inter.Width, inter.Height);
                var dstX = inter.X - rect.X;
                var dstY = inter.Y - rect.Y;
                g.DrawImage(bitmap, dstX, dstY, srcRect, System.Drawing.GraphicsUnit.Pixel);
            }
            return result;
        }
        catch (Exception ex)
        {
            CaptureLogger.Log("[SelectionWindow] ExtractFromFixedBackground失敗: {0}", ex.Message);
        }
        return null;
    }

    /// <summary>Fixモード用に全スクリーンの個別キャプチャを1枚に合成する。</summary>
    /// <returns>合成された全画面ビットマップ。</returns>
    private System.Drawing.Bitmap ComposeFullScreenCapture()
    {
        var totalBounds = _screenCaptures[0].Bounds;
        foreach (var (b, _) in _screenCaptures)
            totalBounds = System.Drawing.Rectangle.Union(totalBounds, b);

        var result = new System.Drawing.Bitmap(totalBounds.Width, totalBounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using var g = System.Drawing.Graphics.FromImage(result);
        foreach (var (bounds, bitmap) in _screenCaptures)
        {
            g.DrawImage(bitmap, bounds.X - totalBounds.X, bounds.Y - totalBounds.Y);
        }
        return result;
    }

    /// <summary>Fixモード用に固定背景から拡大鏡のソース領域を切り出す。</summary>
    /// <param name="captureBounds">切り出し矩形（画面物理ピクセル座標、カーソル中心）</param>
    /// <param name="capSize">切り出しサイズ（ピクセル）</param>
    /// <returns>capSize×capSize のビットマップ（画面範囲外は透明ピクセル）。失敗時は null。</returns>
    /// <remarks>
    /// <see cref="_screenCaptures"/> の事前キャプチャ画像から該当領域を抽出する。<br/>
    /// 画面端では透明ピクセルでパディングされ、LoupeDrawingHelper が市松模様を透かす。<br/>
    /// </remarks>
    private System.Drawing.Bitmap? ExtractMagnifierSource(System.Drawing.Rectangle captureBounds, int capSize)
    {
        if (_screenCaptures.Count == 0) return null;
        try
        {
            var result = new System.Drawing.Bitmap(capSize, capSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(result);
            // 透明ピクセル初期化（画像範囲外は透明→DrawLoupeが市松模様を透かす）
            g.Clear(System.Drawing.Color.Transparent);

            // 各スクリーンキャプチャと重なる部分を描画
            foreach (var (bounds, bitmap) in _screenCaptures)
            {
                // captureBounds と bounds の交差領域
                var interX = Math.Max(captureBounds.X, bounds.X);
                var interY = Math.Max(captureBounds.Y, bounds.Y);
                var interR = Math.Min(captureBounds.Right, bounds.Right);
                var interB = Math.Min(captureBounds.Bottom, bounds.Bottom);
                int interW = interR - interX;
                int interH = interB - interY;
                if (interW <= 0 || interH <= 0) continue;

                // 交差領域の bitmap 上の座標
                var srcRect = new System.Drawing.Rectangle(
                    interX - bounds.X,
                    interY - bounds.Y,
                    interW, interH);
                // 結果画像上の座標
                var dstX = interX - captureBounds.X;
                var dstY = interY - captureBounds.Y;
                g.DrawImage(bitmap, dstX, dstY, srcRect, System.Drawing.GraphicsUnit.Pixel);
            }
            return result;
        }
        catch (Exception ex)
        {
            CaptureLogger.Log("[SelectionWindow] ExtractMagnifierSource失敗: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>選択矩形を描画する。</summary>
    /// <remarks>
    /// 透明矩形を重ねても下のオーバーレイは除去できないため、<br/>
    /// オーバーレイはハイライト/選択領域の周囲4矩形として描画する。<br/>
    /// これによりハイライト/選択領域は完全に透明（オーバーレイなし）となる。<br/>
    /// Fixモード時は固定背景画像を最初に描画し、その上にオーバーレイを重ねる。<br/>
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Fixモード: 各スクリーンの固定背景を正しい位置に描画
        foreach (var (bounds, bmp) in _screenAvaloniaBitmaps)
        {
            var relX = (bounds.X - Position.X) / _scaling;
            var relY = (bounds.Y - Position.Y) / _scaling;
            context.DrawImage(bmp, new Rect(relX, relY, bounds.Width / _scaling, bounds.Height / _scaling));
        }

        var clientRect = new Rect(0, 0, Bounds.Width, Bounds.Height);

        // ハイライト（WindowSelect）または選択（AreaSelect/SelectScreen）領域を決定
        var holeRect = _hasHighlight ? _highlightRect
                    : _hasSelection ? _selectionRect
                    : (Rect?)null;

        if (holeRect.HasValue)
        {
            var hr = holeRect.Value;
            // ハイライト/選択領域の周囲4矩形にオーバーレイを描画（領域内は透明のまま）
            // 上
            if (hr.Top > 0)
                context.FillRectangle(s_overlayBrush, new Rect(0, 0, clientRect.Width, hr.Top));
            // 下
            if (hr.Bottom < clientRect.Height)
                context.FillRectangle(s_overlayBrush, new Rect(0, hr.Bottom, clientRect.Width, clientRect.Height - hr.Bottom));
            // 左
            if (hr.Left > 0)
                context.FillRectangle(s_overlayBrush, new Rect(0, hr.Top, hr.Left, hr.Height));
            // 右
            if (hr.Right < clientRect.Width)
                context.FillRectangle(s_overlayBrush, new Rect(hr.Right, hr.Top, clientRect.Width - hr.Right, hr.Height));

            // テキスト背景用ブラシ（キャプチャ時の不透明度を使用）
            var textBgAlpha = (byte)Math.Clamp(_textBgOpacity * 255, 0, 255);
            var textBgBrush = new SolidColorBrush(Color.FromArgb(textBgAlpha, 0, 0, 0));

            if (_hasHighlight)
            {
                // WindowSelect: ハイライト枠を描画
                context.DrawRectangle(_selectionPen, _highlightRect);

                // ── 左上に枠サイズ表示 ──
                var hlSizeText = $"{(int)_highlightRect.Width} x {(int)_highlightRect.Height}";
                var hlFt = new FormattedText(
                    hlSizeText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    14,
                    Brushes.White);
                var pad = 4.0;
                context.FillRectangle(textBgBrush, new Rect(
                    _highlightRect.X + pad, _highlightRect.Y + pad,
                    hlFt.Width + pad * 2, hlFt.Height + pad * 2));
                context.DrawText(hlFt, new Point(
                    _highlightRect.X + pad * 2,
                    _highlightRect.Y + pad * 2));

                // ── 中央にウィンドウタイトル表示 ──
                if (!string.IsNullOrEmpty(_windowTitle))
                {
                    var titleText = new FormattedText(
                        _windowTitle,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        16,
                        Brushes.White);
                    var titleX = _highlightRect.X + (_highlightRect.Width - titleText.Width) / 2;
                    var titleY = _highlightRect.Y + (_highlightRect.Height - titleText.Height) / 2;
                    context.FillRectangle(textBgBrush, new Rect(
                        titleX - pad, titleY - pad,
                        titleText.Width + pad * 2, titleText.Height + pad * 2));
                    context.DrawText(titleText, new Point(titleX, titleY));
                }
            }

            if (_hasSelection)
            {
                // 選択枠を描画
                context.DrawRectangle(_selectionPen, _selectionRect);

                // SelectScreen: 中央にスクリーン番号表示
                if (_captureType == CaptureType.SelectScreen && _screenIndex >= 0)
                {
                    var labelText = $"スクリーン {_screenIndex + 1}";
                    var labelFormatted = new FormattedText(
                        labelText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        20,
                        Brushes.White);
                    var labelX = _selectionRect.X + (_selectionRect.Width - labelFormatted.Width) / 2;
                    var labelY = _selectionRect.Y + (_selectionRect.Height - labelFormatted.Height) / 2;
                    var pad = 4.0;
                    context.FillRectangle(textBgBrush, new Rect(
                        labelX - pad, labelY - pad,
                        labelFormatted.Width + pad * 2, labelFormatted.Height + pad * 2));
                    context.DrawText(labelFormatted, new Point(labelX, labelY));
                }

                // ── 左上に枠サイズ表示 ──
                var sizeText = $"{(int)_selectionRect.Width} x {(int)_selectionRect.Height}";
                var formattedText = new FormattedText(
                    sizeText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    14,
                    Brushes.White);
                var pad2 = 4.0;
                context.FillRectangle(textBgBrush, new Rect(
                    _selectionRect.X + pad2, _selectionRect.Y + pad2,
                    formattedText.Width + pad2 * 2, formattedText.Height + pad2 * 2));
                context.DrawText(formattedText, new Point(
                    _selectionRect.X + pad2 * 2,
                    _selectionRect.Y + pad2 * 2));
            }

            // ── 領域選択の拡大鏡（全描画の最後＝最前面） ──
            if (_captureType == CaptureType.AreaSelect && _magnifierImage is not null)
            {
                DrawMagnifier(context);
            }
        }
        else
        {
            // 選択/ハイライトがない場合は全面にオーバーレイ
            context.FillRectangle(s_overlayBrush, clientRect);

            // ── オーバーレイ上に拡大鏡を表示（ドラッグ前のホバー中） ──
            if (_captureType == CaptureType.AreaSelect && _magnifierImage is not null)
            {
                DrawMagnifier(context);
            }
        }
    }

    /// <summary>拡大鏡を描画する（LoupeDrawingHelperで生成済みのBitmapをそのまま描画）。</summary>
    private void DrawMagnifier(DrawingContext context)
    {
        double offsetX = 20;
        double offsetY = 20;
        double mx = _magnifierCursorPos.X + offsetX;
        double my = _magnifierCursorPos.Y + offsetY;

        // 画面右端・下端で折り返し
        if (mx + _loupeSize > Bounds.Width)
            mx = _magnifierCursorPos.X - _loupeSize - offsetX;
        if (my + _loupeSize > Bounds.Height)
            my = _magnifierCursorPos.Y - _loupeSize - offsetY;
        // 画面左端・上端でもクランプ
        mx = Math.Max(0, mx);
        my = Math.Max(0, my);

        var magRect = new Rect(mx, my, _loupeSize, _loupeSize);
        var srcRect = new Rect(0, 0, _loupeSize, _loupeSize);
        context.DrawImage(_magnifierImage!, srcRect, magRect);
    }
}

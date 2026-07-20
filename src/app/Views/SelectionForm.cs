using System.Runtime.InteropServices;
using app.Models;

namespace app.Views;

/// <summary>
/// 画面キャプチャ時の領域選択用モーダルフォーム。3種の選択モードをサポートする。
/// </summary>
/// <remarks>
/// サポートする選択モード:<br/>
/// - <see cref="CaptureType.SelectScreen"/>: マルチスクリーン環境でのスクリーン選択<br/>
/// - <see cref="CaptureType.WindowSelect"/>: マウスカーソル下のウィンドウ自動検出<br/>
/// - <see cref="CaptureType.AreaSelect"/>: マウスドラッグによる自由領域選択<br/>
/// <br/>
/// 全面に事前キャプチャ画像を表示し、非選択領域を暗転、選択領域を明るく表示する。<br/>
/// 選択領域の境界には白黒の破線を描画する。<br/>
/// 選択完了時は <see cref="SelectionCompleted"/> イベント、キャンセル時は <see cref="Cancelled"/> イベントが発生する。<br/>
/// </remarks>
public sealed partial class SelectionForm : Form
{
    private readonly CaptureType _captureType;
    private readonly (int Index, Rectangle Bounds, string DeviceName)[] _screens;
    private readonly Bitmap? _screenCapture;
    private Point _startPoint;
    private Point _endPoint;
    private bool _isDragging;
    private Rectangle _currentSelection;
    private int _highlightedScreenIndex = -1;
    private IntPtr _highlightedWindow;
    private Rectangle _highlightedWindowRect;
    private string _highlightedWindowTitle = "";
    private readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 150 };
    private readonly Point _virtualOrigin;
    private readonly Color _borderColor;

    /// <summary>
    /// 選択が完了した時に発生するイベント（引数はスクリーン座標の矩形）
    /// </summary>
    public event EventHandler<Rectangle>? SelectionCompleted;

    /// <summary>
    /// キャプチャがキャンセルされた時に発生するイベント
    /// </summary>
    public event EventHandler? Cancelled;

    /// <summary>ウィンドウ選択モードで最後に検出・確定されたウィンドウハンドル。</summary>
    /// <remarks>
    /// ウィンドウ選択（<see cref="CaptureType.WindowSelect"/>）でマウスクリックにより
    /// 確定された時点のハイライトウィンドウハンドルを保持する。<br/>
    /// <see cref="SelectionCompleted"/> イベントの sender から取得できる。<br/>
    /// </remarks>
    public IntPtr SelectedWindowHandle { get; private set; }

    /// <summary>
    /// 選択スクリーン／ウィンドウ選択／領域選択 モード用コンストラクタ
    /// </summary>
    /// <param name="captureType">キャプチャの種類</param>
    /// <param name="preCapturedImage">事前にキャプチャした全画面ビットマップ</param>
    public SelectionForm(CaptureType captureType, Bitmap preCapturedImage, string borderColorName = "White")
    {
        _captureType = captureType;
        _screenCapture = preCapturedImage;
        _screens = CaptureManager.GetAllScreenBounds();
        _borderColor = Color.FromName(borderColorName);

        var virtualBounds = SystemInformation.VirtualScreen;
        _virtualOrigin = virtualBounds.Location;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualBounds;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = captureType == CaptureType.AreaSelect ? Cursors.Cross : Cursors.Default;
        BackColor = Color.Black;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;

        _hoverTimer.Tick += HoverTimer_Tick;

        Paint += SelectionForm_Paint;
        MouseDown += SelectionForm_MouseDown;
        MouseMove += SelectionForm_MouseMove;
        MouseUp += SelectionForm_MouseUp;
        KeyDown += SelectionForm_KeyDown;
        Shown += SelectionForm_Shown;
    }

    /// <summary>
    /// フォーム表示時にウィンドウ選択の初回検出を実行する
    /// </summary>
    private void SelectionForm_Shown(object? sender, EventArgs e)
    {
        if (_captureType == CaptureType.WindowSelect)
        {
            DetectWindowUnderCursor();
            Invalidate();
        }
    }

    /// <summary>
    /// フォーム描画処理。モードに応じて適切な描画メソッドを呼び出す
    /// </summary>
    private void SelectionForm_Paint(object? sender, PaintEventArgs e)
    {
        try
        {
            if (_screenCapture is null)
            {
                return;
            }

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImage(_screenCapture, Point.Empty);

            switch (_captureType)
            {
                case CaptureType.SelectScreen:
                    PaintScreenSelect(e);
                    break;
                case CaptureType.WindowSelect:
                    PaintWindowSelect(e);
                    break;
                case CaptureType.AreaSelect:
                    PaintAreaSelect(e);
                    break;
            }
        }
        catch (Exception)
        {
        }
    }

    // ─── 静的描画リソース（キャッシュして再利用） ────────────────
    private static readonly SolidBrush s_dimBrush = new(Color.FromArgb(140, 0, 0, 0));
    private static readonly Font s_overlayFont = new("Segoe UI", 14, FontStyle.Regular);
    private static readonly SolidBrush s_overlayTextBrush = new(Color.White);
    private static readonly SolidBrush s_overlayBgBrush = new(Color.FromArgb(160, 0, 0, 0));

    /// <summary>
    /// 白黒の破線ペンを作成する（一番細い線）
    /// </summary>
    private Pen CreateDashedBorderPen()
    {
        var pen = new Pen(_borderColor, 1)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Custom,
            DashPattern = [1f, 3f]
        };
        return pen;
    }

    /// <summary>
    /// 指定位置にオーバーレイテキストを描画する
    /// </summary>
    private static void DrawOverlayText(Graphics g, string text, RectangleF rect)
    {
        var textSize = g.MeasureString(text, s_overlayFont);
        var cx = rect.X + rect.Width / 2f;
        var cy = rect.Y + rect.Height / 2f;
        var labelX = cx - textSize.Width / 2f;
        var labelY = cy - textSize.Height / 2f;
        g.FillRectangle(s_overlayBgBrush, labelX - 10, labelY - 10, textSize.Width + 20, textSize.Height + 20);
        g.DrawString(text, s_overlayFont, s_overlayTextBrush, labelX, labelY);
    }

    /// <summary>
    /// スクリーン選択の描画。選択中のスクリーンは明るく、非選択は暗く表示する。
    /// </summary>
    private void PaintScreenSelect(PaintEventArgs e)
    {
        var g = e.Graphics;
        foreach (var (index, bounds, _) in _screens)
        {
            var screenRect = ScreenToClient(bounds);
            if (index != _highlightedScreenIndex)
            {
                g.FillRectangle(s_dimBrush, screenRect);
            }
            else
            {
                using var border = CreateDashedBorderPen();
                g.DrawRectangle(border, screenRect);
            }
            // スクリーン番号を中央にオーバーレイ表示
            DrawOverlayText(g, $"スクリーン {index + 1}", screenRect);
        }
    }

    /// <summary>
    /// ウィンドウ選択の描画。ハイライト中のウィンドウ領域は明るく、それ以外は暗く表示する。
    /// </summary>
    private void PaintWindowSelect(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (_highlightedWindow != IntPtr.Zero && _highlightedWindowRect != Rectangle.Empty)
        {
            var highlightClient = ScreenToClient(_highlightedWindowRect);

            using (var region = new Region(ClientRectangle))
            {
                region.Exclude(highlightClient);
                g.Clip = region;
                g.FillRectangle(s_dimBrush, ClientRectangle);
                g.ResetClip();
            }

            using (var border = CreateDashedBorderPen())
            {
                g.DrawRectangle(border, highlightClient);
            }

            // ウィンドウ名を中央にオーバーレイ表示
            if (!string.IsNullOrEmpty(_highlightedWindowTitle))
            {
                DrawOverlayText(g, _highlightedWindowTitle, highlightClient);
            }
        }
        else
        {
            g.FillRectangle(s_dimBrush, ClientRectangle);
        }
    }

    /// <summary>
    /// 領域選択の描画。ドラッグ中の選択領域は明るく、それ以外は暗く表示する。
    /// 選択範囲の中央に座標とサイズを表示する。
    /// </summary>
    private void PaintAreaSelect(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (_isDragging && _currentSelection.Width > 0 && _currentSelection.Height > 0)
        {
            using (var region = new Region(ClientRectangle))
            {
                region.Exclude(_currentSelection);
                g.Clip = region;
                g.FillRectangle(s_dimBrush, ClientRectangle);
                g.ResetClip();
            }

            using (var border = CreateDashedBorderPen())
            {
                g.DrawRectangle(border, _currentSelection);
            }

            // 選択範囲の中央に座標・サイズ情報をオーバーレイ表示
            var x1 = _currentSelection.X + _virtualOrigin.X;
            var y1 = _currentSelection.Y + _virtualOrigin.Y;
            var x2 = x1 + _currentSelection.Width;
            var y2 = y1 + _currentSelection.Height;
            var infoText = $"座標: ({x1}, {y1})-({x2}, {y2})\nサイズ: ({_currentSelection.Width}, {_currentSelection.Height})";
            DrawOverlayText(g, infoText, _currentSelection);
        }
        else if (!_isDragging)
        {
            // ドラッグ開始前は全面暗転＋各スクリーンに「領域選択」と表示（スクリーン選択と同じ書式）
            using var dimBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
            g.FillRectangle(dimBrush, ClientRectangle);

            foreach (var (_, bounds, _) in _screens)
            {
                var screenRect = new Rectangle(
                    bounds.X - _virtualOrigin.X,
                    bounds.Y - _virtualOrigin.Y,
                    bounds.Width,
                    bounds.Height);

                DrawOverlayText(g, "領域選択", screenRect);
            }
        }
    }

    /// <summary>
    /// スクリーン座標をクライアント座標に変換する
    /// </summary>
    private Rectangle ScreenToClient(Rectangle screenRect)
    {
        return new Rectangle(
            screenRect.X - _virtualOrigin.X,
            screenRect.Y - _virtualOrigin.Y,
            screenRect.Width,
            screenRect.Height);
    }

    /// <summary>
    /// クライアント座標からスクリーンインデックスを取得する
    /// </summary>
    private int GetScreenIndexAtPoint(Point clientPoint)
    {
        var screenPoint = new Point(
            clientPoint.X + _virtualOrigin.X,
            clientPoint.Y + _virtualOrigin.Y);

        for (var i = 0; i < _screens.Length; i++)
        {
            if (_screens[i].Bounds.Contains(screenPoint))
            {
                return i;
            }
        }
        return -1;
    }

    private void SelectionForm_MouseDown(object? sender, MouseEventArgs e)
    {
        try
        {
            if (_captureType == CaptureType.AreaSelect)
            {
                _startPoint = e.Location;
                _isDragging = true;
            }
            else if (_captureType == CaptureType.SelectScreen)
            {
                var screenIndex = GetScreenIndexAtPoint(e.Location);
                if (screenIndex >= 0)
                {
                    Hide();
                    SelectionCompleted?.Invoke(this, _screens[screenIndex].Bounds);
                }
            }
            else if (_captureType == CaptureType.WindowSelect)
            {
                _hoverTimer.Stop();
                if (_highlightedWindow == IntPtr.Zero)
                {
                    DetectWindowUnderCursor();
                }
                SelectedWindowHandle = _highlightedWindow;
                Hide();
                if (_highlightedWindow != IntPtr.Zero && _highlightedWindowRect != Rectangle.Empty)
                {
                    SelectionCompleted?.Invoke(this, _highlightedWindowRect);
                }
            }
        }
        catch (Exception)
        {
            CancelCapture();
        }
    }

    private void SelectionForm_MouseMove(object? sender, MouseEventArgs e)
    {
        try
        {
            if (_captureType == CaptureType.AreaSelect)
            {
                if (_isDragging)
                {
                    _endPoint = e.Location;
                    _currentSelection = GetNormalizedRectangle(_startPoint, _endPoint);
                    Invalidate();
                }
            }
            else if (_captureType == CaptureType.SelectScreen)
            {
                var screenIndex = GetScreenIndexAtPoint(e.Location);
                if (screenIndex != _highlightedScreenIndex)
                {
                    _highlightedScreenIndex = screenIndex;
                    Invalidate();
                }
            }
            else if (_captureType == CaptureType.WindowSelect)
            {
                var old = _highlightedWindow;
                DetectWindowUnderCursor();
                if (_highlightedWindow != old)
                {
                    Invalidate();
                }
            }
        }
        catch (Exception)
        {
            CancelCapture();
        }
    }

    private void SelectionForm_MouseUp(object? sender, MouseEventArgs e)
    {
        try
        {
            if (_captureType == CaptureType.AreaSelect && _isDragging)
            {
                _isDragging = false;
                _endPoint = e.Location;
                _currentSelection = GetNormalizedRectangle(_startPoint, _endPoint);

                if (_currentSelection.Width > 5 && _currentSelection.Height > 5)
                {
                    var screenSelection = new Rectangle(
                        _currentSelection.X + _virtualOrigin.X,
                        _currentSelection.Y + _virtualOrigin.Y,
                        _currentSelection.Width,
                        _currentSelection.Height);

                    Hide();
                    SelectionCompleted?.Invoke(this, screenSelection);
                }
                else
                {
                    Invalidate();
                }
            }
        }
        catch (Exception)
        {
            CancelCapture();
        }
    }

    private void SelectionForm_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.KeyCode == Keys.Escape)
            {
                CancelCapture();
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// マウスカーソル下のウィンドウを検出する。
    /// EnumWindows で全トップレベルウィンドウを列挙し、カーソル位置を含む最初の可視ウィンドウを検出する。
    /// 画像ベース方式（全画面キャプチャ表示）では WindowFromPoint が自身を返すため、EnumWindows を使用する。
    /// </summary>
    private void DetectWindowUnderCursor()
    {
        try
        {
            var cursorPos = Cursor.Position;
            IntPtr found = IntPtr.Zero;
            string foundTitle = "";

            EnumWindows((hWnd, _) =>
            {
                try
                {
                    if (hWnd == Handle) return true;
                    var root = GetAncestor(hWnd, GA_ROOT);
                    if (root == Handle || root == IntPtr.Zero) return true;
                    if (!IsWindowVisible(root)) return true;

                    var rect = CaptureManager.GetWindowRect(root);
                    if (rect != Rectangle.Empty && rect.Contains(cursorPos))
                    {
                        found = root;
                        foundTitle = GetWindowTextFromHandle(root);
                        return false;
                    }
                }
                catch
                {
                }
                return true;
            }, IntPtr.Zero);

            _highlightedWindow = found;
            _highlightedWindowTitle = foundTitle;
            if (found != IntPtr.Zero)
            {
                _highlightedWindowRect = CaptureManager.GetWindowVisibleRect(found);
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// ホバータイマーのTick処理。
    /// </summary>
    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
    }

    /// <summary>
    /// ウィンドウハンドルからウィンドウタイトルを取得する
    /// </summary>
    private static string GetWindowTextFromHandle(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        _ = GetWindowTextNative(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// キャプチャをキャンセルする
    /// </summary>
    private void CancelCapture()
    {
        try
        {
            _hoverTimer.Stop();
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
        }
        finally
        {
            if (Visible)
            {
                Hide();
            }
            if (!IsDisposed)
            {
                Close();
            }
        }
    }

    private static Rectangle GetNormalizedRectangle(Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var width = Math.Abs(start.X - end.X);
        var height = Math.Abs(start.Y - end.Y);
        return new Rectangle(x, y, width, height);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        try
        {
            if (keyData == Keys.Escape)
            {
                CancelCapture();
                return true;
            }
        }
        catch (Exception)
        {
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hoverTimer.Stop();
            _hoverTimer.Dispose();
            Paint -= SelectionForm_Paint;
            MouseDown -= SelectionForm_MouseDown;
            MouseMove -= SelectionForm_MouseMove;
            MouseUp -= SelectionForm_MouseUp;
            KeyDown -= SelectionForm_KeyDown;
            Shown -= SelectionForm_Shown;
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowText", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextNative(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint GA_ROOT = 2;
}

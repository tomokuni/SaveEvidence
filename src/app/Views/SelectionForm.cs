using app;
using app.Models;

namespace app.Views;

/// <summary>
/// 画面選択フォーム。範囲選択モードと選択スクリーンモードをサポートする。
/// 選択スクリーンモードでは、事前にキャプチャした全画面ビットマップを表示して
/// 選択させる方式をとる（透明オーバーレイを使わないため座標問題が発生しない）。
/// </summary>
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

    /// <summary>
    /// 選択が完了した時に発生するイベント（引数はスクリーン座標の矩形）
    /// </summary>
    public event EventHandler<Rectangle>? SelectionCompleted;

    /// <summary>
    /// キャプチャがキャンセルされた時に発生するイベント
    /// </summary>
    public event EventHandler? Cancelled;

    /// <summary>
    /// 選択スクリーンモード用コンストラクタ
    /// </summary>
    /// <param name="captureType">キャプチャの種類（SelectScreen）</param>
    /// <param name="preCapturedImage">事前にキャプチャした全画面ビットマップ</param>
    public SelectionForm(CaptureType captureType, Bitmap preCapturedImage)
        : this(captureType)
    {
        _screenCapture = preCapturedImage;

        // 透明オーバーレイは使わない（レイヤードウィンドウの位置問題を回避）
        Opacity = 1.0;
        BackColor = Color.Black;

        // キャプチャ画像に合わせてフォームサイズと位置を設定
        var virtualBounds = SystemInformation.VirtualScreen;
        ClientSize = virtualBounds.Size;
        Location = virtualBounds.Location;

        Cursor = Cursors.Default;
    }

    /// <summary>
    /// 範囲選択（AreaSelect）モード用コンストラクタ
    /// </summary>
    /// <param name="captureType">キャプチャの種類</param>
    public SelectionForm(CaptureType captureType)
    {
        _captureType = captureType;
        _screens = CaptureManager.GetAllScreenBounds();

        // すべてのスクリーンを囲む物理ピクセル矩形を計算
        var totalBounds = Rectangle.Empty;
        foreach (var screen in Screen.AllScreens)
        {
            totalBounds = Rectangle.Union(totalBounds, screen.Bounds);
        }

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = totalBounds;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = captureType == CaptureType.SelectScreen ? Cursors.Default : Cursors.Cross;
        BackColor = Color.Black;
        Opacity = 0.3;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None; // スケーリングによる座標ずれを防止

        Paint += SelectionForm_Paint!;
        MouseDown += SelectionForm_MouseDown!;
        MouseMove += SelectionForm_MouseMove!;
        MouseUp += SelectionForm_MouseUp!;
        KeyDown += SelectionForm_KeyDown!;
    }

    private void SelectionForm_Paint(object sender, PaintEventArgs e)
    {
        try
        {
            if (_captureType == CaptureType.SelectScreen && _screenCapture is not null)
            {
                // キャプチャ画像を描画
                e.Graphics.DrawImage(_screenCapture, Point.Empty);

                // 半透明の暗転オーバーレイ
                using var dimBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
                e.Graphics.FillRectangle(dimBrush, ClientRectangle);

                // 各スクリーンの境界線と番号を描画
                DrawScreenBoundaries(e.Graphics);
            }
            else if (_captureType == CaptureType.AreaSelect && _isDragging)
            {
                using var pen = new Pen(Color.Red, 2);
                e.Graphics.DrawRectangle(pen, _currentSelection);

                using var brush = new SolidBrush(Color.FromArgb(80, 255, 0, 0));
                e.Graphics.FillRectangle(brush, _currentSelection);
            }
        }
        catch (Exception ex)
        {
            Program.LogException(ex);
        }
    }

    /// <summary>
    /// 各スクリーンの境界線と番号を描画する。
    /// スクリーン座標系と画像の座標系は一致しているので単純な座標変換でよい。
    /// </summary>
    private void DrawScreenBoundaries(Graphics g)
    {
        // キャプチャ画像の左上座標＝仮想スクリーンの左上座標
        var virtualOrigin = SystemInformation.VirtualScreen.Location;

        foreach (var (index, bounds, _) in _screens)
        {
            // 仮想スクリーン座標 → 画像内座標
            var screenRect = new Rectangle(
                bounds.X - virtualOrigin.X,
                bounds.Y - virtualOrigin.Y,
                bounds.Width,
                bounds.Height);

            // ハイライト表示
            if (index == _highlightedScreenIndex)
            {
                using var highlightBrush = new SolidBrush(Color.FromArgb(80, 0, 120, 255));
                g.FillRectangle(highlightBrush, screenRect);
            }

            // 境界線
            using var pen = new Pen(index == _highlightedScreenIndex ? Color.Cyan : Color.White, 3);
            g.DrawRectangle(pen, screenRect);

            // ラベル
            var labelText = $"スクリーン {index + 1}";
            using var font = new Font("Segoe UI", 24, FontStyle.Bold);
            using var textBrush = new SolidBrush(index == _highlightedScreenIndex ? Color.Cyan : Color.White);
            using var bgBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0));

            var textSize = g.MeasureString(labelText, font);
            var labelX = screenRect.X + (screenRect.Width - textSize.Width) / 2;
            var labelY = screenRect.Y + (screenRect.Height - textSize.Height) / 2;

            var labelRect = new Rectangle(
                (int)labelX - 10, (int)labelY - 10,
                (int)textSize.Width + 20, (int)textSize.Height + 20);

            g.FillRectangle(bgBrush, labelRect);
            g.DrawString(labelText, font, textBrush, labelX, labelY);
        }
    }

    /// <summary>
    /// 指定されたクライアント座標のスクリーンインデックスを取得する。
    /// キャプチャ画像とスクリーン座標系は一致しているため、
    /// 画像内座標 + 仮想スクリーン原点 = スクリーン座標 となる。
    /// </summary>
    private int GetScreenIndexAtPoint(Point clientPoint)
    {
        var virtualOrigin = SystemInformation.VirtualScreen.Location;
        var screenPoint = new Point(
            clientPoint.X + virtualOrigin.X,
            clientPoint.Y + virtualOrigin.Y);

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
        }
        catch (Exception ex)
        {
            Program.LogException(ex);
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
        }
        catch (Exception ex)
        {
            Program.LogException(ex);
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
                    // クライアント座標をスクリーン座標に変換
                    var primaryOrigin = Screen.PrimaryScreen!.Bounds.Location;
                    var screenSelection = new Rectangle(
                        _currentSelection.X + primaryOrigin.X,
                        _currentSelection.Y + primaryOrigin.Y,
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
        catch (Exception ex)
        {
            Program.LogException(ex);
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
        catch (Exception ex)
        {
            Program.LogException(ex);
        }
    }

    private void CancelCapture()
    {
        try
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Program.LogException(ex);
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
        catch (Exception ex)
        {
            Program.LogException(ex);
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Paint -= SelectionForm_Paint!;
            MouseDown -= SelectionForm_MouseDown!;
            MouseMove -= SelectionForm_MouseMove!;
            MouseUp -= SelectionForm_MouseUp!;
            KeyDown -= SelectionForm_KeyDown!;
        }

        base.Dispose(disposing);
    }
}

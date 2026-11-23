using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（WinForms 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: レイヤー（画像・注釈・入力）と書式バーを重ねて持つ。 2 つを別々に置くと、
/// 位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>レイヤーは <see cref="Layer"/> から触る</b>: 画像・文書・選択・元に戻すなどは
/// <see cref="AnnotationLayerControl"/> の API を使う（本コントロールは配置だけを担う）。
/// </remarks>
public sealed class AnnotationTextEditor : UserControl
{
    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>本文の書式を操作するバー。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>外観・重なり順の編集パネル（開いている間だけ保持する）。</summary>
    private AnnotationStylePanel? _stylePanel;
    /// <summary>書式バーを載せる枠なしウィンドウ（ウィンドウの外へ出せる）。</summary>
    private Form? _formatBarWindow;

    /// <summary>外観の編集パネルを載せる枠なしウィンドウ（ウィンドウの外へ出せる）。</summary>
    private Form? _styleWindow;


    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl { Dock = DockStyle.Fill };
        _formatBar = new AnnotationFormatBar(_layer);
        Controls.Add(_layer);


        _layer.EditingChanged += (_, _) =>
        {
            ShowFormatBarWindow(_layer.IsEditingText);
            if (_layer.IsEditingText)
            {
                _formatBar.RefreshState();
            }
        };

        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.RefreshState();
            }
        };

        _layer.SelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.RefreshState();
            }

            if (_stylePanel is not null)
            {
                CloseStylePanel();
            }
        };

        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);
    }

    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
    public AnnotationLayerControl Layer => _layer;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ColorTextFormat TextFormat
    {
        get => _formatBar.TextFormat;
        set
        {
            _formatBar.TextFormat = value;
            _layer.ColorTextFormat = value;
        }
    }

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>枠なしウィンドウへ載せる</b>: アプリのウィンドウの子にするとウィンドウの外へ出せず、
    /// 狭いウィンドウでパネルが切れます。 枠なし・タスク バー非表示のウィンドウへ載せれば、
    /// 画面の内側でウィンドウの外へも出せます（実測 2026-11-23）。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
    {
        CloseStylePanel();

        var panel = new AnnotationStylePanel(_layer, CloseStylePanel, AnnotationPanelAppearance.StyleEditor);
        var location = PointToScreen(new Point((int)Math.Max(0, x), (int)Math.Max(0, y)));
        _stylePanel = panel;
        _styleWindow = ShowInWindow(panel, location);
    }

    /// <summary>コントロールを枠なしウィンドウへ載せて表示する。</summary>
    /// <param name="content">載せる中身（自動で大きさを合わせる）。</param>
    /// <param name="screenLocation">ウィンドウの左上（画面座標）。</param>
    /// <returns>作ったウィンドウ。</returns>
    private static Form ShowInWindow(Control content, Point screenLocation)
    {
        content.Dock = DockStyle.Fill;
        var window = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = screenLocation,
            TopMost = false,
        };

        window.Controls.Add(content);
        window.Show();

        // **画面の外へは出さない**: ウィンドウの外へ出せるようにしたため、作業領域で挟む（実測 2026-11-23）。
        var area = Screen.FromPoint(window.Location).WorkingArea;
        window.Location = new Point(
            Math.Clamp(window.Location.X, area.X, Math.Max(area.X, area.Right - window.Width)),
            Math.Clamp(window.Location.Y, area.Y, Math.Max(area.Y, area.Bottom - window.Height)));
        return window;
    }
    /// <param name="show">出す場合は <see langword="true"/>。</param>
    private void ShowFormatBarWindow(bool show)
    {
        if (!show)
        {
            if (_formatBarWindow is { } closing)
            {
                _formatBarWindow = null;
                closing.Controls.Remove(_formatBar);
                closing.Close();
                closing.Dispose();
            }

            return;
        }

        if (_formatBarWindow is not null)
        {
            return;
        }

        // 本文の上端の少し上へ出す（本文に被せない）
        var location = PointToScreen(new Point(8, 8));
        _formatBarWindow = ShowInWindow(_formatBar, new Point(location.X, Math.Max(0, location.Y - _formatBar.Height - 8)));
    }
    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel is not null;


    /// <summary>外観・重なり順の編集パネルを閉じる。</summary>
    public void CloseStylePanel()
    {
        _stylePanel = null;

        if (_styleWindow is not { } window)
        {
            return;
        }

        _styleWindow = null;
        window.Hide();
        window.Controls.Clear();
        window.Close();
        window.Dispose();
    }
}

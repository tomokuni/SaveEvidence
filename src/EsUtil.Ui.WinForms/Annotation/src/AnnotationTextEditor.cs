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

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl { Dock = DockStyle.Fill };
        _formatBar = new AnnotationFormatBar(_layer) { Visible = false, Location = new Point(8, 8) };

        Controls.Add(_layer);
        Controls.Add(_formatBar);
        _formatBar.BringToFront();

        _layer.EditingChanged += (_, _) =>
        {
            _formatBar.Visible = _layer.IsEditingText;
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
    /// <b>ウィンドウの内側へ収めない</b>: 収めると狭いウィンドウでパネルが潰れる。 はみ出してよい
    /// （パネルはドラッグで動かせる）。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
    {
        CloseStylePanel();

        var panel = new AnnotationStylePanel(_layer, CloseStylePanel)
        {
            Location = new Point((int)Math.Max(0, x), (int)Math.Max(0, y)),
        };

        // 押したまま動かすと移動する
        Point? origin = null;
        Point originLocation = default;
        panel.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            origin = e.Location;
            originLocation = panel.Location;
        };
        panel.MouseMove += (_, e) =>
        {
            if (origin is not { } start)
            {
                return;
            }

            panel.Location = new Point(
                originLocation.X + (e.X - start.X),
                originLocation.Y + (e.Y - start.Y));
        };
        panel.MouseUp += (_, _) => origin = null;

        _stylePanel = panel;
        Controls.Add(panel);
        panel.BringToFront();
    }

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel is not null;

    /// <summary>指定した位置が外観の編集パネルの上かどうかを判定する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <returns>パネルの上の場合は <see langword="true"/>。</returns>
    public bool IsStylePanelHit(double x, double y)
        => _stylePanel is { } panel
            && x >= panel.Left && y >= panel.Top
            && x < panel.Right && y < panel.Bottom;

    /// <summary>外観・重なり順の編集パネルを閉じる。</summary>
    public void CloseStylePanel()
    {
        if (_stylePanel is not { } panel)
        {
            return;
        }

        _stylePanel = null;
        Controls.Remove(panel);
        panel.Dispose();
    }
}

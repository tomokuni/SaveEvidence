using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（WinUI 3 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: レイヤー（画像・注釈・入力）と書式バーを重ねて持つ。 2 つを別々に置くと、
/// 位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>レイヤーは <see cref="Layer"/> から触る</b>: 画像・文書・選択・元に戻すなどは
/// <see cref="AnnotationLayerControl"/> の API を使う（本コントロールは配置だけを担う）。
/// </remarks>
public sealed class AnnotationTextEditor : Grid
{
    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>本文の書式を操作するバー。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>書式バーを載せるフライアウト（ウィンドウの外へ出せるようにする）。</summary>
    private readonly Flyout _formatBarFlyout;

    /// <summary>外観・重なり順の編集パネル（開いている間だけ保持する）。</summary>
    private AnnotationStylePanel? _stylePanel;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);
        _formatBarFlyout = new Flyout
        {
            Content = _formatBar,
            Placement = FlyoutPlacementMode.Top,
            ShouldConstrainToRootBounds = false,
        };

        Children.Add(_layer);

        _layer.EditingChanged += (_, _) =>
        {
            if (!_layer.IsEditingText)
            {
                _formatBarFlyout.Hide();
                return;
            }

            _formatBar.Refresh();
            ShowFormatBar();
        };

        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
            }
        };

        _layer.SelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
            }

            if (_stylePanel is not null)
            {
                CloseStylePanel();
            }
        };

        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);
    }

    /// <summary>書式バーを本文の上へ出す（ウィンドウの外へ出せる）。</summary>
    private void ShowFormatBar()
    {
        var body = _layer.GetSelectedBodyRect();
        _ = body;
        _formatBarFlyout.ShowAt(_layer);
    }

    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
    public AnnotationLayerControl Layer => _layer;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
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

        var panel = new AnnotationStylePanel(_layer)
        {
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
            Margin = new Microsoft.UI.Xaml.Thickness(Math.Max(0, x), Math.Max(0, y), 0, 0),
        };

        // 押したまま動かすと移動する（パネルの上で完結させる）
        Windows.Foundation.Point? origin = null;
        Microsoft.UI.Xaml.Thickness originMargin = default;
        panel.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed)
            {
                return;
            }

            origin = e.GetCurrentPoint(this).Position;
            originMargin = panel.Margin;
            panel.CapturePointer(e.Pointer);
            e.Handled = true;
        };
        panel.PointerMoved += (_, e) =>
        {
            if (origin is not { } start)
            {
                return;
            }

            var current = e.GetCurrentPoint(this).Position;
            panel.Margin = new Microsoft.UI.Xaml.Thickness(
                originMargin.Left + (current.X - start.X),
                originMargin.Top + (current.Y - start.Y),
                0,
                0);
            e.Handled = true;
        };
        panel.PointerReleased += (_, e) =>
        {
            origin = null;
            panel.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        };

        _stylePanel = panel;
        Children.Add(panel);
    }

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel is not null;

    /// <summary>指定した位置が外観の編集パネルの上かどうかを判定する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <returns>パネルの上の場合は <see langword="true"/>。</returns>
    public bool IsStylePanelHit(double x, double y)
    {
        if (_stylePanel is not { } panel)
        {
            return false;
        }

        return x >= panel.Margin.Left && y >= panel.Margin.Top
            && x < panel.Margin.Left + panel.ActualWidth && y < panel.Margin.Top + panel.ActualHeight;
    }
    /// <summary>外観・重なり順の編集パネルを閉じる。</summary>
    public void CloseStylePanel()
    {
        if (_stylePanel is not { } panel)
        {
            return;
        }

        _stylePanel = null;
        Children.Remove(panel);
    }
}

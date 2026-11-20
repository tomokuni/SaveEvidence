using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

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

    /// <summary>外観パネルを載せるウィンドウ（開いている間だけ保持する）。</summary>
    private Window? _styleWindow;

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
    /// <b>独立したウィンドウへ出す</b>: アプリのウィンドウの子として置くと、ウィンドウの縁で切れて項目が
    /// 選べなくなる（実測 2026-11-21）。 位置はアプリのウィンドウへ収めない。<br/>
    /// <b>閉じるのは外側のクリックだけ</b>: ウィンドウが非アクティブになったら閉じる（ポインタが外れても閉じない）。
    /// 閉じるボタンは置かない。<br/>
    /// <b>パネルは移動できる</b>: 余白（見出し・枠）を押したまま動かすとウィンドウが動く。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
    {
        // 位置は OS のポインター位置を使う（DIP から画面座標への変換はウィンドウの枠の分だけ
        // ずれるため。 引数は 4 つの UI 版で同じ形に揃えるために受け取る）。
        _ = x;
        _ = y;

        CloseStylePanel();
        var panel = new AnnotationStylePanel(_layer);
        panel.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        var width = Math.Max(1, (int)Math.Ceiling(panel.DesiredSize.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(panel.DesiredSize.Height * scale));

        var window = new Window
        {
            Content = panel,
            ExtendsContentIntoTitleBar = true,
        };

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // 位置はポインタ（画面座標）。 アプリのウィンドウの内側へ収めない
        if (NativeCursor.TryGetScreenPosition(out var screenX, out var screenY))
        {
            window.AppWindow.Move(new PointInt32(screenX, screenY));
        }

        window.AppWindow.Resize(new SizeInt32(width, height));

        // 注釈の外を押したら閉じる（ウィンドウが非アクティブになる）。
        // 表示直後は入力の途中でいったん非アクティブになることがあるため、直後の通知は無視する
        var shownAt = Environment.TickCount64;
        window.Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated
                && Environment.TickCount64 - shownAt > DeactivateGraceMs)
            {
                CloseStylePanel();
            }
        };

        // 操作できる項目（ボタン・コンボボックス）の上では動かさない（押下を奪わない）
        var dragging = false;
        var last = default(Windows.Foundation.Point);
        panel.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler((_, e) =>
            {
                if (e.Handled || !e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                dragging = true;
                last = e.GetCurrentPoint(null).Position;
                panel.CapturePointer(e.Pointer);
                e.Handled = true;
            }),
            handledEventsToo: false);
        panel.PointerMoved += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            var current = e.GetCurrentPoint(null).Position;
            var dx = (int)Math.Round((current.X - last.X) * scale);
            var dy = (int)Math.Round((current.Y - last.Y) * scale);
            var origin = window.AppWindow.Position;
            window.AppWindow.Move(new PointInt32(origin.X + dx, origin.Y + dy));
            last = current;
            e.Handled = true;
        };
        panel.PointerReleased += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            panel.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        };

        // パネルの自然な寸法にウィンドウを合わせる。
        // 読み込み前の測定はスタイル適用前で小さいため、読み込んだ後に測り直す。
        // **寸法の変化で追従しない**: ウィンドウを縮めるとパネルも縮み、寸法が縮み続ける（実測 2026-11-21）。
        panel.Loaded += (_, _) =>
        {
            if (_styleWindow != window)
            {
                return;
            }

            panel.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var natural = panel.DesiredSize;
            if (natural.Width <= 0 || natural.Height <= 0)
            {
                return;
            }

            window.AppWindow.Resize(new SizeInt32(
                Math.Max(1, (int)Math.Ceiling(natural.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(natural.Height * scale))));
            panel.InvalidateMeasure();
        };

        _stylePanel = panel;
        _styleWindow = window;

        // 表示直後の押下の途中で非アクティブ化されるのを避けるため、入力の処理が終わってから出す
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_styleWindow != window)
            {
                return;
            }

            window.Activate();
        });
    }

    /// <summary>表示直後の非アクティブ化を無視する時間（ミリ秒）。</summary>
    private const int DeactivateGraceMs = 500;

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _styleWindow is not null;

    /// <summary>外観・重なり順の編集パネルを閉じる。</summary>
    public void CloseStylePanel()
    {
        if (_styleWindow is not { } window)
        {
            return;
        }

        _stylePanel = null;
        _styleWindow = null;
        window.Close();
    }
}

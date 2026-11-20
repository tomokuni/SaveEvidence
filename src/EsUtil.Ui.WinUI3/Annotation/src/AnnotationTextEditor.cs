using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（WinUI 3 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: レイヤー（画像・注釈・入力）と書式バーを持つ。 2 つを別々に置くと、
/// 位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// ウィンドウの生成・移動・破棄だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>ウィンドウの内側へ収めない</b>: 書式バーも右クリックの外観フライアウトも、独立した枠なしウィンドウ
/// として出す（アプリのウィンドウの縁で切れない）。
/// </remarks>
public sealed class AnnotationTextEditor : Grid, IAnnotationPanelHost
{
    /// <summary>書式バーと本文の間の余白（DIP）。</summary>
    private const double BarGap = 6;

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>本文の書式を操作するバー。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>パネルの外観（コアが単一所有する値）。</summary>
    private readonly AnnotationPanelAppearance _appearance = AnnotationPanelAppearance.Default;

    /// <summary>書式バーのウィンドウを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観フライアウトを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>表示中のパネルのウィンドウ（種別ごとに 1 つ）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Window> _windows = [];

    /// <summary>表示中のパネルの左上（物理ピクセル）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPointInt> _positions = [];

    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private Border? _draggingRoot;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);

        _formatBarPanel = new AnnotationPanelController(this, _appearance);
        _stylePanel = new AnnotationPanelController(this, _appearance);

        Children.Add(_layer);

        _layer.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
                ShowFormatBar();
                return;
            }

            _formatBarPanel.Close();
        };

        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_formatBarPanel.IsOpen)
            {
                _formatBar.Refresh();
            }
        };

        _layer.SelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
                ShowFormatBar();
            }
        };

        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);
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

    /// <summary>表示中のパネルの左上（物理ピクセル）を取得する。</summary>
    /// <value>表示していない場合は原点。</value>
    public EsPointInt PanelPosition
        => _positions.Count > 0 ? _positions.Values.First() : default;

    /// <summary>パネルのウィンドウを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPointInt screenPosition)
    {
        DestroyPanel(kind);

        var root = new Border
        {
            Child = kind == AnnotationPanelKind.FormatBar
                ? _formatBar
                : new AnnotationStylePanel(_layer),
            Background = ToBrush(appearance.Background),
            BorderBrush = ToBrush(appearance.Border),
            BorderThickness = new Thickness(appearance.BorderThickness),
            CornerRadius = new CornerRadius(appearance.CornerRadius),
            Padding = new Thickness(appearance.Padding),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var window = new Window
        {
            // ウィンドウの飾りを消す（残すとパネルの枠と二重になり、余計な辺枠に見える）
            ExtendsContentIntoTitleBar = true,
            Content = root,
        };

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // **Alt+Tab の一覧から外さない**: `IsShownInSwitchers = false` にするとウィンドウが
        // アクティブにならず、パネルが表示されない（実測 2026-11-21）。 タスク バーへ出さない
        // 手段が他に無いため、一覧へは出す（見た目と操作の正しさを優先する）。

        window.AppWindow.Move(new PointInt32(screenPosition.X, screenPosition.Y));

        // 寸法は中身に合わせる（幅も高さも中身で決まる）。 実寸はスタイル適用後に決まるため、
        // 読み込んだ後に測り直す
        var scale = RenderScale();
        root.Loaded += (_, _) => ResizeToContent(kind, window, root, appearance, scale);

        if (kind == AnnotationPanelKind.StyleEditor)
        {
            // 外側をクリックすると非アクティブになる（閉じるのは押下だけ）
            window.Activated += (_, e) =>
            {
                if (e.WindowActivationState == WindowActivationState.Deactivated)
                {
                    _stylePanel.Close();
                }
            };
        }

        // 余白（中身の無いところ）を押したまま動かすとウィンドウが動く
        root.PointerPressed += (_, e) => BeginPanelDrag(kind, root, e);
        root.PointerMoved += (_, e) => ContinuePanelDrag(kind, root, e);
        root.PointerReleased += (_, e) => EndPanelDrag(kind, root, e);

        _windows[kind] = window;
        _positions[kind] = screenPosition;

        // **表示は入力の処理が終わってから行う**: 押下の処理の中で新しいウィンドウを
        // アクティブにすると、内容が配置されないまま表示されない（実測 2026-11-21）。
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_windows.TryGetValue(kind, out var current) || !ReferenceEquals(current, window))
            {
                return;
            }

            window.Activate();

            // DWM の枠は表示後に消す（作成時はまだネイティブ ウィンドウが揃っていない）
            AnnotationPanelWindow.RemoveSystemFrame(
                WinRT.Interop.WindowNative.GetWindowHandle(window));
        });

        // **書式バーは本文へフォーカスを戻す**: アクティブがパネルへ移ると本文の入力（文字・IME）が
        // そちらへ行き、その場編集が成り立たない。
        if (kind == AnnotationPanelKind.FormatBar)
        {
            _ = _layer.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>パネルのウィンドウを移動する。</summary>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    public void MovePanel(EsPointInt screenPosition)
    {
        var entry = _windows.FirstOrDefault();
        if (entry.Value is not { } window)
        {
            return;
        }

        _positions[entry.Key] = screenPosition;
        window.AppWindow.Move(new PointInt32(screenPosition.X, screenPosition.Y));
    }

    /// <summary>表示中のパネルのウィンドウを閉じて破棄する。</summary>
    public void DestroyPanel()
    {
        foreach (var kind in _windows.Keys.ToArray())
        {
            DestroyPanel(kind);
        }
    }

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>位置と寿命はコアが決める</b>: ウィンドウの内側へ収めず、右クリックした位置へ出し、
    /// 外側をクリックすると閉じる。 余白を押したまま動かすと移動する。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
        => _stylePanel.Show(AnnotationPanelKind.StyleEditor, ToScreenPixels(new Windows.Foundation.Point(x, y)));

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>外観の編集パネルを閉じる。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>書式バーを本文の上へ出す（ウィンドウの内側へは収めない）。</summary>
    private void ShowFormatBar()
    {
        var body = _layer.GetSelectedBodyRect();
        _formatBarPanel.Show(
            AnnotationPanelKind.FormatBar, ToScreenPixels(new Windows.Foundation.Point(body.X, body.Y - BarGap)));
    }

    /// <summary>パネルの寸法を中身に合わせる。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="window">パネルのウィンドウ。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="scale">物理ピクセル / DIP の倍率。</param>
    /// <remarks>
    /// <b>後から寸法を測り直さない</b>: ウィンドウを縮めると中身も縮み、寸法が縮み続ける
    /// （実測 2026-11-21）。 読み込んだ時点の自然な寸法で 1 回だけ決める。
    /// </remarks>
    private void ResizeToContent(
        AnnotationPanelKind kind,
        Window window,
        Border root,
        AnnotationPanelAppearance appearance,
        double scale)
    {
        if (!_windows.TryGetValue(kind, out var current) || !ReferenceEquals(current, window))
        {
            return;
        }

        root.Measure(new Windows.Foundation.Size(
            appearance.MaxWidth, appearance.MaxContentHeight));
        var natural = root.DesiredSize;
        if (natural.Width <= 0 || natural.Height <= 0)
        {
            return;
        }

        window.AppWindow.Resize(new SizeInt32(
            (int)Math.Ceiling(Math.Min(natural.Width, appearance.MaxWidth) * scale),
            (int)Math.Ceiling(natural.Height * scale)));
    }

    /// <summary>このコントロールの座標（DIP）を画面座標（物理ピクセル）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>画面座標（物理ピクセル）。 取得できない場合は原点。</returns>
    private EsPointInt ToScreenPixels(Windows.Foundation.Point point)
    {
        if (AppWindowOf() is not { } appWindow)
        {
            return default;
        }

        var scale = RenderScale();
        var origin = TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0));
        return new EsPointInt(
            (int)Math.Round(appWindow.Position.X + ((origin.X + point.X) * scale)),
            (int)Math.Round(appWindow.Position.Y + ((origin.Y + point.Y) * scale)));
    }

    /// <summary>このコントロールが載っているウィンドウの <c>AppWindow</c> を取得する。</summary>
    /// <returns>取得できた場合は <c>AppWindow</c>。 取得できない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>XamlRoot の識別子から引く</b>: WinUI 3 の <c>Window</c> は <c>UIElement</c> ではないため、
    /// 親をたどってウィンドウへ到達できない。 表示中の島（<c>ContentIslandEnvironment</c>）が
    /// 自分のウィンドウの識別子を持つので、それを使う。<br/>
    /// <b>取得できなくても止めない</b>: 識別子が未確定の間は例外になる。 その場合は位置を既定値にして、
    /// パネル自体は出す（出せないより出すほうがよい）。
    /// </remarks>
    private AppWindow? AppWindowOf()
    {
        try
        {
            return XamlRoot?.ContentIslandEnvironment?.AppWindowId is { } windowId
                ? AppWindow.GetFromWindowId(windowId)
                : null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // 識別子が未確定の間は引けない。 位置は既定値にしてパネル自体は出す
            // （出せないより、少しずれた位置に出るほうがよい）
            return null;
        }
    }

    /// <summary>物理ピクセル / DIP の倍率を取得する。</summary>
    /// <returns>1 以上の倍率。</returns>
    private double RenderScale()
    {
        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        return scale > 0 ? scale : 1.0;
    }

    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(AnnotationPanelKind kind, Border root, PointerRoutedEventArgs e)
    {
        // 中身（ボタン・入力欄）の押下では動かさない（操作を奪わない）
        if (!ReferenceEquals(e.OriginalSource, root)
            || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed
            || !NativeCursor.TryGetScreenPosition(out var x, out var y))
        {
            return;
        }

        _draggingRoot = root;
        Controller(kind).BeginDrag(new EsPointInt(x, y));
        root.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>位置は OS のポインターで測る</b>: ウィンドウを動かしてもパネル内の座標は変わらないため、
    /// パネル内座標の差分では 1 回で止まる。
    /// </remarks>
    private void ContinuePanelDrag(AnnotationPanelKind kind, Border root, PointerRoutedEventArgs e)
    {
        if (!ReferenceEquals(_draggingRoot, root)
            || !NativeCursor.TryGetScreenPosition(out var x, out var y))
        {
            return;
        }

        Controller(kind).DragTo(new EsPointInt(x, y));
        e.Handled = true;
    }

    /// <summary>パネルのドラッグを終える。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">ポインターの引数。</param>
    private void EndPanelDrag(AnnotationPanelKind kind, Border root, PointerRoutedEventArgs e)
    {
        _draggingRoot = null;
        Controller(kind).EndDrag();
        root.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    /// <summary>種別ごとのコントローラーを取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>コントローラー。</returns>
    private AnnotationPanelController Controller(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar ? _formatBarPanel : _stylePanel;

    /// <summary>種別のパネルを閉じる（表示していなければ何もしない）。</summary>
    /// <param name="kind">パネルの種別。</param>
    private void DestroyPanel(AnnotationPanelKind kind)
    {
        Controller(kind).Close();

        if (!_windows.Remove(kind, out var window))
        {
            return;
        }

        _positions.Remove(kind);
        window.Content = null;
        window.Close();
    }

    /// <summary>コアの色を WinUI のブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>不透明な塗りつぶしのブラシ。</returns>
    private static SolidColorBrush ToBrush(DrawingColor color)
        => new(Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
}

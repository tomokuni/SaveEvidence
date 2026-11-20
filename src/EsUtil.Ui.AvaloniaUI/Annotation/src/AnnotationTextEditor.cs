using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール。</summary>
/// <remarks>
/// <b>セットで使う</b>: 本文の表示・キャレット・選択範囲は <see cref="AnnotationLayerControl"/> が描き、
/// 書式バーは独立したウィンドウとして本文の近くへ出す。 2 つを別々に置くと、位置合わせと表示の切り替えを
/// ホストが書くことになる（サンプルごとにずれる）。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// ウィンドウの生成・移動・破棄だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>ウィンドウの内側へ収めない</b>: 書式バーも右クリックの外観フライアウトも、アプリのウィンドウの外へ
/// 出してよい（狭いウィンドウで切れるのを避ける）。
/// </remarks>
public sealed class AnnotationTextEditor : Panel, IAnnotationPanelHost
{
    /// <summary>書式バーと本文の間の余白（DIP）。</summary>
    private const double BarGap = 6;

    /// <summary>書式バー（編集中だけ表示する）。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>書式バーの外観（コアが単一所有する値）。</summary>
    private readonly AnnotationPanelAppearance _appearance = AnnotationPanelAppearance.Default;

    /// <summary>書式バーのウィンドウを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観フライアウトを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>表示中のパネルのウィンドウ（種別ごとに 1 つ）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Window> _windows = [];

    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private Border? _draggingRoot;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);

        // 下地を持たないとパネルが入力の対象にならない（Avalonia は背景が無い面をヒットテストしない）
        Background = Brushes.Transparent;

        _formatBarPanel = new AnnotationPanelController(this, _appearance);
        _stylePanel = new AnnotationPanelController(this, _appearance);

        Children.Add(_layer);

        // 右クリックの外観フライアウト（位置はレイヤーが知らせる）
        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);

        // 編集の開始・終了でバーを出し入れする（本文の位置は編集のたびに変わりうる）
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

        // 文字・書式の変更で表示を追随させる（バーの現在値が古くならないようにする）
        _layer.DocumentChanged += (_, _) =>
        {
            if (_formatBarPanel.IsOpen)
            {
                _formatBar.Refresh();
            }
        };

        // 移動・サイズ変更で本文の位置が変わるため、バーの位置を追随させる
        _layer.SelectionChanged += (_, _) =>
        {
            if (_formatBarPanel.IsOpen)
            {
                ShowFormatBar();
            }
        };

        // 選択範囲の変更で、書式バーの押下状態（太字・斜体・下線・取り消し線）を追随させる
        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_formatBarPanel.IsOpen)
            {
                _formatBar.Refresh();
            }
        };
    }

    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
    /// <value>画像・文書・選択・元に戻すなどを操作する対象。</value>
    public AnnotationLayerControl Layer => _layer;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    /// <value>編集中の場合は <see langword="true"/>（書式バーが出ている）。</value>
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>書式バーの代表色パレット（■ を並べる格子）の列数を取得または設定する。</summary>
    /// <value>1 以上の列数。 既定は <see cref="AnnotationPalette.DefaultPaletteColumns"/>（8 列）。</value>
    public int PaletteColumns
    {
        get => _formatBar.PaletteColumns;
        set => _formatBar.PaletteColumns = value;
    }

    /// <summary>書式バーの色の選択の幅（DIP）を取得する。</summary>
    /// <value>「■ + 全角 4 文字 + 100%」と「■ + <c>#FFFFFFFF</c>」の広いほうの幅。</value>
    public double ColorWidth => _formatBar.ColorWidth;

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    /// <remarks>
    /// <b>右クリックのフライアウトと揃える</b>: 同じ値を <see cref="AnnotationLayerControl.ColorTextFormat"/> へも写す
    /// （書式バーとフライアウトで色の見え方が食い違わない）。
    /// </remarks>
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
        => _windows.Values.FirstOrDefault()?.Position is { } position
            ? new EsPointInt(position.X, position.Y)
            : default;

    /// <summary>パネルのウィンドウを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPointInt screenPosition)
    {
        DestroyPanel(kind);

        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var root = new Border
        {
            Child = kind == AnnotationPanelKind.FormatBar
                ? _formatBar
                : AnnotationStyleFlyout.BuildContent(_layer, appearance),
            Background = ToBrush(appearance.Background),
            BorderBrush = ToBrush(appearance.Border),
            BorderThickness = new Thickness(appearance.BorderThickness),
            CornerRadius = new CornerRadius(appearance.CornerRadius),
            Padding = new Thickness(appearance.Padding),
        };

        var window = new Window
        {
            // ウィンドウの飾りを消す（残すとパネルの枠と二重になり、余計な辺枠に見える）
            WindowDecorations = WindowDecorations.None,
            ShowInTaskbar = false,
            CanResize = false,
            CanMinimize = false,
            CanMaximize = false,
            Topmost = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(screenPosition.X, screenPosition.Y),
            Background = ToBrush(appearance.Background),
            Content = root,
            MaxWidth = appearance.MaxWidth,
            MaxHeight = appearance.MaxContentHeight,

            // **書式バーはアクティブにしない**: アクティブにすると本文の入力（文字・IME）が
            // そちらへ移り、その場編集が成り立たない。
            ShowActivated = kind == AnnotationPanelKind.StyleEditor,
        };

        if (kind == AnnotationPanelKind.StyleEditor)
        {
            // 外側をクリックすると非アクティブになる（閉じるのは押下だけ）
            window.Deactivated += (_, _) => _stylePanel.CloseOnDeactivate();
        }

        // 余白（中身の無いところ）を押したまま動かすとウィンドウが動く
        root.PointerPressed += (_, e) => BeginPanelDrag(kind, root, top, e);
        root.PointerMoved += (_, e) => ContinuePanelDrag(kind, root, top, e);
        root.PointerReleased += (_, e) => EndPanelDrag(kind, e);

        _windows[kind] = window;

        if (top is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        // OS の枠（DWM が描く 1 ピクセルの枠と角丸）を消す。 残すとパネルの枠と二重になる。
        // ハンドルは表示の後でないと取れないため Opened で行う
        window.Opened += (_, _) =>
            AnnotationPanelWindow.RemoveSystemFrame(window.TryGetPlatformHandle()?.Handle ?? nint.Zero);
    }

    /// <summary>パネルのウィンドウを移動する。</summary>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    public void MovePanel(EsPointInt screenPosition)
    {
        if (_windows.Values.FirstOrDefault() is { } window)
        {
            window.Position = new PixelPoint(screenPosition.X, screenPosition.Y);
        }
    }

    /// <summary>表示中のパネルのウィンドウを閉じて破棄する。</summary>
    public void DestroyPanel()
    {
        foreach (var kind in _windows.Keys.ToArray())
        {
            DestroyPanel(kind);
        }
    }

    /// <summary>右クリック位置へ外観フライアウトを出す（既に出ていれば閉じる）。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>位置と寿命はコアが決める</b>: ウィンドウの内側へ収めず、右クリックした位置へ出し、
    /// 外側をクリックすると閉じる。 見出し（余白）を押したまま動かすと移動する。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
        => _stylePanel.Show(AnnotationPanelKind.StyleEditor, ToScreenPixels(new Point(x, y)));

    /// <summary>外観フライアウトを閉じる（出ていなければ何もしない）。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>外観フライアウトが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>書式バーを本文の上へ出す（ウィンドウの内側へは収めない）。</summary>
    private void ShowFormatBar()
    {
        var body = _layer.GetSelectedBodyRect();
        _formatBarPanel.Show(
            AnnotationPanelKind.FormatBar, ToScreenPixels(new Point(body.X, body.Y - BarGap)));
    }

    /// <summary>このコントロールの座標（DIP）を画面座標（物理ピクセル）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>画面座標（物理ピクセル）。 取得できない場合は原点。</returns>
    private EsPointInt ToScreenPixels(Point point)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return default;
        }

        var screen = top.PointToScreen(point);
        return new EsPointInt(screen.X, screen.Y);
    }

    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="top">画面。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(
        AnnotationPanelKind kind, Border root, TopLevel top, PointerPressedEventArgs e)
    {
        // 中身（ボタン・入力欄）の押下では動かさない（操作を奪わない）
        if (!ReferenceEquals(e.Source, root)
            || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggingRoot = root;
        var cursor = top.PointToScreen(e.GetPosition(top));
        Controller(kind).BeginDrag(new EsPointInt(cursor.X, cursor.Y));
        e.Pointer.Capture(root);
        e.Handled = true;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="top">画面。</param>
    /// <param name="e">ポインターの引数。</param>
    private void ContinuePanelDrag(
        AnnotationPanelKind kind, Border root, TopLevel top, PointerEventArgs e)
    {
        if (!ReferenceEquals(_draggingRoot, root))
        {
            return;
        }

        var cursor = top.PointToScreen(e.GetPosition(top));
        Controller(kind).DragTo(new EsPointInt(cursor.X, cursor.Y));
        e.Handled = true;
    }

    /// <summary>パネルのドラッグを終える。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="e">ポインターの引数。</param>
    private void EndPanelDrag(AnnotationPanelKind kind, PointerReleasedEventArgs e)
    {
        _draggingRoot = null;
        Controller(kind).EndDrag();
        e.Pointer.Capture(null);
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

        window.Content = null;
        window.Close();
    }

    /// <summary>コアの色を Avalonia のブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>不透明な塗りつぶしのブラシ。</returns>
    private static IBrush ToBrush(DrawingColor color)
        => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

    /// <summary>子要素の寸法を測る（レイヤーのみ）。</summary>
    /// <param name="availableSize">使える領域。</param>
    /// <returns>必要な寸法（レイヤーの希望寸法）。</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        _layer.Measure(availableSize);
        return _layer.DesiredSize;
    }

    /// <summary>子要素を配置する（レイヤーは全面）。</summary>
    /// <param name="finalSize">最終的な領域。</param>
    /// <returns>配置した寸法。</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _layer.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

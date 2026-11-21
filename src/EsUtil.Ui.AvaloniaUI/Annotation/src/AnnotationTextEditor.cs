using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
/// 書式バーはアプリの画面へ重ねて出す。 2 つを別々に置くと、位置合わせと表示の切り替えを
/// ホストが書くことになる（サンプルごとにずれる）。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// 重ねる先への追加・移動・削除だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>独立したウィンドウを作らない</b>: ウィンドウはタスク バーへ出る・アプリの後ろへ回る・フォーカスを
/// 奪うといった問題が版ごとに出るため、アプリの画面の最上位（<see cref="OverlayLayer"/>）へ重ねる。
/// 画像（プレビュー）の内側では切れない。
/// </remarks>
public sealed class AnnotationTextEditor : Panel, IAnnotationPanelHost
{
    /// <summary>書式バーと本文の間の余白（DIP）。</summary>
    private const double BarGap = 6;

    /// <summary>書式バー（編集中だけ表示する）。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>書式バーを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観フライアウトを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>表示中のパネルの根（種別ごとに 1 つ。 使い回す）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Border> _roots = [];

    /// <summary>パネルを重ねた先（アプリの画面の最上位）。</summary>
    private OverlayLayer? _overlay;

    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private Border? _draggingRoot;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);

        // 下地を持たないとパネルが入力の対象にならない（Avalonia は背景が無い面をヒットテストしない）
        Background = Brushes.Transparent;

        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar);
        _stylePanel = new AnnotationPanelController(this, AnnotationPanelAppearance.StyleEditor);

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

    /// <summary>パネルを置ける領域（DIP）を取得する。</summary>
    /// <value>アプリの画面の全体（重ねた先の大きさ）。</value>
    public EsRect AvailableArea
        => _overlay is { } overlay && overlay.Bounds.Width > 0
            ? new EsRect(0, 0, overlay.Bounds.Width, overlay.Bounds.Height)
            : TopLevel.GetTopLevel(this) is { } top
                ? new EsRect(0, 0, top.ClientSize.Width, top.ClientSize.Height)
                : default;

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <value>表示していない場合は原点。</value>
    public EsPoint PanelPosition
        => _roots.Values.FirstOrDefault(root => Canvas.GetLeft(root) is not double.NaN) is { } root
            ? new EsPoint(
                double.IsNaN(Canvas.GetLeft(root)) ? 0 : Canvas.GetLeft(root),
                double.IsNaN(Canvas.GetTop(root)) ? 0 : Canvas.GetTop(root))
            : default;

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <value>表示していない場合は原点。</value>
    public EsSize PanelSize
        => _roots.Values.FirstOrDefault() is { } root
            ? new EsSize(root.Bounds.Width, root.Bounds.Height)
            : default;

    /// <summary>パネルを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position)
    {
        DestroyPanel(kind);

        if (OverlayLayer.GetOverlayLayer(this) is not { } overlay)
        {
            return;
        }

        _overlay = overlay;

        // **根は種別ごとに 1 度だけ作って使い回す**: 書式バーは作り直さない（購読が増えるため）。
        // 根を作り直すと、前の根が書式バーを子として保持したままになり、付け替えに失敗する
        // （実測 2026-11-20: WinUI 3 版で E_INVALIDARG になった）。
        if (!_roots.TryGetValue(kind, out var root))
        {
            root = new Border
            {
                Child = kind == AnnotationPanelKind.FormatBar
                    ? _formatBar
                    : AnnotationStyleFlyout.BuildContent(_layer, appearance),
            };

            _roots[kind] = root;
        }

        root.Background = ToBrush(appearance.Background);
        root.BorderBrush = ToBrush(appearance.Border);
        root.BorderThickness = new Thickness(appearance.BorderThickness);
        root.CornerRadius = new CornerRadius(appearance.CornerRadius);
        root.Padding = new Thickness(
            appearance.Padding, appearance.VerticalPadding,
            appearance.Padding, appearance.VerticalPadding);

        // 余白（中身の無いところ）を押したまま動かすとパネルが動く
        root.PointerPressed += (_, e) => BeginPanelDrag(kind, root, e);
        root.PointerMoved += (_, e) => ContinuePanelDrag(kind, root, e);
        root.PointerReleased += (_, e) => EndPanelDrag(kind, e);

        if (!overlay.Children.Contains(root))
        {
            overlay.Children.Add(root);
        }

        Canvas.SetLeft(root, position.X);
        Canvas.SetTop(root, position.Y);
    }

    /// <summary>パネルを移動する。</summary>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void MovePanel(EsPoint position)
    {
        if (_roots.Values.FirstOrDefault() is not { } root)
        {
            return;
        }

        Canvas.SetLeft(root, position.X);
        Canvas.SetTop(root, position.Y);
    }

    /// <summary>表示中のパネルを画面から外す（根と中身は残す）。</summary>
    public void DestroyPanel()
    {
        foreach (var kind in _roots.Keys.ToArray())
        {
            DestroyPanel(kind);
        }
    }

    /// <summary>右クリック位置へ外観フライアウトを出す（既に出ていれば閉じる）。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>位置と寿命はコアが決める</b>: アプリの画面の内側へ収め、右クリックした位置へ出し、
    /// 外側をクリックすると閉じる。 余白を押したまま動かすと移動する。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
        => _stylePanel.Show(AnnotationPanelKind.StyleEditor, ToOverlayPoint(new Point(x, y)));

    /// <summary>外観フライアウトを閉じる（出ていなければ何もしない）。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>外観フライアウトが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>書式バーを本文の外（本文に被さらない位置）へ出し直す。</summary>
    /// <remarks>
    /// <b>本文に被せない</b>: 編集中の文字が隠れて読めない（実測 2026-11-20）。 位置の判断は
    /// コア（<see cref="AnnotationPanelPlacement.ResolveOutside"/>）が単一所有する。
    /// <b>大きさは実寸を使う</b>: 中身（フォント名・大きさ・色）で幅が決まるため、固定値だと
    /// 枠からはみ出す。 一度置いてから実寸で出し直す。
    /// </remarks>
    private void ShowFormatBar()
    {
        var body = ToOverlayRect(ToEsRect(_layer.GetSelectedBodyRect()));
        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar, body, MeasureFormatBar(), BarGap);

        // 実寸が測れたら、その大きさで置き直す（初回は中身が未測定のため）
        if (_roots.TryGetValue(AnnotationPanelKind.FormatBar, out var root))
        {
            var size = MeasureFormatBar();
            if (root.Bounds.Width > 0 && Math.Abs(root.Bounds.Width - size.Width) > 0.5)
            {
                _formatBarPanel.ShowOutside(AnnotationPanelKind.FormatBar, body, size, BarGap);
            }
        }
    }

    /// <summary>書式バーの大きさ（DIP）を測る。</summary>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private EsSize MeasureFormatBar()
    {
        if (_roots.TryGetValue(AnnotationPanelKind.FormatBar, out var root)
            && root.Bounds.Width > 0 && root.Bounds.Height > 0)
        {
            return new EsSize(root.Bounds.Width, root.Bounds.Height);
        }

        _formatBar.Measure(Size.Infinity);
        var bounds = _formatBar.Bounds.Size;
        var size = bounds.Width > 0 ? bounds : _formatBar.DesiredSize;
        var appearance = AnnotationPanelAppearance.FormatBar;
        return new EsSize(
            size.Width + (appearance.Padding * 2) + (appearance.BorderThickness * 2),
            size.Height + (appearance.VerticalPadding * 2) + (appearance.BorderThickness * 2));
    }

    /// <summary>種別のパネルを画面から外す（根と中身は残す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    private void DestroyPanel(AnnotationPanelKind kind)
    {
        Controller(kind).Close();

        if (_roots.TryGetValue(kind, out var root))
        {
            _overlay?.Children.Remove(root);
        }
    }

    /// <summary>このコントロールの座標（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>重ねた先の座標。 取得できない場合はそのまま返す。</returns>
    private EsPoint ToOverlayPoint(Point point)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return new EsPoint(point.X, point.Y);
        }

        var screen = top.PointToClient(top.PointToScreen(point));
        return new EsPoint(screen.X, screen.Y);
    }

    /// <summary>このコントロールの矩形（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="rect">このコントロールの矩形。</param>
    /// <returns>重ねた先の矩形。</returns>
    private EsRect ToOverlayRect(EsRect rect)
    {
        var origin = ToOverlayPoint(new Point(rect.X, rect.Y));
        return new EsRect(origin.X, origin.Y, rect.Width, rect.Height);
    }

    /// <summary>コントロールの矩形をコアの矩形へ変換する。</summary>
    /// <param name="rect">コントロールの矩形。</param>
    /// <returns>コアの矩形。</returns>
    private static EsRect ToEsRect(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(AnnotationPanelKind kind, Border root, PointerPressedEventArgs e)
    {
        // 中身（ボタン・入力欄）の押下では動かさない（操作を奪わない）
        if (!ReferenceEquals(e.Source, root)
            || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggingRoot = root;
        Controller(kind).BeginDrag(ToOverlayPoint(e.GetPosition(this)));
        e.Pointer.Capture(root);
        e.Handled = true;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">ポインターの引数。</param>
    private void ContinuePanelDrag(AnnotationPanelKind kind, Border root, PointerEventArgs e)
    {
        if (!ReferenceEquals(_draggingRoot, root))
        {
            return;
        }

        Controller(kind).DragTo(ToOverlayPoint(e.GetPosition(this)));
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

using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（WinUI 3 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: レイヤー（画像・注釈・入力）と書式バーを持つ。 2 つを別々に置くと、
/// 位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// 重ねる先への追加・移動・削除だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>独立したウィンドウを作らない</b>: ウィンドウはタスク バーへ出る・アプリの後ろへ回る・フォーカスを
/// 奪うといった問題が版ごとに出るため、アプリの画面の最上位（<c>XamlRoot</c> の根の <c>Canvas</c>）へ重ねる。
/// 画像（プレビュー）の内側では切れない。
/// </remarks>
public sealed class AnnotationTextEditor : Grid, IAnnotationPanelHost
{
    /// <summary>書式バーと本文の間の余白（DIP）。</summary>
    private const double BarGap = 6;

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>本文の書式を操作するバー。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>書式バーを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観フライアウトを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>表示中のパネルの根（種別ごとに 1 つ。 使い回す）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Border> _roots = [];

    /// <summary>表示中のパネルの左上（DIP）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];

    /// <summary>パネルを重ねた先（アプリの画面の最上位）。</summary>
    private Canvas? _overlay;

    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private Border? _draggingRoot;

    /// <summary>キー入力を根で受けているかどうか（編集中だけ受け取る）。</summary>
    private bool _isWatchingInput;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);

        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar);
        _stylePanel = new AnnotationPanelController(this, AnnotationPanelAppearance.StyleEditor);

        Children.Add(_layer);

        _layer.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
                StartWatchingInput();
                ShowFormatBar();
                return;
            }

            StopWatchingInput();
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

    /// <summary>パネルを置ける領域（DIP）を取得する。</summary>
    /// <value>アプリの画面の全体。</value>
    public EsRect AvailableArea
        => _overlay is { ActualWidth: > 0 } overlay
            ? new EsRect(0, 0, overlay.ActualWidth, overlay.ActualHeight)
            : XamlRoot?.Content is FrameworkElement root && root.ActualWidth > 0
                ? new EsRect(0, 0, root.ActualWidth, root.ActualHeight)
                : default;

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <value>表示していない場合は原点。</value>
    public EsPoint PanelPosition
        => _positions.Count > 0 ? _positions.Values.First() : default;

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <value>表示していない場合は原点。</value>
    public EsSize PanelSize
        => _roots.Values.FirstOrDefault() is { } root
            ? new EsSize(root.ActualWidth, root.ActualHeight)
            : default;

    /// <summary>パネルを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position)
    {
        DestroyPanel(kind);

        if (ResolveOverlay() is not { } canvas)
        {
            return;
        }

        // 根は種別ごとに 1 度だけ作って使い回す（中身を作り直すと購読が増える）
        if (!_roots.TryGetValue(kind, out var root))
        {
            root = new Border
            {
                Child = kind == AnnotationPanelKind.FormatBar
                    ? _formatBar
                    : new AnnotationStylePanel(_layer),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
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

        root.PointerPressed += (_, e) => BeginPanelDrag(kind, root, canvas, e);
        root.PointerMoved += (_, e) => ContinuePanelDrag(kind, root, canvas, e);
        root.PointerReleased += (_, e) => EndPanelDrag(kind, root, e);

        if (!canvas.Children.Contains(root))
        {
            canvas.Children.Add(root);
        }

        Canvas.SetLeft(root, position.X);
        Canvas.SetTop(root, position.Y);
        _positions[kind] = position;
    }

    /// <summary>パネルを移動する。</summary>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void MovePanel(EsPoint position)
    {
        var entry = _roots.FirstOrDefault();
        if (entry.Value is not { } root)
        {
            return;
        }

        _positions[entry.Key] = position;
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

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>位置と寿命はコアが決める</b>: アプリの画面の内側へ収め、右クリックした位置へ出し、
    /// 外側をクリックすると閉じる。 余白を押したまま動かすと移動する。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
        => _stylePanel.Show(
            AnnotationPanelKind.StyleEditor, ToOverlayPoint(new Windows.Foundation.Point(x, y)));

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>外観の編集パネルを閉じる。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>書式バーを本文の外（本文に被さらない位置）へ出し直す。</summary>
    /// <remarks>
    /// <b>本文に被せない</b>: 編集中の文字が隠れて読めない（実測 2026-11-20）。 位置の判断は
    /// コア（<see cref="AnnotationPanelPlacement.ResolveOutside"/>）が単一所有する。<br/>
    /// <b>大きさは実寸を使う</b>: 中身（フォント名・大きさ・色）で幅が決まるため、固定値だと
    /// 枠からはみ出す。 一度置いてから実寸で出し直す。
    /// </remarks>
    private void ShowFormatBar()
    {
        var body = ToOverlayRect(_layer.GetSelectedBodyRect());
        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar, body, MeasureFormatBar(), BarGap);

        if (_roots.TryGetValue(AnnotationPanelKind.FormatBar, out var root)
            && root.ActualWidth > 0)
        {
            var size = MeasureFormatBar();
            if (Math.Abs(root.ActualWidth - size.Width) > 0.5)
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
            && root.ActualWidth > 0 && root.ActualHeight > 0)
        {
            return new EsSize(root.ActualWidth, root.ActualHeight);
        }

        var appearance = AnnotationPanelAppearance.FormatBar;
        _formatBar.Measure(new Windows.Foundation.Size(
            double.PositiveInfinity, double.PositiveInfinity));
        var size = _formatBar.DesiredSize;
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

    /// <summary>パネルを重ねる先（アプリの画面の最上位）を取得する。</summary>
    /// <returns>重ねる先。 取得できない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>根の <c>Canvas</c> を使う</b>: プレビューの内側へ置くと画像の外で切れる。 根の
    /// <c>Panel</c> の最後の子として <c>Canvas</c> を足すと、最前面に置け、子は座標で配置できる。
    /// </remarks>
    private Canvas? ResolveOverlay()
    {
        if (_overlay is { } existing && existing.XamlRoot is not null)
        {
            return existing;
        }

        if (XamlRoot?.Content is not Panel rootPanel)
        {
            return null;
        }

        var canvas = new Canvas();
        rootPanel.Children.Add(canvas);
        _overlay = canvas;
        return canvas;
    }

    /// <summary>このコントロールの座標（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>重ねた先の座標。 取得できない場合はそのまま返す。</returns>
    private EsPoint ToOverlayPoint(Windows.Foundation.Point point)
    {
        if (_overlay is not { } canvas)
        {
            return new EsPoint(point.X, point.Y);
        }

        try
        {
            var local = TransformToVisual(canvas).TransformPoint(point);
            return new EsPoint(local.X, local.Y);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // ツリーへ繋がっていない間は変換できない。 位置はそのまま使う（見えないより出すほうがよい）
            return new EsPoint(point.X, point.Y);
        }
    }

    /// <summary>このコントロールの矩形（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="rect">このコントロールの矩形。</param>
    /// <returns>重ねた先の矩形。</returns>
    private EsRect ToOverlayRect(EsRect rect)
    {
        var origin = ToOverlayPoint(new Windows.Foundation.Point(rect.X, rect.Y));
        return new EsRect(origin.X, origin.Y, rect.Width, rect.Height);
    }

    /// <summary>編集中だけ、根でキー入力と文字入力を受け取る。</summary>
    /// <remarks>
    /// <b>フォーカスに依存しない</b>: レイヤー以外へフォーカスが残っていても文字を入力できるよう、
    /// 根（<c>XamlRoot</c> の内容）で受け取ってレイヤーへ渡す（実測 2026-11-21: レイヤーへ
    /// フォーカスが移らず文字を入力できなかった）。
    /// </remarks>
    private void StartWatchingInput()
    {
        if (_isWatchingInput || XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        _isWatchingInput = true;
        root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootKeyDown), handledEventsToo: true);
        root.AddHandler(
            UIElement.CharacterReceivedEvent,
            new Windows.Foundation.TypedEventHandler<UIElement, CharacterReceivedRoutedEventArgs>(OnRootCharacterReceived),
            handledEventsToo: true);
    }

    /// <summary>根で受けたキー入力をレイヤーへ渡すのを止める。</summary>
    private void StopWatchingInput()
    {
        if (!_isWatchingInput || XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        _isWatchingInput = false;
        root.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootKeyDown));
        root.RemoveHandler(
            UIElement.CharacterReceivedEvent,
            new Windows.Foundation.TypedEventHandler<UIElement, CharacterReceivedRoutedEventArgs>(OnRootCharacterReceived));
    }

    /// <summary>根で受けたキー入力をレイヤーへ渡す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;

        if (_layer.IsEditingText)
        {
            _layer.ForwardKey(e);
        }
    }

    /// <summary>根で受けた文字入力をレイヤーへ渡す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">文字入力の引数。</param>
    private void OnRootCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        _ = sender;

        if (_layer.IsEditingText)
        {
            _layer.ForwardCharacter(e);
        }
    }

    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="canvas">重ねた先。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(
        AnnotationPanelKind kind, Border root, Canvas canvas, PointerRoutedEventArgs e)
    {
        // 中身（ボタン・入力欄）の押下では動かさない（操作を奪わない）
        if (!ReferenceEquals(e.OriginalSource, root)
            || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggingRoot = root;
        var position = e.GetCurrentPoint(canvas).Position;
        Controller(kind).BeginDrag(new EsPoint(position.X, position.Y));
        root.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="canvas">重ねた先。</param>
    /// <param name="e">ポインターの引数。</param>
    private void ContinuePanelDrag(
        AnnotationPanelKind kind, Border root, Canvas canvas, PointerRoutedEventArgs e)
    {
        if (!ReferenceEquals(_draggingRoot, root))
        {
            return;
        }

        var position = e.GetCurrentPoint(canvas).Position;
        Controller(kind).DragTo(new EsPoint(position.X, position.Y));
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

    /// <summary>コアの色を WinUI のブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>不透明な塗りつぶしのブラシ。</returns>
    private static SolidColorBrush ToBrush(DrawingColor color)
        => new(Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
}

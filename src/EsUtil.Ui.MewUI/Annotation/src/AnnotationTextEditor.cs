using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（MewUI 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: 本文の表示・キャレット・選択範囲は <see cref="AnnotationLayerControl"/> が描き、
/// 書式バーは独立したウィンドウとして本文の近くへ出す。 2 つを別々に置くと、位置合わせと表示の切り替えを
/// ホストが書くことになる。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// ウィンドウの生成・移動・破棄だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>ウィンドウの内側へ収めない</b>: 書式バーも右クリックの外観フライアウトも、アプリのウィンドウの外へ
/// 出してよい。 MewUI の <c>Popup</c> はウィンドウの内側に描かれ、マウス入力も受け取れないため使わない。
/// </remarks>
public sealed class AnnotationTextEditor : Panel, IAnnotationPanelHost
{
    /// <summary>書式バーと本文の間の余白（DIP）。</summary>
    private const double BarGap = 6;

    /// <summary>書式バー（編集中だけ表示する）。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>パネルの外観（コアが単一所有する値）。</summary>
    private readonly AnnotationPanelAppearance _appearance = AnnotationPanelAppearance.StyleEditor;

    /// <summary>書式バーのウィンドウを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観フライアウトを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>表示中のパネルのウィンドウ（種別ごとに 1 つ）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Window> _windows = [];

    /// <summary>表示中のパネルの左上（物理ピクセル）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPointInt> _positions = [];

    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private UIElement? _draggingRoot;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);

        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar);
        _stylePanel = new AnnotationPanelController(this, _appearance);

        Add(_layer);

        // 編集の開始・終了でバーを出し入れする（本文の位置は編集のたびに変わりうる）
        _layer.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
                ShowFormatBar(_layer.GetSelectedBodyRect());
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
                ShowFormatBar(_layer.GetSelectedBodyRect());
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

        // 右クリックの編集パネル（外観・吹き出し・重なり順）
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

    /// <summary>表示中のパネルの大きさ（物理ピクセル）を取得する。</summary>
    /// <value>表示していない場合は原点。</value>
    public EsSizeInt PanelSize
        => _windows.Values.FirstOrDefault() is { } window
            ? new EsSizeInt(
                (int)Math.Ceiling(window.Width * Scale(window)),
                (int)Math.Ceiling(window.Height * Scale(window)))
            : default;

    /// <summary>パネルを置く画面の作業領域（物理ピクセル）を取得する。</summary>
    /// <value>取得できない場合は仮想スクリーン全体。</value>
    public EsRect WorkArea
        => MewUiScreen.TryGetWorkArea(out var area) ? area : default;

    /// <summary>パネルのウィンドウを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPointInt screenPosition)
    {
        DestroyPanel(kind);

        if (FindVisualRoot() is not Window owner)
        {
            return;
        }

        var root = new Border
        {
            Child = kind == AnnotationPanelKind.FormatBar
                ? _formatBar
                : new AnnotationStylePanel(_layer, appearance),
            Background = ToColor(appearance.Background),
            BorderBrush = ToColor(appearance.Border),
            BorderThickness = appearance.BorderThickness,
            CornerRadius = appearance.CornerRadius,
            Padding = new Thickness(
                appearance.Padding, appearance.VerticalPadding,
                appearance.Padding, appearance.VerticalPadding),
        };

        var window = new Window
        {
            // ウィンドウの飾りを消す（残すとパネルの枠と二重になり、余計な辺枠に見える）
            Borderless = true,
            BorderThickness = 0,
            CornerRadius = 0,
            Background = ToColor(appearance.Background),
            ShowInTaskbar = false,
            Topmost = true,
            StartupLocation = WindowStartupLocation.Manual,
            Content = root,
            WindowSize = WindowSize.FitContentSize(
                appearance.MaxWidth, appearance.MaxContentHeight),
        };

        // 外側をクリックすると非アクティブになる（閉じるのは押下だけ）
        window.Deactivated += () =>
        {
            if (kind == AnnotationPanelKind.StyleEditor)
            {
                _stylePanel.CloseOnDeactivate();
            }
        };

        // 余白（中身の無いところ）を押したまま動かすとウィンドウが動く。
        // MewUI のイベントは子から親へ伝播しないため、子（ボタン・入力欄）の押下はここへ来ない
        root.OnMouseDown(e => BeginPanelDrag(kind, root, e));
        root.OnMouseMove(e => ContinuePanelDrag(kind, root, e));
        root.OnMouseUp(e => EndPanelDrag(kind, e));

        _windows[kind] = window;
        _positions[kind] = screenPosition;
        window.StartManualPosition(
            screenPosition.X / Scale(owner), screenPosition.Y / Scale(owner));
        window.Show(owner);

        // OS の枠（DWM が描く 1 ピクセルの枠と角丸）を消す。 残すとパネルの枠と二重になる
        AnnotationPanelWindow.RemoveSystemFrame(window.Handle);

        // **書式バーはアプリへアクティブを戻す**: アクティブがパネルへ移ると本文の入力
        // （文字・IME）がそちらへ行き、その場編集が成り立たない。
        if (kind == AnnotationPanelKind.FormatBar)
        {
            owner.Activate();
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
        var scale = Scale(window);
        window.MoveTo(screenPosition.X / scale, screenPosition.Y / scale);
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
        => _stylePanel.Show(AnnotationPanelKind.StyleEditor, ToScreenPixels(new Point(x, y)));

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>外観の編集パネルを閉じる。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>書式バーを本文の外（本文に被さらない位置）へ出す。</summary>
    /// <param name="bodyDip">本文の矩形（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>本文に被せない</b>: 編集中の文字が隠れて読めない（実測 2026-11-20）。 位置の判断は
    /// コア（<see cref="AnnotationPanelPlacement.ResolveOutside"/>）が単一所有する。
    /// </remarks>
    private void ShowFormatBar(EsRect bodyDip)
    {
        var target = ToScreenRect(bodyDip);
        var scale = RenderScale();
        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar,
            target,
            new EsSizeInt(
                (int)Math.Ceiling(Math.Max(1, _formatBar.DesiredSize.Width) * scale),
                (int)Math.Ceiling(Math.Max(1, _formatBar.DesiredSize.Height) * scale)),
            (int)Math.Round(BarGap * scale));
    }

    /// <summary>このコントロールの座標（DIP）を画面座標（物理ピクセル）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>画面座標（物理ピクセル）。 取得できない場合は原点。</returns>
    private EsPointInt ToScreenPixels(Point point)
    {
        var rect = ToScreenRect(new EsRect(point.X, point.Y, 0, 0));
        return new EsPointInt((int)Math.Round(rect.X), (int)Math.Round(rect.Y));
    }

    /// <summary>このコントロールの矩形（DIP）を画面座標（物理ピクセル）へ変換する。</summary>
    /// <param name="rect">このコントロールの矩形。</param>
    /// <returns>画面座標の矩形（物理ピクセル）。</returns>
    private EsRect ToScreenRect(EsRect rect)
    {
        if (FindVisualRoot() is not Window owner)
        {
            return default;
        }

        var client = owner.PointToScreen(new Point(0, 0));
        var scale = RenderScale();
        var local = _layer.TranslatePoint(new Point(rect.X, rect.Y), this);
        return new EsRect(
            client.X + (local.X * scale),
            client.Y + (local.Y * scale),
            rect.Width * scale,
            rect.Height * scale);
    }

    /// <summary>物理ピクセル / DIP の倍率を取得する。</summary>
    /// <returns>1 以上の倍率。</returns>
    private double RenderScale()
        => FindVisualRoot() is Window owner ? Scale(owner) : 1.0;

    /// <summary>ウィンドウの倍率（物理ピクセル / DIP）を取得する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <returns>1 以上の倍率。</returns>
    private static double Scale(Window window) => window.DpiScale > 0 ? window.DpiScale : 1.0;

    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(AnnotationPanelKind kind, Border root, MouseEventArgs e)
    {
        if (!e.LeftButton || !MewUiNativeCursor.TryGetScreenPosition(out var x, out var y))
        {
            return;
        }

        _draggingRoot = root;
        Controller(kind).BeginDrag(new EsPointInt(x, y));
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>位置は OS のポインターで測る</b>: ウィンドウを動かしてもパネル内の座標は変わらないため、
    /// パネル内座標の差分では 1 回で止まる（実測 2026-11-21）。
    /// </remarks>
    private void ContinuePanelDrag(AnnotationPanelKind kind, Border root, MouseEventArgs e)
    {
        _ = e;

        if (!ReferenceEquals(_draggingRoot, root)
            || !MewUiNativeCursor.TryGetScreenPosition(out var x, out var y))
        {
            return;
        }

        Controller(kind).DragTo(new EsPointInt(x, y));
    }

    /// <summary>パネルのドラッグを終える。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="e">ポインターの引数。</param>
    private void EndPanelDrag(AnnotationPanelKind kind, MouseEventArgs e)
    {
        _ = e;
        _draggingRoot = null;
        Controller(kind).EndDrag();
    }

    /// <summary>種別ごとのコントローラーを取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>コントローラー。</returns>
    private AnnotationPanelController Controller(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar ? _formatBarPanel : _stylePanel;

    /// <summary>種別のパネルを閉じる（表示していなければ何もしない）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <remarks>
    /// <b>中身を先に外す</b>: ウィンドウを閉じても中身（書式バー）は根の子のまま残る。 外さずに
    /// 次を作ると、既に親を持つ要素として追加されず、<b>2 回目以降にバーが表示されない</b>
    /// （実測 2026-11-20）。
    /// </remarks>
    private void DestroyPanel(AnnotationPanelKind kind)
    {
        Controller(kind).Close();

        if (!_windows.Remove(kind, out var window))
        {
            return;
        }

        _positions.Remove(kind);
        if (window.Content is Border root)
        {
            root.Child = null;
        }

        window.Content = null;
        window.Close();
    }

    /// <summary>コアの色を MewUI の色へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>MewUI の色。</returns>
    private static MewUIColor ToColor(System.Drawing.Color color)
        => new(color.A, color.R, color.G, color.B);

    /// <summary>子要素の寸法を測る（レイヤーのみ）。</summary>
    /// <param name="availableSize">使える領域。</param>
    /// <returns>必要な寸法（レイヤーの希望寸法）。</returns>
    protected override Size MeasureContent(Size availableSize)
    {
        _layer.Measure(availableSize);
        return _layer.DesiredSize;
    }

    /// <summary>子要素を配置する（レイヤーは全面）。</summary>
    /// <param name="bounds">最終的な領域。</param>
    protected override void ArrangeContent(Rect bounds)
        => _layer.Arrange(new Rect(0, 0, bounds.Width, bounds.Height));
}

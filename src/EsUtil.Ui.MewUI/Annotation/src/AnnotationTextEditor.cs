using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（MewUI 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: 本文の表示・キャレット・選択範囲は <see cref="AnnotationLayerControl"/> が描き、
/// 書式バーはアプリの画面へ重ねて出す。 2 つを別々に置くと、位置合わせと表示の切り替えを
/// ホストが書くことになる。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// 重ねる先への追加・移動・削除だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>独立したウィンドウを作らない</b>: ウィンドウはタスク バーへ出る・アプリの後ろへ回る・フォーカスを
/// 奪うといった問題が版ごとに出るため、アプリの画面の最上位（<c>Window.OverlayLayer</c>）へ重ねる。
/// 画像（プレビュー）の内側では切れない。
/// </remarks>
public sealed class AnnotationTextEditor : Panel, IAnnotationPanelHost
{
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

    /// <summary>表示中のパネルの左上（DIP）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];

    /// <summary>パネルを重ねた先（アプリの画面の最上位）。</summary>
    private OverlayLayer? _overlay;

    /// <summary>書式バーの対象（注釈の矩形）。 実寸が確定したらこの矩形で置き直す。</summary>
    private EsRect? _formatBarTarget;

    /// <summary>描画ごとの置き直しを繋いだかどうか（二重に繋がない）。</summary>
    private bool _frameHook;

    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private UIElement? _draggingRoot;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);

        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar);
        _stylePanel = new AnnotationPanelController(this, AnnotationPanelAppearance.StyleEditor);

        Add(_layer);

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

        // 右クリックの編集パネル（外観・吹き出し・重なり順）
        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);

        // パネルの外側の押下で閉じる（MewUI のイベントは子から親へ伝播しないため、
        // プレビューの押下をレイヤーから受け取ってコアへ知らせる）
        _layer.PreviewGestureRequested += (_, e) =>
        {
            if (e.Gesture == AnnotationPreviewGesture.Press)
            {
                _stylePanel.CloseIfOutside(ToOverlayPoint(new Point(e.X, e.Y)));
            }
        };
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
    /// <value>注釈レイヤー（画像の表示領域）が占める範囲（ウィンドウの座標）。</value>
    /// <remarks>
    /// <b>画像の表示領域に収める</b>: ページ全体を領域にすると、ツール バーや余白の外へ出て
    /// 画像の外に表示される（実測 2026-11-21）。 画像の表示領域に収めれば、画像の内側で切れない。
    /// </remarks>
    public EsRect AvailableArea
    {
        get
        {
            var origin = ToOverlayPoint(default);
            var width = _layer.Bounds.Width > 0 ? _layer.Bounds.Width : RenderSize.Width;
            var height = _layer.Bounds.Height > 0 ? _layer.Bounds.Height : RenderSize.Height;
            return width > 0 && height > 0
                ? new EsRect(origin.X, origin.Y, width, height)
                : default;
        }
    }

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>左上の位置。 表示していない場合は原点。</returns>
    public EsPoint GetPanelPosition(AnnotationPanelKind kind)
        => _positions.TryGetValue(kind, out var position) ? position : default;

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>パネルの大きさ。 表示していない場合は原点。</returns>
    public EsSize GetPanelSize(AnnotationPanelKind kind) => MeasurePanel(kind);

    /// <summary>パネルを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position)
    {
        RemovePanel(kind);

        if (FindVisualRoot() is not Window owner || owner.OverlayLayer is not { } overlayLayer)
        {
            return;
        }

        // **重ねる先はアプリの画面の最上位**: プレビューの内側へ置くと画像の外で切れる。
        // **全面を覆う Canvas を挟まない**: 挟むとパネルの外側の押下を吸ってしまい、注釈も
        // 書式バーも操作できなくなる（実測 2026-11-21）。 位置は余白（Margin）で与える。
        _overlay = overlayLayer;

        // **実寸が確定したら置き直す**: 置いた時点の見積りより実際の描画が高いことがあり、
        // その差だけ書式バーが注釈へ重なる（実測 2026-11-21）。 描画のたびに実寸で決め直す。
        if (!_frameHook)
        {
            _frameHook = true;
            owner.FrameRendered += () =>
            {
                if (_formatBarTarget is { } bar && _formatBarPanel.IsOpen)
                {
                    _formatBarPanel.RepositionOutside(bar, AnnotationPanelMetrics.FormatBarGap);
                }
            };
        }

        // 根は種別ごとに 1 度だけ作って使い回す（中身を作り直すと購読が増える）
        if (!_roots.TryGetValue(kind, out var root))
        {
            root = new Border
            {
                Child = kind == AnnotationPanelKind.FormatBar
                    ? _formatBar
                    : new AnnotationStylePanel(_layer, appearance),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            _roots[kind] = root;
        }

        // **中身は表示のたびに作り直す**: 吹き出しの設定は「吹き出しを選んでいるとき」だけ出す。
        // 一度作った中身を使い回すと、最初に枠線を右クリックした後は吹き出しの設定が出ない（実測 2026-11-21）。
        if (kind != AnnotationPanelKind.FormatBar)
        {
            root.Child = new AnnotationStylePanel(_layer, appearance);
        }

        root.Background = ToColor(appearance.Background);
        root.BorderBrush = ToColor(appearance.Border);
        root.BorderThickness = appearance.BorderThickness;
        root.CornerRadius = appearance.CornerRadius;
        root.Padding = new Thickness(
            appearance.Padding, appearance.VerticalPadding,
            appearance.Padding, appearance.VerticalPadding);

        // **大きさを中身に合わせて確定する**: MewUI の <c>OverlayLayer</c> は子を渡された領域いっぱいに
        // 配置するため、大きさを指定しないとパネルが縦に伸び、中身が上下の中央へ寄る（実測 2026-11-22:
        // 書式バーが注釈に重なった）。 中身を左上へ寄せ、根の大きさを実寸にする。
        if (root.Child is Control content)
        {
            content.HorizontalAlignment = HorizontalAlignment.Left;
            content.VerticalAlignment = VerticalAlignment.Top;
        }

        var size = MeasureContentSize(kind, appearance);
        root.Width = size.Width;
        // **高さはコアの固定値をそのまま使う**: 中身の希望寸法と実際の描画高さがずれると、
        // 置いた位置と実寸が食い違って書式バーが注釈に重なる（実測 2026-11-21）。
        root.Height = appearance.FixedContentHeight > 0
            ? appearance.ResolvePanelHeight(appearance.FixedContentHeight)
            : size.Height;

        // 余白（中身の無いところ）を押したまま動かすとパネルが動く。
        // MewUI のイベントは子から親へ伝播しないため、子（ボタン・入力欄）の押下はここへ来ない
        root.OnMouseDown(e => BeginPanelDrag(kind, root, owner, e));
        root.OnMouseMove(e => ContinuePanelDrag(kind, root, owner, e));
        root.OnMouseUp(_ => EndPanelDrag(kind));

        if (!overlayLayer.Contains(root))
        {
            overlayLayer.Add(root);
        }

        ApplyPosition(kind, root, position);
    }

    /// <summary>パネルを移動する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void MovePanel(AnnotationPanelKind kind, EsPoint position)
    {
        if (_roots.TryGetValue(kind, out var root))
        {
            ApplyPosition(kind, root, position);
        }
    }

    /// <summary>パネルを画面から外す（根と中身は残す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    public void DestroyPanel(AnnotationPanelKind kind) => RemovePanel(kind);

    /// <summary>パネルの位置を適用する（左上の余白として与える）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="position">左上の位置（DIP）。</param>
    private void ApplyPosition(AnnotationPanelKind kind, Border root, EsPoint position)
    {
        _positions[kind] = position;
        root.Margin = new Thickness(position.X, position.Y, 0, 0);
    }

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>位置と寿命はコアが決める</b>: 置ける領域（キャプチャ ページ）の内側へ収め、右クリックした位置へ出し、
    /// 外側をクリックすると閉じる。 余白を押したまま動かすと移動する。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
    {
        _stylePanel.Show(AnnotationPanelKind.StyleEditor, ToOverlayPoint(new Point(x, y)));
        _stylePanel.Constrain(MeasurePanel(AnnotationPanelKind.StyleEditor));
    }

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
        var body = ToOverlayRect(_layer.GetSelectedBoundsRect());
        _formatBarTarget = body;
        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar, body, MeasureFormatBar(), AnnotationPanelMetrics.FormatBarGap);
        _formatBarPanel.RepositionOutside(body, AnnotationPanelMetrics.FormatBarGap);
    }
    /// <summary>書式バーの大きさ（DIP）を測る。</summary>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private EsSize MeasureFormatBar()
    {
        var measured = MeasurePanel(AnnotationPanelKind.FormatBar);
        if (measured.Width > 0 && measured.Height > 0)
        {
            return measured;
        }

        var appearance = AnnotationPanelAppearance.FormatBar;
        _formatBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _formatBar.DesiredSize;
        return new EsSize(
            size.Width + (appearance.Padding * 2) + (appearance.BorderThickness * 2),
            size.Height + (appearance.VerticalPadding * 2) + (appearance.BorderThickness * 2));
    }

    /// <summary>パネルの大きさ（DIP）を測る。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>実寸。 まだ置いていない場合は希望寸法。</returns>
    private EsSize MeasurePanel(AnnotationPanelKind kind)
        => MeasureContentSize(kind, KindAppearance(kind));

    /// <summary>種別の外観を取得する（既定値。 パネルはどちらか 1 つしか持たない）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>外観。</returns>
    private static AnnotationPanelAppearance KindAppearance(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar
            ? AnnotationPanelAppearance.FormatBar
            : AnnotationPanelAppearance.StyleEditor;

    /// <summary>中身の実寸へ枠と余白を足したパネルの大きさを求める。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="appearance">外観（枠と余白）。</param>
    /// <returns>パネルの大きさ（DIP）。 中身が無い場合は原点。</returns>
    /// <remarks>
    /// <b>根ではなく中身を測る</b>: <c>OverlayLayer</c> の子は渡された領域へ引き伸ばされるため、
    /// 根の実寸は画面の大きさになる（実測 2026-11-22）。
    /// </remarks>
    private EsSize MeasureContentSize(AnnotationPanelKind kind, AnnotationPanelAppearance appearance)
    {
        if (!_roots.TryGetValue(kind, out var root) || root.Child is not { } content)
        {
            return default;
        }

        // レイアウト済みなら実寸（中身の RenderSize）を使う。 未レイアウトの中身は希望寸法が
        // 実際より小さく出ることがあり、その値で位置を決めると注釈に重なる（実測 2026-11-21）。
        var measured = content.RenderSize.Width > 0 && content.RenderSize.Height > 0
            ? new Size(content.RenderSize.Width, content.RenderSize.Height)
            : MeasureContent(content);

        return new EsSize(
            measured.Width + (appearance.Padding * 2) + (appearance.BorderThickness * 2),
            appearance.ResolvePanelHeight(measured.Height));
    }

    /// <summary>中身の希望寸法を測る。</summary>
    /// <param name="content">中身。</param>
    /// <returns>希望寸法。</returns>
    private static Size MeasureContent(Element content)
    {
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = content.DesiredSize;
        return new Size(desired.Width, desired.Height);
    }

    /// <summary>種別のパネルを画面から外す（根と中身は残す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    private void RemovePanel(AnnotationPanelKind kind)
    {
        if (_roots.TryGetValue(kind, out var root))
        {
            _overlay?.Remove(root);
        }
    }

    /// <summary>このコントロールの座標（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>重ねた先の座標。</returns>
    /// <remarks>
    /// <b>MewUI の <c>Bounds</c> はウィンドウの絶対座標</b>である（親からの相対ではない）。 したがって
    /// レイヤーの左上を足すだけでウィンドウ座標になる（<c>OverlayLayer</c> もウィンドウ座標で置く）。<br/>
    /// <b><c>TranslatePoint</c> は使わない</b>: ウィンドウやその内容の根を渡すと変換せず (0,0) を返すことがあり、
    /// パネルがアプリの左上へずれる（実測 2026-11-22: 書式バーがウィンドウの左上へ出た）。
    /// </remarks>
    private EsPoint ToOverlayPoint(Point point)
    {
        // **毎回ウィンドウ座標へ写す**: <c>Bounds</c> を足すだけだと、ツール バーの高さが変わった直後など
        // レイアウトが確定する前の値を使ったときにずれる（実測 2026-11-21: 約 28 DIP 下へずれた）。
        if ((FindVisualRoot() as Window)?.Content is { } root)
        {
            var local = _layer.TranslatePoint(point, root);
            if (!double.IsNaN(local.X) && !double.IsNaN(local.Y) && (local.X != 0 || local.Y != 0 || (point.X == 0 && point.Y == 0)))
            {
                return new EsPoint(local.X, local.Y);
            }
        }

        return new EsPoint(_layer.Bounds.X + point.X, _layer.Bounds.Y + point.Y);
    }

    /// <summary>このコントロールの矩形（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="rect">このコントロールの矩形。</param>
    /// <returns>重ねた先の矩形。</returns>
    private EsRect ToOverlayRect(EsRect rect)
    {
        var origin = ToOverlayPoint(new Point(rect.X, rect.Y));
        return new EsRect(origin.X, origin.Y, rect.Width, rect.Height);
    }

    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="owner">重ねた先のウィンドウ。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(
        AnnotationPanelKind kind, Border root, Window owner, MouseEventArgs e)
    {
        if (!e.LeftButton)
        {
            return;
        }

        _draggingRoot = root;
        var position = e.GetPosition(owner);
        Controller(kind).BeginDrag(new EsPoint(position.X, position.Y));
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="owner">重ねた先のウィンドウ。</param>
    /// <param name="e">ポインターの引数。</param>
    private void ContinuePanelDrag(
        AnnotationPanelKind kind, Border root, Window owner, MouseEventArgs e)
    {
        if (!ReferenceEquals(_draggingRoot, root))
        {
            return;
        }

        var position = e.GetPosition(owner);
        Controller(kind).DragTo(new EsPoint(position.X, position.Y));
    }

    /// <summary>パネルのドラッグを終える。</summary>
    /// <param name="kind">パネルの種別。</param>
    private void EndPanelDrag(AnnotationPanelKind kind)
    {
        _draggingRoot = null;
        Controller(kind).EndDrag();
    }

    /// <summary>種別ごとのコントローラーを取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>コントローラー。</returns>
    private AnnotationPanelController Controller(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar ? _formatBarPanel : _stylePanel;

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
    /// <param name="bounds">最終的な領域（ウィンドウの絶対座標）。</param>
    /// <remarks>
    /// <b>絶対座標のまま渡す</b>: MewUI の <c>Bounds</c> はウィンドウの絶対座標であり、
    /// 親は子へ絶対座標の矩形を渡して配置する。 0 起点で渡すとレイヤーの <c>Bounds</c> が (0,0) になり、
    /// 注釈の位置をウィンドウ座標へ写せない（実測 2026-11-22: 書式バーがアプリの左上へ出た）。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds) => _layer.Arrange(bounds);
}

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

    /// <summary>表示中のパネルの左上（DIP）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];

    /// <summary>パネルを重ねた先（アプリの画面の最上位）。</summary>
    private OverlayLayer? _overlay;

    /// <summary>パネルを載せているウィンドウ（座標変換とドラッグの基準）。</summary>
    private Window? _owner;

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
    /// <value>キャプチャ ページのうちプレビューが占める範囲（重ねた先の座標）。</value>
    /// <remarks>
    /// <b>アプリの画面の全体にはしない</b>: プレビューの外へ出すとキャプチャ ページから
    /// はみ出して見える（実測 2026-11-21）。 ページの内側に収めれば画像の内側で切れることもない。
    /// </remarks>
    public EsRect AvailableArea
    {
        get
        {
            var origin = ToOverlayPoint(default);
            return RenderSize.Width > 0 && RenderSize.Height > 0
                ? new EsRect(origin.X, origin.Y, RenderSize.Width, RenderSize.Height)
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
        _owner = owner;
        _overlay = overlayLayer;

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

        root.Background = ToColor(appearance.Background);
        root.BorderBrush = ToColor(appearance.Border);
        root.BorderThickness = appearance.BorderThickness;
        root.CornerRadius = appearance.CornerRadius;
        root.Padding = new Thickness(
            appearance.Padding, appearance.VerticalPadding,
            appearance.Padding, appearance.VerticalPadding);

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
        var body = ToOverlayRect(_layer.GetSelectedBodyRect());
        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar, body, MeasureFormatBar(), BarGap);
        _formatBarPanel.Constrain(MeasureFormatBar());
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
    {
        if (!_roots.TryGetValue(kind, out var root))
        {
            return default;
        }

        if (root.RenderSize.Width > 0 && root.RenderSize.Height > 0)
        {
            return new EsSize(root.RenderSize.Width, root.RenderSize.Height);
        }

        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = root.DesiredSize;
        return new EsSize(desired.Width, desired.Height);
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
    /// <returns>重ねた先の座標。 取得できない場合はそのまま返す。</returns>
    /// <remarks>重ねた先はウィンドウの最上位（<c>OverlayLayer</c>）のため、ウィンドウ座標へ写す。</remarks>
    private EsPoint ToOverlayPoint(Point point)
    {
        // 置ける領域はパネルを置く前に問い合わせられるため、ウィンドウは都度解決する
        if (_owner is null && FindVisualRoot() is Window owner)
        {
            _owner = owner;
        }

        if (_owner is not { } resolved)
        {
            return new EsPoint(point.X, point.Y);
        }

        var local = _layer.TranslatePoint(point, resolved);
        return new EsPoint(local.X, local.Y);
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
    /// <param name="bounds">最終的な領域。</param>
    protected override void ArrangeContent(Rect bounds)
        => _layer.Arrange(new Rect(0, 0, bounds.Width, bounds.Height));
}

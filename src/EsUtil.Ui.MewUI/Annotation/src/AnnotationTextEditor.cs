using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（MewUI 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: 本文の表示・キャレット・選択範囲は <see cref="AnnotationLayerControl"/> が描き、
/// 書式バーはその上に重ねる。 2 つを別々に置くと、位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>レイヤーは <see cref="Layer"/> から触る</b>: 画像・文書・選択・元に戻すなどは
/// <see cref="AnnotationLayerControl"/> の API を使う（本コントロールは配置だけを担う）。<br/>
/// <b>寸法</b>: レイヤーは与えられた領域いっぱいに置き、バーは本文の矩形の上へ置く（上に入りきらないときは内側へ寄せる）。
/// </remarks>
public sealed class AnnotationTextEditor : Panel
{
    /// <summary>書式バーと本文の間の余白（DIP）。</summary>
    private const double BarGap = 6;

    /// <summary>書式バー（編集中だけ表示する）。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>外観パネルを載せるウィンドウ（開いている間だけ保持する）。</summary>
    private Window? _styleWindow;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer) { IsVisible = false };

        Add(_layer);
        Add(_formatBar);

        // 編集の開始・終了でバーを出し入れする（本文の位置は編集のたびに変わりうるため配置もやり直す）
        _layer.EditingChanged += (_, _) =>
        {
            _formatBar.IsVisible = _layer.IsEditingText;
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
            }

            InvalidateMeasure();
            InvalidateArrange();
        };

        // 文字・書式の変更で表示を追随させる（バーの現在値が古くならないようにする）
        _layer.DocumentChanged += (_, _) =>
        {
            if (_formatBar.IsVisible)
            {
                _formatBar.Refresh();
            }
        };

        // 移動・サイズ変更で本文の位置が変わるため配置し直す
        _layer.SelectionChanged += (_, _) =>
        {
            if (_formatBar.IsVisible)
            {
                InvalidateArrange();
            }
        };

        // 選択範囲の変更で、書式バーの押下状態（太字・斜体・下線・取り消し線）を追随させる
        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_formatBar.IsVisible)
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

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>独立したウィンドウへ出す</b>: アプリのウィンドウの子として置くと、ウィンドウの縁で切れて項目が
    /// 選べなくなる（実測 2026-11-21。 MewUI の <c>Popup</c> もウィンドウの内側に描かれるため使えない）。<br/>
    /// <b>閉じるのは外側のクリックだけ</b>: ウィンドウが非アクティブになったら閉じる（ポインタが外れても閉じない）。
    /// 閉じるボタンは置かない。<br/>
    /// <b>パネルは移動できる</b>: 余白（見出し・枠）を押したまま動かすとウィンドウが動く。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
    {
        _ = x;
        _ = y;

        CloseStylePanel();

        if (FindVisualRoot() is not Window owner)
        {
            return;
        }

        var panel = new AnnotationStylePanel(_layer);
        var window = new Window
        {
            Borderless = true,
            ShowInTaskbar = false,
            Topmost = false,
            StartupLocation = WindowStartupLocation.Manual,
            Content = panel,
            WindowSize = WindowSize.FitContentSize(640, 960),
        };

        // 位置はポインタ（画面座標）。 アプリのウィンドウの内側へ収めない
        var scale = owner.DpiScale > 0 ? owner.DpiScale : 1.0;
        if (MewUiNativeCursor.TryGetScreenPosition(out var screenX, out var screenY))
        {
            window.StartManualPosition(screenX / scale, screenY / scale);
        }

        // 注釈の外を押したら閉じる（ウィンドウが非アクティブになる）
        window.Deactivated += () => CloseStylePanel();

        // パネルの余白・見出しの帯を押したまま動かすと、ウィンドウが動く。
        // MewUI はイベントを親へ伝播せず、当たり判定は最も深い「当たる」要素になる。
        // パネル自身が押されたときだけ動かすため、ボタン・コンボボックスの上では動かない。
        // **位置は OS のポインターで測る**: ウィンドウを動かすとパネル内の座標は動かないため、
        // パネル内の座標で差分を取ると 1 回で止まる（実測 2026-11-21）。
        Point? originWindow = null;
        var originCursor = default(System.Drawing.Point);
        panel.OnMouseDown(e =>
        {
            if (!e.LeftButton || !MewUiNativeCursor.TryGetScreenPosition(out var x, out var y))
            {
                return;
            }

            originWindow = window.Position;
            originCursor = new System.Drawing.Point(x, y);
        });
        panel.OnMouseMove(e =>
        {
            _ = e;

            if (originWindow is not { } start
                || !MewUiNativeCursor.TryGetScreenPosition(out var x, out var y))
            {
                return;
            }

            window.MoveTo(
                start.X + ((x - originCursor.X) / scale),
                start.Y + ((y - originCursor.Y) / scale));
        });
        panel.OnMouseUp(_ => originWindow = null);

        _styleWindow = window;
        window.Show(owner);
    }

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _styleWindow is not null;

    /// <summary>外観・書式の編集パネルを閉じる。</summary>
    public void CloseStylePanel()
    {
        if (_styleWindow is not { } window)
        {
            return;
        }

        _styleWindow = null;
        window.Close();
    }

    /// <summary>子要素の寸法を測る。</summary>
    /// <param name="availableSize">使える領域。</param>
    /// <returns>必要な寸法（レイヤーの希望寸法）。</returns>
    protected override Size MeasureContent(Size availableSize)
    {
        _layer.Measure(availableSize);

        if (_formatBar.IsVisible)
        {
            // バーは自分の幅を保つ（レイヤーの幅に合わせて縮めると項目が切れる）
            _formatBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        return _layer.DesiredSize;
    }

    /// <summary>子要素を配置する（レイヤーは全面、バーは本文の上）。</summary>
    /// <param name="bounds">最終的な領域。</param>
    protected override void ArrangeContent(Rect bounds)
    {
        _layer.Arrange(new Rect(0, 0, bounds.Width, bounds.Height));

        if (_formatBar.IsVisible)
        {
            var desired = _formatBar.DesiredSize;
            var body = _layer.GetSelectedBodyRect();
            var x = Math.Clamp(body.Width > 0 ? body.X : 0, 0, Math.Max(0, bounds.Width - desired.Width));

            // 本文の上へ置く。 上が足りなければ下へ置く（上へ寄せるとツールバーに重なる）
            var above = body.Y - desired.Height - BarGap;
            var y = above >= 0 ? above : body.Y + body.Height + BarGap;
            y = Math.Clamp(y, 0, Math.Max(0, bounds.Height - desired.Height));
            _formatBar.Arrange(new Rect(x, y, desired.Width, desired.Height));
        }
    }
}

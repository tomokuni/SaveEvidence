using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール。</summary>
/// <remarks>
/// <b>セットで使う</b>: 本文の表示・キャレット・選択範囲は <see cref="AnnotationLayerControl"/> が描き、
/// 書式バーはその上に重ねる。 2 つを別々に置くと、位置合わせと表示の切り替えをホストが書くことになる
/// （サンプルごとにずれる）。 本コントロールはその 2 つを内側で持ち、**編集の開始・終了に合わせてバーを出し入れ**する。<br/>
/// <b>レイヤーは <see cref="Layer"/> から触る</b>: 画像・文書・選択・元に戻すなどは従来どおり
/// <see cref="AnnotationLayerControl"/> の API を使う（本コントロールは配置だけを担う）。<br/>
/// <b>寸法</b>: レイヤーは与えられた領域いっぱいに置き、バーは本文の矩形の上へ置く（上に入りきらないときは内側へ寄せる）。
/// </remarks>
public sealed class AnnotationTextEditor : Panel
{
    /// <summary>書式バーと本文の間の余白（DIP）。</summary>
    private const double BarGap = 6;

    /// <summary>書式バー（編集中だけ表示する）。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>外観フライアウト（右クリック）の入れ物。 位置は Margin で決める。</summary>
    private Border? _styleHost;

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer) { IsVisible = false };

        // 下地を持たないとパネルが入力の対象にならない（Avalonia は背景が無い面をヒットテストしない）
        Background = Brushes.Transparent;

        // 書式バーは同じ画面の子として重ねる（別ウィンドウへ出すとキー入力がそちらへ移り、
        // 本文のキャレット移動・Shift+矢印の選択が効かなくなる。 実測 2026-11-20）
        _formatBar.ZIndex = 10;
        _formatBar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        _formatBar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

        Children.Add(_layer);
        Children.Add(_formatBar);

        // 右クリックの外観フライアウト（位置はレイヤーが知らせる）
        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);

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
    }

    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
    /// <value>画像・文書・選択・元に戻すなどを操作する対象。</value>
    public AnnotationLayerControl Layer => _layer;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    /// <value>編集中の場合は <see langword="true"/>（書式バーが出ている）。</value>
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>書式バーの代表色パレット（■ を並べる格子）の列数を取得または設定する。</summary>
    /// <value>1 以上の列数。 既定は <see cref="AnnotationPalette.DefaultPaletteColumns"/>（8 列）。</value>
    /// <remarks>
    /// <b>列数を増やすほど横に広がる</b>: 40 色なら 8 列 × 5 行に収まり、スクロールせずに選べる。
    /// </remarks>
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

    /// <summary>子要素の寸法を測る。</summary>
    /// <param name="availableSize">使える領域。</param>
    /// <returns>必要な寸法（レイヤーの希望寸法）。</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        _layer.Measure(availableSize);

        if (_formatBar.IsVisible)
        {
            // バーは自分の幅を保つ（レイヤーの幅に合わせて縮めると項目が切れる）
            _formatBar.Measure(Size.Infinity);
        }

        // 外観フライアウトも測る（測らないと配置されず表示されない）
        _styleHost?.Measure(Size.Infinity);

        return _layer.DesiredSize;
    }

    /// <summary>子要素を配置する（レイヤーは全面。 書式バーは本文の上、外観フライアウトは右クリック位置）。</summary>
    /// <param name="finalSize">最終的な領域。</param>
    /// <returns>配置した寸法。</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _layer.Arrange(new Rect(finalSize));

        if (_formatBar.IsVisible)
        {
            var desired = _formatBar.DesiredSize;
            var body = _layer.GetSelectedBodyRect();
            var above = body.Height > 0 ? body.Y - desired.Height - BarGap : 0;
            var y = above >= 0 ? above : body.Y + body.Height + BarGap;
            _formatBar.Arrange(new Rect(Math.Max(0, body.X), Math.Max(0, y), desired.Width, desired.Height));
        }

        // Margin と配置の両方でずらさない（Marquee は Margin を加味して配置される）
        _styleHost?.Arrange(new Rect(finalSize));

        return finalSize;
    }

    /// <summary>右クリック位置へ外観フライアウトを出す（既に出ていれば閉じる）。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>ウィンドウの内側へ収めない</b>: 収めると狭いウィンドウでパネルが潰れる。 はみ出してよい
    /// （パネルはドラッグで動かせる）。<br/>
    /// <b>パネルは移動できる</b>: 見出し（<c>外観</c> の行）をドラッグすると動く。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
    {
        CloseStylePanel();

        var content = AnnotationStyleFlyout.BuildContent(_layer);
        var host = new Border
        {
            Child = content,
            Background = (IBrush?)this.FindResource("SystemControlBackgroundAltHighBrush") ?? Brushes.White,
            BorderBrush = (IBrush?)this.FindResource("SystemControlForegroundBaseMediumLowBrush") ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(Math.Max(0, x), Math.Max(0, y), 0, 0),
            ZIndex = 20,
        };

        // 見出しのドラッグで移動する（本文の編集を妨げないよう、パネルの上だけで完結させる）
        Point? dragOrigin = null;
        Thickness originMargin = default;
        host.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
            {
                dragOrigin = e.GetPosition(this);
                originMargin = host.Margin;
                e.Pointer.Capture(host);
                e.Handled = true;
            }
        };
        host.PointerMoved += (_, e) =>
        {
            if (dragOrigin is not { } origin)
            {
                return;
            }

            var current = e.GetPosition(this);
            host.Margin = new Thickness(
                originMargin.Left + (current.X - origin.X),
                originMargin.Top + (current.Y - origin.Y),
                0,
                0);
            e.Handled = true;
        };
        host.PointerReleased += (_, e) =>
        {
            dragOrigin = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        };

        _styleHost = host;
        Children.Add(host);
        InvalidateArrange();
    }

    /// <summary>外観フライアウトを閉じる（出ていなければ何もしない）。</summary>
    public void CloseStylePanel()
    {
        if (_styleHost is not { } host)
        {
            return;
        }

        _styleHost = null;
        Children.Remove(host);
    }

    /// <summary>外観フライアウトが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _styleHost is not null;
}
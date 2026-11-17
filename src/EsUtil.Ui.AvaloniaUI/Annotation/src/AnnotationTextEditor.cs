using Avalonia;
using Avalonia.Controls;

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

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer) { IsVisible = false };

        Children.Add(_layer);
        Children.Add(_formatBar);

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
    /// <value>「■ + 全角 4 文字 + 100%」と「■ + <c>#FFFFFFFF</c>」の狭いほうの幅。</value>
    public double ColorWidth => _formatBar.ColorWidth;

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

        return _layer.DesiredSize;
    }

    /// <summary>子要素を配置する（レイヤーは全面、バーは本文の上）。</summary>
    /// <param name="finalSize">最終的な領域。</param>
    /// <returns>配置した寸法。</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _layer.Arrange(new Rect(finalSize));

        if (!_formatBar.IsVisible)
        {
            return finalSize;
        }

        var desired = _formatBar.DesiredSize;
        var body = _layer.GetSelectedBodyRect();
        var x = body.Width > 0 ? body.X : 0;
        var y = body.Height > 0 ? body.Y - desired.Height - BarGap : 0;

        // 上・左右にはみ出す場合は内側へ寄せる（バーが切れると操作できない）
        x = Math.Clamp(x, 0, Math.Max(0, finalSize.Width - desired.Width));
        y = Math.Clamp(y, 0, Math.Max(0, finalSize.Height - desired.Height));
        _formatBar.Arrange(new Rect(x, y, desired.Width, desired.Height));
        return finalSize;
    }
}

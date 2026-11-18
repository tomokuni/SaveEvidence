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

    /// <summary>外観・書式の編集パネル（右クリックで開く間だけ保持する）。</summary>
    private AnnotationStylePanel? _stylePanel;

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
        _layer.StyleEditorRequested += (_, _) => ShowStylePanel();
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

    /// <summary>外観・書式の編集パネルを表示する（右クリックと同じ）。</summary>
    public void ShowStylePanel()
    {
        CloseStylePanel();

        var panel = new AnnotationStylePanel(_layer, CloseStylePanel);
        _stylePanel = panel;
        Add(panel);
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>外観・書式の編集パネルを閉じる。</summary>
    public void CloseStylePanel()
    {
        if (_stylePanel is not { } panel)
        {
            return;
        }

        _stylePanel = null;
        Remove(panel);
        InvalidateArrange();
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

        _stylePanel?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return _layer.DesiredSize;
    }

    /// <summary>子要素を配置する（レイヤーは全面、バーは本文の上、編集パネルは右上）。</summary>
    /// <param name="bounds">最終的な領域。</param>
    protected override void ArrangeContent(Rect bounds)
    {
        _layer.Arrange(new Rect(0, 0, bounds.Width, bounds.Height));

        if (_formatBar.IsVisible)
        {
            var desired = _formatBar.DesiredSize;
            var body = _layer.GetSelectedBodyRect();
            var x = body.Width > 0 ? body.X : 0;
            var y = body.Height > 0 ? body.Y - desired.Height - BarGap : 0;

            // 上・左右にはみ出す場合は内側へ寄せる（バーが切れると操作できない）
            x = Math.Clamp(x, 0, Math.Max(0, bounds.Width - desired.Width));
            y = Math.Clamp(y, 0, Math.Max(0, bounds.Height - desired.Height));
            _formatBar.Arrange(new Rect(x, y, desired.Width, desired.Height));
        }

        if (_stylePanel is { } panel)
        {
            var desired = panel.DesiredSize;
            var x = Math.Max(0, bounds.Width - desired.Width - 12);
            panel.Arrange(new Rect(x, 12, desired.Width, desired.Height));
        }
    }
}

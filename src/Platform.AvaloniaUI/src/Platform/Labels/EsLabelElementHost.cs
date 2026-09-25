using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.Platform.AvaloniaUI.Labels;

/// <summary>コアが解決したラベル要素（背景 + テキスト）を親パネルの子要素として同期するホスト。</summary>
/// <remarks>
/// <b>適用パターン</b>: Host（コアの配置指示を自 UI の要素へ写す手順を単一所有する）。<br/>
/// <br/>
/// 「文字を UI 要素として配置する」コントロールが共通で必要とする処理を単一所有する:<br/>
/// - 要素の追加・再利用・削除（配置のたびに呼ばれる）<br/>
/// - 配置指示（<see cref="EsLabelElement"/>）の写像（背景色・角丸・内側余白・位置・大きさ・文字）<br/>
/// <br/>
/// <b>位置はコアが決める</b>: どこへ・どの大きさで置くかはコア（描画実装）が単一所有するため、本クラスは位置の計算を持たない。<br/>
/// 要素は親パネルの子の末尾へ置かれ、子は親より後に描かれるため<b>常に最上位</b>になる。<br/>
/// <br/>
/// <b>色はコアの色型で受ける</b>（コアは UI 非依存のため）ため、ブラシへの変換と容量制限付きキャッシュも本クラスが単一所有する<br/>
/// （コントロールごとに持つと、同じ色でも重複して保持され容量管理も二重になる）。<br/>
/// <br/>
/// <b>実寸測定は呼び出し側が行う</b>: 測定 API と「実寸を配置計算へ渡すかどうか」の方針はコントロールごとに異なるため、<br/>
/// 本クラスは測定関数を持たない（<c>Sync</c> へ渡す要素の解決時に呼び出し側が測定する）。
/// </remarks>
public sealed class EsLabelElementHost
{
    /// <summary>色に対応するブラシの容量制限付きキャッシュ（コントロール間で共有する）。</summary>
    private static readonly CapacityLimitedCache<Color, SolidColorBrush> s_brushes =
        new(static color => new SolidColorBrush(color));

    /// <summary>要素を子として持つ親パネル。</summary>
    private readonly Panel _owner;

    /// <summary>現在の要素（背景 + テキスト。親の子と 1 対 1 で対応する）。</summary>
    private readonly List<Border> _elements = [];

    /// <summary>要素を配置する親パネルを指定して初期化する。</summary>
    /// <param name="owner">要素を子として持つ親パネル。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> が null の場合。</exception>
    public EsLabelElementHost(Panel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>コア（描画実装）が解決した配置指示へ子要素を同期する。</summary>
    /// <param name="elements">配置するラベル要素（描画順に並ぶ）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="elements"/> が null の場合。</exception>
    /// <remarks>
    /// 余分な要素は親から外し、不足分は生成して子の末尾へ追加する（配置のたびに呼ばれる）。<br/>
    /// 各要素は左上寄せと <c>Margin</c> で絶対配置する（配置矩形はコアが単一所有する）。
    /// </remarks>
    public void Sync(IReadOnlyList<EsLabelElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        while (_elements.Count > elements.Count)
        {
            var last = _elements[^1];
            _elements.RemoveAt(_elements.Count - 1);
            _owner.Children.Remove(last);
        }

        while (_elements.Count < elements.Count)
        {
            var border = new Border { Child = new TextBlock { TextWrapping = TextWrapping.NoWrap } };
            _elements.Add(border);
            _owner.Children.Add(border);
        }

        for (var i = 0; i < elements.Count; i++)
        {
            Apply(_elements[i], elements[i]);
        }
    }

    /// <summary>1 つの要素の内容と配置をコアの指示に合わせる。</summary>
    /// <param name="border">更新する要素（背景）。</param>
    /// <param name="element">コアが解決した配置指示（寸法は実寸を反映済み）。</param>
    private static void Apply(Border border, in EsLabelElement element)
    {
        border.Background = s_brushes.GetOrCreate(element.BackgroundColor.ToAvaloniaColor());
        border.CornerRadius = new CornerRadius(element.CornerRadius);
        border.Padding = new Thickness(element.PaddingX, element.PaddingY);
        border.Width = element.Bounds.Width;
        border.Height = element.Bounds.Height;
        border.Margin = new Thickness(element.Bounds.X, element.Bounds.Y, 0, 0);
        border.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        border.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

        if (border.Child is TextBlock text)
        {
            text.Text = element.Text;
            text.FontSize = element.FontSize;
            text.Foreground = s_brushes.GetOrCreate(element.TextColor.ToAvaloniaColor());
        }
    }
}

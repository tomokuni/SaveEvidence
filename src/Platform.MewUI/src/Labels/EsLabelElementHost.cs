using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.Platform.MewUI.Labels;

/// <summary>コアが解決したラベル要素（背景 + テキスト）を親パネルの子要素として同期するホスト。</summary>
/// <remarks>
/// <b>適用パターン</b>: Host（コアの配置指示を自 UI の要素へ写す手順を単一所有する）。<br/>
/// <br/>
/// 「文字を UI 要素として配置する」コントロールが共通で必要とする処理を単一所有する:<br/>
/// - 要素の追加・再利用・削除（配置のたびに呼ばれる）<br/>
/// - 配置指示（<see cref="EsLabelElement"/>）の写像（背景色・角丸・内側余白・大きさ・文字）<br/>
/// - 配置矩形の確定（本 UI は <c>Margin</c> による絶対配置が反映されないため、配置フェーズで明示的に位置を決める）<br/>
/// <br/>
/// <b>位置はコアが決める</b>: どこへ・どの大きさで置くかはコア（描画実装）が単一所有するため、本クラスは位置の計算を持たない。<br/>
/// 要素は親パネルの子の末尾へ置かれ、子は親より後に描かれるため<b>常に最上位</b>になる。<br/>
/// <br/>
/// <b>色はコアの色型で受ける</b>（コアは UI 非依存のため）ため、色の変換も本クラスが単一所有する。<br/>
/// <b>実寸測定は呼び出し側が行う</b>（測定 API はコントロールが与える。<c>Measure</c> は子要素の測定を伝搬するだけである）。
/// </remarks>
public sealed class EsLabelElementHost
{
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

    /// <summary>子要素へ利用可能サイズを伝搬して測定する。</summary>
    /// <param name="availableSize">利用可能サイズ。</param>
    /// <remarks>本 UI は子を自動で測定しないため、コントロールの測定フェーズから呼ぶ。</remarks>
    public void Measure(Size availableSize)
    {
        foreach (var element in _elements)
        {
            element.Measure(availableSize);
        }
    }

    /// <summary>コア（描画実装）が解決した配置指示へ子要素を同期し、親の絶対座標を基準に配置する。</summary>
    /// <param name="elements">配置するラベル要素（描画順に並ぶ）。null 不可。</param>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="elements"/> が null の場合。</exception>
    /// <remarks>
    /// 余分な要素は親から外し、不足分は生成して子の末尾へ追加する（配置のたびに呼ばれる）。<br/>
    /// 子の座標はウィンドウ絶対座標のため、親の絶対座標（<paramref name="bounds"/> の左上）を加算する。
    /// </remarks>
    public void Sync(IReadOnlyList<EsLabelElement> elements, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(elements);

        while (_elements.Count > elements.Count)
        {
            var last = _elements[^1];
            _elements.RemoveAt(_elements.Count - 1);
            _owner.Remove(last);
        }

        while (_elements.Count < elements.Count)
        {
            var border = new Border().Child(new TextBlock());
            _elements.Add(border);
            _owner.Add(border);
        }

        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            var border = _elements[i];

            Apply(border, element);
            border.Arrange(new Rect(
                bounds.X + element.Bounds.X, bounds.Y + element.Bounds.Y, element.Bounds.Width, element.Bounds.Height));
        }
    }

    /// <summary>1 つの要素の内容と体裁をコアの指示に合わせる。</summary>
    /// <param name="border">更新する要素（背景）。</param>
    /// <param name="element">コアが解決した配置指示（寸法は実寸を反映済み）。</param>
    /// <remarks>本体は配置でサイズを確定するため、要素には内側余白と角丸だけを与える。</remarks>
    private static void Apply(Border border, in EsLabelElement element)
    {
        border.Padding(element.PaddingX, element.PaddingY);
        border.CornerRadius(element.CornerRadius);
        border.Background = element.BackgroundColor.ToMewUIColor();

        if (border.Child is TextBlock text)
        {
            text.Text = element.Text;
            text.FontSize = element.FontSize;
            text.Foreground = element.TextColor.ToMewUIColor();
        }
    }
}

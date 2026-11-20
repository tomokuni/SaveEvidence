using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）の外観。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じ見た目にする</b>: 以前は UI ごとに枠・余白・幅を持っていたため、版ごとに
/// 「余計な枠」「横幅が足りない」といった差が出た。 値はここが単一所有し、UI は与えられた値を
/// そのまま適用する（UI 側で枠・余白を足さない）。<br/>
/// <b>パネル自身が枠と余白を持つ</b>: ウィンドウ（トップレベル）には飾りを付けない。 ウィンドウの枠を
/// 残すと、パネルの枠と二重になり「辺枠が追加されている」ように見える。<br/>
/// <b>幅を固定する理由</b>: 入力欄の幅が中身（色名の長さ）で変わると、色を選ぶたびにパネルが伸び縮みする。
/// 幅を決めておけば、どの UI でも同じ位置に同じ幅で出る。
/// </remarks>
/// <param name="Background">パネルの下地の色（ウィンドウの下地にも同じ色を使う）。</param>
/// <param name="Border">パネルの枠の色。</param>
/// <param name="BorderThickness">パネルの枠の太さ（DIP）。 0 は枠を描かない。</param>
/// <param name="CornerRadius">パネルの角丸の半径（DIP）。 0 は角を丸めない。</param>
/// <param name="Padding">パネルの内側の余白（DIP）。</param>
/// <param name="ContentWidth">中身の幅（DIP）。 外観フライアウトの中身がこの幅に揃う。</param>
/// <param name="MaxWidth">パネルの幅の上限（DIP）。 書式バーのように中身で幅が決まるパネルに使う。</param>
/// <param name="MaxContentHeight">中身の高さの上限（DIP）。 超える場合は中身をスクロールさせる。</param>
public sealed record AnnotationPanelAppearance(
    DrawingColor Background,
    DrawingColor Border,
    double BorderThickness,
    double CornerRadius,
    double Padding,
    double ContentWidth,
    double MaxWidth,
    double MaxContentHeight)
{
    /// <summary>既定の外観を取得する。</summary>
    /// <value>下地 <c>#FFFFFF</c> / 枠 <c>#999999</c> / 枠 1 DIP / 角丸 4 DIP / 余白 10 DIP / 中身の幅 340 DIP / 幅の上限 640 DIP / 高さの上限 460 DIP。</value>
    /// <remarks>
    /// <b>AvaloniaUI 版の見た目に合わせる</b>: 4 つの UI 版の基準は AvaloniaUI 版である。
    /// 下地は白、枠は <c>#999999</c> の 1 DIP、内側の余白は 10 DIP（実測 2026-11-20 の画面から採寸）。
    /// </remarks>
    public static AnnotationPanelAppearance Default { get; } = new(
        Background: DrawingColor.FromArgb(255, 255, 255, 255),
        Border: DrawingColor.FromArgb(255, 153, 153, 153),
        BorderThickness: 1,
        CornerRadius: 4,
        Padding: 10,
        ContentWidth: 340,
        MaxWidth: 640,
        MaxContentHeight: 460);
}

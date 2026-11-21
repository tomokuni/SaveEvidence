using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）の外観。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じ見た目にする</b>: 以前は UI ごとに枠・余白・幅を持っていたため、版ごとに
/// 「余計な枠」「横幅が足りない」「ボタンが枠からはみ出す」といった差が出た。 値はここが単一所有し、
/// UI は与えられた値をそのまま適用する（UI 側で枠・余白を足さない）。<br/>
/// <b>パネル自身が枠と余白を持つ</b>: ウィンドウ（トップレベル）には飾りを付けない。 ウィンドウの枠を
/// 残すと、パネルの枠と二重になり「辺枠が追加されている」ように見える。<br/>
/// <b>書式バーと外観フライアウトで値を分ける</b>: 書式バーは中身（フォント名・大きさ・色）で幅が決まるため
/// 幅を固定せず、外観フライアウトは入力欄の幅を揃えるため固定する。
/// </remarks>
/// <param name="Background">パネルの下地の色（ウィンドウの下地にも同じ色を使う）。</param>
/// <param name="Border">パネルの枠の色。</param>
/// <param name="BorderThickness">パネルの枠の太さ（DIP）。 0 は枠を描かない。</param>
/// <param name="CornerRadius">パネルの角丸の半径（DIP）。 0 は角を丸めない。</param>
/// <param name="Padding">パネルの内側の余白（DIP）。</param>
/// <param name="VerticalPadding">パネルの内側の余白の上下（DIP）。</param>
/// <param name="ContentWidth">中身の幅（DIP）。 0 は中身に合わせる（書式バー）。</param>
/// <param name="MaxWidth">パネルの幅の上限（DIP）。</param>
/// <param name="MaxContentHeight">中身の高さの上限（DIP）。 超える場合は中身をスクロールさせる。</param>
/// <param name="MinContentHeight">中身の最小の高さ（DIP）。 希望寸法がこれより小さくてもこの高さを保つ。</param>
public sealed record AnnotationPanelAppearance(
    DrawingColor Background,
    DrawingColor Border,
    double BorderThickness,
    double CornerRadius,
    double Padding,
    double VerticalPadding,
    double ContentWidth,
    double MaxWidth,
    double MaxContentHeight,
    double MinContentHeight)
{
    /// <summary>右クリックの外観フライアウトの外観を取得する。</summary>
    /// <value>下地 <c>#FFFFFF</c> / 枠 <c>#999999</c> / 枠 1 DIP / 角丸 4 DIP / 余白 10 DIP / 中身の幅 340 DIP / 幅の上限 640 DIP / 高さの上限 460 DIP。</value>
    /// <remarks>
    /// <b>AvaloniaUI 版の見た目に合わせる</b>: 4 つの UI 版の基準は AvaloniaUI 版である。
    /// 下地は白、枠は <c>#999999</c> の 1 DIP、内側の余白は 10 DIP（実測 2026-11-20 の画面から採寸）。
    /// </remarks>
    public static AnnotationPanelAppearance StyleEditor { get; } = new(
        Background: DrawingColor.FromArgb(255, 255, 255, 255),
        Border: DrawingColor.FromArgb(255, 153, 153, 153),
        BorderThickness: 1,
        CornerRadius: 4,
        Padding: 10,
        VerticalPadding: 10,
        ContentWidth: 340,
        MaxWidth: 640,
        MaxContentHeight: 460,
        MinContentHeight: 0);

    /// <summary>書式バーの外観を取得する。</summary>
    /// <value>下地 <c>#FFFFFF</c> / 枠 <c>#999999</c> / 枠 1 DIP / 角丸 0 DIP / 余白 6,3 DIP / 幅は中身に合わせる / 幅の上限 2400 DIP / 高さの上限 200 DIP。</value>
    /// <remarks>
    /// <b>幅を固定しない</b>: 中身（フォント名・大きさ・色）で幅が決まる。 固定すると項目が枠から
    /// はみ出す（実測 2026-11-20）。<br/>
    /// <b>角を丸めない</b>: 高さが低いため、丸めると項目の角が欠けて見える。<br/>`r`n    /// <b>中身の最小の高さを 44 DIP にする</b>: UI ごとに中身の希望寸法が実際より小さく出るため、`r`n    /// これより低いと中身が枠からはみ出し、書式バーが注釈に重なる（実測 2026-11-21）。
    /// </remarks>
    public static AnnotationPanelAppearance FormatBar { get; } = new(
        Background: DrawingColor.FromArgb(255, 255, 255, 255),
        Border: DrawingColor.FromArgb(255, 153, 153, 153),
        BorderThickness: 1,
        CornerRadius: 0,
        Padding: 6,
        VerticalPadding: 3,
        ContentWidth: 0,
        MaxWidth: 2400,
        MaxContentHeight: 200,
        MinContentHeight: 26);
}

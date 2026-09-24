using EsUtil.Platform.Core.Geometry;
using Estable.Marquee;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.CoreForControl;

/// <summary>ラベル 1 件を UI 要素（テキスト要素）として配置するための指示。</summary>
/// <param name="Bounds">要素を置く矩形（DIP。コントロールの左上を原点とする）。</param>
/// <param name="TextOrigin">文字の描画原点（左上・DIP。要素の内側余白の算出に使う）。</param>
/// <param name="Text">表示する文言。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="TextColor">文字色。</param>
/// <param name="BackgroundColor">背景色（実効色へ解決済み。アルファ 0 は透明）。</param>
/// <param name="CornerRadius">背景の角丸半径（DIP）。0 は四角形。</param>
/// <remarks>
/// <b>位置付け</b>: コントロール化のための配置指示である。コアが決めた文言・外観・レイアウトを、<br/>
/// UI 層が「テキスト要素を 1 つ置く」だけで反映できる形へ写した値である。<br/>
/// 要素の内側余白は <see cref="PaddingX"/> / <see cref="PaddingY"/> が <see cref="TextOrigin"/> から求める<br/>
/// （コアの余白規則を要素の余白へ 1:1 で写すため、見た目がコアの計算と一致する）。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> であり、UI 境界で自 UI の色型へ変換する。
/// </remarks>
public readonly record struct MarqueeLabelElement(
    EsRect Bounds,
    EsPoint TextOrigin,
    string Text,
    double FontSize,
    DrawingColor TextColor,
    DrawingColor BackgroundColor,
    double CornerRadius)
{
    /// <summary>要素の左内側余白（DIP）を取得する。</summary>
    /// <value>背景矩形の左端から文字の描画原点までの距離。</value>
    public double PaddingX => TextOrigin.X - Bounds.X;

    /// <summary>要素の上内側余白（DIP）を取得する。</summary>
    /// <value>背景矩形の上端から文字の描画原点までの距離。</value>
    public double PaddingY => TextOrigin.Y - Bounds.Y;
}

/// <summary>コアの描画内容から、ラベル要素の配置指示を解決する（描画順の解釈の単一所有）。</summary>
/// <remarks>
/// <b>位置付け</b>: コントロール化のための変換である。コア（<see cref="Marquee.ResolveContent"/>）は<br/>
/// 「どのラベルを・どの文言で・どの順に描くか」を決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI 層から受け取って要素の矩形へ変換する。<br/>
/// <br/>
/// <b>なぜ CoreForControl 層にあるか</b>: 「実寸から要素の矩形を求めて並べる」ことは、文字を UI 要素として<br/>
/// 配置するコントロールでだけ必要になる手順である（単体で使う Marquee には要素という概念が無い）。<br/>
/// UI フレームワークには依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。<br/>
/// <br/>
/// <b>順序はコアが所有する</b>: 本クラスは <see cref="MarqueeDrawContent.Layers"/> の順序に従って並べるだけで、<br/>
/// 順序を判断しない（ラベルは必ず最後に来るため、要素も常に最上位に置かれる）。
/// </remarks>
public static class MarqueeLabelLayoutPlan
{
    /// <summary>ラベル要素の配置指示を解決する（描画順に並ぶ）。</summary>
    /// <param name="content">コアが解決した描画内容（文言・外観・層の順序を持つ）。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素の一覧（サイズラベル → カーソル座標ラベルの順）。どちらも無い場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. サイズラベルを持つ場合は実寸から配置を求め、要素を積む。<br/>
    /// 2. カーソル座標ラベルを持つ場合は実寸から配置を求め、要素を積む（サイズラベルより手前）。<br/>
    /// <br/>
    /// 外観（色・フォントサイズ・余白）と文言はコアが単一所有する（本クラスは実寸の反映のみ）。
    /// </remarks>
    public static IReadOnlyList<MarqueeLabelElement> Resolve(
        in MarqueeDrawContent content, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        if (content.SizeLabel is null && content.CoordLabel is null)
        {
            // 描画のたびに呼ばれるため、要素が無い場合は一覧を生成しない
            return [];
        }

        var elements = new List<MarqueeLabelElement>(2);

        if (content.SizeLabel is { } sizeLabel)
        {
            elements.Add(ToElement(sizeLabel.ComputeFrame(measureText(sizeLabel.Text, sizeLabel.FontSize)), sizeLabel.Text, sizeLabel.FontSize, sizeLabel.TextColor, sizeLabel.BackgroundColor));
        }

        if (content.CoordLabel is { } coordLabel)
        {
            elements.Add(ToElement(coordLabel.ComputeFrame(measureText(coordLabel.Text, coordLabel.FontSize)), coordLabel.Text, coordLabel.FontSize, coordLabel.TextColor, coordLabel.BackgroundColor));
        }

        return elements;
    }

    /// <summary>コアの配置（背景矩形・文字原点）を要素の配置指示へ写す。</summary>
    /// <param name="frame">コアが求めた配置。</param>
    /// <param name="text">表示する文言。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="backgroundColor">背景色。</param>
    /// <returns>要素の配置指示（角丸は 0 = 四角形）。</returns>
    private static MarqueeLabelElement ToElement(
        MarqueeLabelFrame frame, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor)
        => new(frame.Background, frame.TextOrigin, text, fontSize, textColor, backgroundColor, CornerRadius: 0);
}

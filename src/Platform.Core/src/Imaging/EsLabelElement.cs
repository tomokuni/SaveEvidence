using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>ラベル 1 件を UI 要素（テキスト要素）として配置するための指示。</summary>
/// <param name="Bounds">要素を置く矩形（DIP。呼び出し側の座標系に従う）。</param>
/// <param name="TextOrigin">文字の描画原点（左上・DIP。要素の内側余白の算出に使う）。</param>
/// <param name="Text">表示する文言。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="TextColor">文字色。</param>
/// <param name="BackgroundColor">背景色（アルファ 0 は透明）。</param>
/// <param name="CornerRadius">背景の角丸半径（DIP）。0 は四角形。</param>
/// <remarks>
/// <b>位置付け</b>: コントロール化のための配置指示である。コアが決めた文言・外観・レイアウトを、<br/>
/// UI 層が「テキスト要素を 1 つ置く」だけで反映できる形へ写した値である。<br/>
/// 要素の内側余白は <see cref="PaddingX"/> / <see cref="PaddingY"/> が <see cref="TextOrigin"/> から求める<br/>
/// （コアの余白規則を要素の余白へ 1:1 で写すため、見た目がコアの計算と一致する）。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> であり、UI 境界で自 UI の色型へ変換する。
/// </remarks>
public readonly record struct EsLabelElement(
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

    /// <summary>コアの配置と外観から要素の配置指示を組み立てる。</summary>
    /// <param name="frame">コアが求めた配置（背景矩形と文字の描画原点）。</param>
    /// <param name="text">表示する文言。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="backgroundColor">背景色。</param>
    /// <param name="cornerRadius">背景の角丸半径（DIP）。0（既定）は四角形。</param>
    /// <returns>要素の配置指示。</returns>
    /// <remarks>
    /// コアの配置（<see cref="EsLabelFrame"/>）を要素の配置指示へ写す写像を単一所有する<br/>
    /// （各コントロールの CoreForControl 層が同じ写像を書くと、写し漏れで見た目がずれる）。
    /// </remarks>
    public static EsLabelElement Create(
        EsLabelFrame frame, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor, double cornerRadius = 0)
        => new(frame.Background, frame.TextOrigin, text, fontSize, textColor, backgroundColor, cornerRadius);
}

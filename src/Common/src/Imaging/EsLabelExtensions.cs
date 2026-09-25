using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Common.Imaging;

/// <summary>ラベル契約（<see cref="IEsLabel"/>）から UI 要素の配置指示を求める拡張メソッド。</summary>
/// <remarks>
/// 「配置（<see cref="IEsLabel.ComputeFrame"/>）と外観の写像（<see cref="EsLabelElement.Create"/>）を合成する」<br/>
/// 手順を単一所有する。各ラベル型が同じ組み立てを書くと、写し漏れ（色・角丸・フォントサイズ）で見た目がずれる。<br/>
/// 返された <see cref="EsLabelElement"/> へ UI 層がテキスト要素を置くだけで反映できる。<br/>
/// <br/>
/// <b>色は引数で受け取る</b>: 色は「描画にだけ必要な設定」であり、描画の設定（UIBase の paint）が単一所有する。<br/>
/// 本契約が色を要求すると、コアのラベル型が色を持つことになる（コアは色を決定しない）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class EsLabelExtensions
{
    /// <summary>テキストの実寸から、UI 要素として配置するための指示を求める。</summary>
    /// <param name="label">対象のラベル。null 不可。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <param name="textColor">文字色（描画の設定が単一所有する）。</param>
    /// <param name="backgroundColor">背景色（描画の設定が単一所有する。アルファ 0 は透明）。</param>
    /// <returns>要素の配置指示（背景の角丸はラベルの指定に従う）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> が null の場合。</exception>
    public static EsLabelElement ToElement(
        this IEsLabel label, EsSize textSize, DrawingColor textColor, DrawingColor backgroundColor)
    {
        ArgumentNullException.ThrowIfNull(label);

        return EsLabelElement.Create(
            label.ComputeFrame(textSize),
            label.Text,
            label.FontSize,
            textColor,
            backgroundColor,
            label.CornerRadius);
    }
}

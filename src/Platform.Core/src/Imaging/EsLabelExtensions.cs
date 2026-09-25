using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>ラベル契約（<see cref="IEsLabel"/>）から UI 要素の配置指示を求める拡張メソッド。</summary>
/// <remarks>
/// 「配置（<see cref="IEsLabel.ComputeFrame"/>）と外観の写像（<see cref="EsLabelElement.Create"/>）を合成する」<br/>
/// 手順を単一所有する。各ラベル型が同じ組み立てを書くと、写し漏れ（色・角丸・フォントサイズ）で見た目がずれる。<br/>
/// 返された <see cref="EsLabelElement"/> へ UI 層がテキスト要素を置くだけで反映できる。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class EsLabelExtensions
{
    /// <summary>テキストの実寸から、UI 要素として配置するための指示を求める。</summary>
    /// <param name="label">対象のラベル。null 不可。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>要素の配置指示（背景の角丸はラベルの指定に従う）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> が null の場合。</exception>
    public static EsLabelElement ToElement(this IEsLabel label, EsSize textSize)
    {
        ArgumentNullException.ThrowIfNull(label);

        return EsLabelElement.Create(
            label.ComputeFrame(textSize),
            label.Text,
            label.FontSize,
            label.TextColor,
            label.BackgroundColor,
            label.CornerRadius);
    }
}

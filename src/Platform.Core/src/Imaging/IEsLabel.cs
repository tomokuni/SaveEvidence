using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>ラベル 1 件の描画契約（文言・外観と、実寸から配置を求める契約）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「背景付きの 1 行テキスト」という共通の形を表す契約である。<br/>
/// 文言と外観はコアが単一所有し、配置はテキストの実寸に依存するため、実寸測定（描画 API 依存）は UI 層が行う。<br/>
/// ラベルの用途ごとに型は異なるが（寸法表示・座標表示・付加情報）、<br/>
/// 「背景 = 実寸 + 内側余白」「文字は背景の内側余白だけ空ける」という体裁の規則は<br/>
/// <see cref="EsLabelLayout"/> が単一所有する（各型が別々に式を書くと余白が非対称になる）。<br/>
/// <br/>
/// <b>要素への写像</b>は <see cref="EsLabelExtensions.ToElement"/> が単一所有する<br/>
/// （各型が同じ組み立てを書くと、写し漏れで見た目がずれる）。<br/>
/// <b>単位</b>: DIP（描画領域と同じ座標系）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public interface IEsLabel
{
    /// <summary>表示する文言を取得する。</summary>
    /// <value>コアが生成した文言（ホストが上書きした場合はその文字列）。</value>
    string Text { get; }

    /// <summary>フォントサイズ（DIP）を取得する。</summary>
    /// <value>安全域へ補正済みのフォントサイズ。</value>
    double FontSize { get; }

    /// <summary>文字色を取得する。</summary>
    /// <value>文字の色（不透明度は色のアルファに統合）。</value>
    DrawingColor TextColor { get; }

    /// <summary>背景色を取得する。</summary>
    /// <value>背景の色（アルファ 0 は透明）。</value>
    DrawingColor BackgroundColor { get; }

    /// <summary>背景の角丸半径（DIP）を取得する。</summary>
    /// <value>角丸半径。0（既定）は四角形。</value>
    /// <remarks>角丸を持たないラベルは既定値（0）をそのまま使う（本メンバーを実装しない）。</remarks>
    double CornerRadius => 0;

    /// <summary>テキストの実寸からラベルの配置（背景矩形・文字の描画原点）を求める。</summary>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>ラベルの配置。<see cref="EsLabelLayout"/> の体裁を適用済み。</returns>
    EsLabelFrame ComputeFrame(EsSize textSize);
}

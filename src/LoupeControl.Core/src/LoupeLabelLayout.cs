using EsUtil.Platform.Core.Geometry;

namespace Estable.Loupe;

/// <summary>ルーペの中心座標ラベルの配置計算ロジック（純粋関数）。</summary>
/// <remarks>
/// ラベルの余白（ルーペ上端からの距離・背景の内側余白）と、背景矩形・文字原点の計算を単一所有する。<br/>
/// 各 UI 実装は本クラスの値を用いてテキスト要素を配置する（UI ごとに余白を持つと見た目がずれる）。<br/>
/// <b>単位は DIP</b>である（描画領域・ルーペの表示サイズと同じ座標系。物理ピクセルへの換算は描画時に行う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// <b>外部公開しない</b>: 利用者（UI 実装・ホスト）は <see cref="LoupeCoordLabel.ComputeFrame"/> だけを使うため、
/// 配置の内部手順は本アセンブリと単体テストに限定する。
/// </remarks>
internal static class LoupeLabelLayout
{
    /// <summary>ルーペ上端からのラベルの余白（DIP）。</summary>
    public const double OuterMargin = 2.0;

    /// <summary>ラベル背景の左右の内側余白（DIP）。</summary>
    public const double PaddingX = 4.0;

    /// <summary>ラベル背景の上下の内側余白（DIP）。</summary>
    public const double PaddingY = 1.0;

    /// <summary>ラベル背景の矩形を計算する（ルーペ上端中央・DIP 座標）。</summary>
    /// <param name="loupeSize">ルーペの一辺のサイズ（DIP）。</param>
    /// <param name="textWidth">ラベル文字列の実測幅（DIP）。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>ラベル背景の矩形（ルーペ左上を原点とする DIP 座標）。</returns>
    /// <remarks>背景は文字列の左右へ <see cref="PaddingX"/>、上下へ <see cref="PaddingY"/> を加えた大きさになる。</remarks>
    public static EsRect ComputeBackground(double loupeSize, double textWidth, double fontSize)
    {
        var origin = ComputeTextOrigin(loupeSize, textWidth);

        return new EsRect(
            origin.X - PaddingX,
            origin.Y,
            textWidth + PaddingX * 2,
            fontSize + PaddingY * 2);
    }

    /// <summary>ラベル文字列の左上（描画原点）を計算する（ルーペ上端中央・DIP 座標）。</summary>
    /// <param name="loupeSize">ルーペの一辺のサイズ（DIP）。</param>
    /// <param name="textWidth">ラベル文字列の実測幅（DIP）。</param>
    /// <returns>文字列の左上（ルーペ左上を原点とする DIP 座標）。</returns>
    /// <remarks>
    /// 水平位置はルーペの中央へ文字列をセンタリングした値、垂直位置はルーペ上端から <see cref="OuterMargin"/> となる。<br/>
    /// 文字のベースライン位置は描画 API 依存のため、各 UI が本戻り値から求める。
    /// </remarks>
    public static EsPoint ComputeTextOrigin(double loupeSize, double textWidth)
        => new((loupeSize - textWidth) / 2, OuterMargin);
}

using EsUtil.Platform.Core.Geometry;

namespace Estable.Loupe;

/// <summary>ルーペの中心座標ラベルの配置計算ロジック（純粋関数）。</summary>
/// <remarks>
/// ラベルの余白（ルーペ上端からの距離・背景の内側余白）と、背景矩形・文字原点の計算を単一所有する。<br/>
/// 各 UI 実装は本クラスの値を用いてラベルを配置する（UI ごとに余白を持つと見た目がずれる）。<br/>
/// 余白をレイアウト機構へ渡す UI 実装は戻り値を使わず、同じ値を Margin / Padding として渡す<br/>
/// （宣言的レイアウトと手計算で同じ矩形になることを、本クラスのテストが担保する）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class LoupeLabelLayout
{
    /// <summary>ルーペ上端からのラベルの余白（DIP）。</summary>
    public const double OuterMargin = 2.0;

    /// <summary>ラベル背景の左右の内側余白（DIP）。</summary>
    public const double PaddingX = 4.0;

    /// <summary>ラベル背景の上下の内側余白（DIP）。</summary>
    public const double PaddingY = 1.0;

    /// <summary>ラベル背景の矩形を計算する（ルーペ上端中央・物理ピクセル座標）。</summary>
    /// <param name="loupePixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="textWidth">ラベル文字列の実測幅（物理ピクセル）。</param>
    /// <param name="fontSize">フォントサイズ（物理ピクセル）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>ラベル背景の矩形（ルーペ左上を原点とする物理ピクセル座標）。</returns>
    /// <remarks>背景は文字列の左右へ <see cref="PaddingX"/>、上下へ <see cref="PaddingY"/> を加えた大きさになる。</remarks>
    public static EsRect ComputeBackground(double loupePixelSize, double textWidth, double fontSize, double scaling)
    {
        var origin = ComputeTextOrigin(loupePixelSize, textWidth, scaling);

        return new EsRect(
            origin.X - PaddingX * scaling,
            origin.Y,
            textWidth + PaddingX * 2 * scaling,
            fontSize + PaddingY * 2 * scaling);
    }

    /// <summary>ラベル文字列の左上（描画原点）を計算する（ルーペ上端中央・物理ピクセル座標）。</summary>
    /// <param name="loupePixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="textWidth">ラベル文字列の実測幅（物理ピクセル）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>文字列の左上（ルーペ左上を原点とする物理ピクセル座標）。</returns>
    /// <remarks>
    /// 水平位置はルーペの中央へ文字列をセンタリングした値、垂直位置はルーペ上端から <see cref="OuterMargin"/> となる。<br/>
    /// 文字のベースライン位置は描画 API 依存のため、各 UI が本戻り値から求める。
    /// </remarks>
    public static EsPoint ComputeTextOrigin(double loupePixelSize, double textWidth, double scaling)
        => new((loupePixelSize - textWidth) / 2, OuterMargin * scaling);
}

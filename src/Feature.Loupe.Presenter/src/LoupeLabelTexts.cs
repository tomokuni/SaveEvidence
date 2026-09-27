using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Loupe.Core.Drawing;

namespace EsUtil.Feature.Loupe.Presenter;

/// <summary>ルーペのラベルの文言と基準点を生成する（描画層が単一所有する）。</summary>
/// <remarks>
/// <b>文言の書式は描画層が単一所有する</b>: コアは幾何（拡大中心・表示サイズ）だけを返し、文言と基準点は描画の都合として本クラスが作る。<br/>
/// 基準点はルーペ上端の中央であり、アラインは水平中央・上端である<br/>
/// （水平位置は基準点の中央寄せで決まるため、ラベルの幅を知らずに指定できる）。<br/>
/// フォントサイズ・角丸・色は <see cref="LoupeAppearance"/> が単一所有する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeLabelTexts
{
    /// <summary>ルーペ上端から中心座標ラベルの基準点までの距離（DIP）。</summary>
    /// <remarks>ラベルを上端の帯へ収めるための距離である（ラベルの体裁（余白・角丸）は外観が単一所有する）。</remarks>
    private const double CoordLabelTopMargin = 2.0;

    /// <summary>中心座標ラベルの文言と基準点を解決する。</summary>
    /// <param name="geometry">描画の幾何（拡大中心・表示サイズを持つ）。</param>
    /// <param name="appearance">外観（表示可否を持つ）。</param>
    /// <returns>中心座標ラベルのテキスト配置。非表示の設定または中心未設定の場合は null。</returns>
    /// <remarks>
    /// 文言は <see cref="EsPoint.Format(int)"/> が文字列化する（座標の書式はコアの値型が単一所有する）。<br/>
    /// 表示可否の判断（設定値と中心の有無の組合せ）は <see cref="LoupeAppearance.ShouldShowCoordLabel"/> が単一所有する。
    /// </remarks>
    internal static LoupeTextPlacement? ResolveCoordLabelText(in LoupeGeometry geometry, LoupeAppearance appearance)
    {
        if (!appearance.ShouldShowCoordLabel(geometry.Center is not null) || geometry.Center is not { } center)
        {
            return null;
        }

        return new LoupeTextPlacement(
            center.Format(),
            new EsPoint(geometry.DisplaySize / 2, CoordLabelTopMargin),
            EsHorizontalAlign.Center,
            EsVerticalAlign.Top,
            default);
    }
}

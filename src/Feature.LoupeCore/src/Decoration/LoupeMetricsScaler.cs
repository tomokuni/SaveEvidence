using EsUtil.Platform.Core.Coordinate;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore.Decoration;

/// <summary>ルーペの寸法（DIP）を描画に使う物理ピクセルへ換算する計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 寸法値（市松模様のタイルサイズ・枠線の太さ）を、表示 DIP → 物理ピクセルのス ケーリング係数で換算する規則を単一所有する。<br/>
/// 換算結果は描画入力（<see cref="LoupeRenderRequest.CheckerSizePixels"/> / <see cref="LoupeRenderRequest.BorderThicknessPixels"/>）へ<br/>
/// 解決済みの値として含まれるため、描画実装は換算を持たない。<br/>
/// 安全域への補正は <see cref="LoupeOptions"/> が生成時に適用済みであり、本クラスは換算だけを行う<br/>
/// （整った値が来ることを型が保証するため、ここで再補正はしない）。<br/>
/// <b>中心座標ラベルは本クラスの対象外</b>: ラベルは DIP で配置する（<see cref="LoupeTextPlacement"/> が文言と基準点を DIP で保持し、
/// テキスト要素として配置される）ため、物理ピクセルへの換算を必要としない。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証 できる。
/// </remarks>
internal static class LoupeMetricsScaler
{
    /// <summary>市松模様の 1 タイルサイズを物理ピクセルへ換算する。</summary>
    /// <param name="checkerSize">タイルサイズ（DIP。安全域へ補正済み）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>タイルサイズ（物理ピクセル。切り上げ・最低 1）。</returns>
    /// <remarks>タイルサイズの安全域への補正は <see cref="LoupeOptions"/>（生成時）が単一所有する。</remarks>
    internal static int ScaleCheckerSize(int checkerSize, double scaling)
        => DpiScaler.ScaleToPixel(checkerSize, scaling);

    /// <summary>枠線の太さを物理ピクセルへ換算する。</summary>
    /// <param name="borderThickness">枠線の太さ（DIP。安全域へ補正済み）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>枠線の太さ（物理ピクセル。切り上げ・最低 1）。</returns>
    /// <remarks>枠線の太さの安全域への補正は <see cref="LoupeOptions"/>（生成時）が単一所有する。</remarks>
    internal static int ScaleBorderThickness(int borderThickness, double scaling)
        => DpiScaler.ScaleToPixel(borderThickness, scaling);
}

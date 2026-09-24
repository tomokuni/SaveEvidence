using EsUtil.Platform.Core.Coordinate;

namespace Estable.Loupe;

/// <summary>ルーペの外観値（DIP）を描画に使う物理ピクセルへ換算する計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 外観の寸法値（市松模様のタイルサイズ・外枠の太さ）を、表示 DIP → 物理ピクセルのスケーリング係数で換算する規則を単一所有する。<br/>
/// UI 層が各自で換算すると、同じ外観設定でも DPI によって見た目がずれる（片方の UI だけ換算する等）。<br/>
/// 安全域への補正は <see cref="LoupeAppearance"/> が生成時に適用済みであり、本クラスは換算だけを行う<br/>
/// （整った値が来ることを型が保証するため、ここで再補正はしない）。<br/>
/// <b>中心座標ラベルは本クラスの対象外</b>: ラベルは DIP で配置する（<see cref="LoupeCoordLabel"/> が DIP の値を保持し、
/// テキスト要素として配置される）ため、物理ピクセルへの換算を必要としない。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class LoupeAppearanceScaler
{
    /// <summary>市松模様の 1 タイルサイズを物理ピクセルへ換算する。</summary>
    /// <param name="appearance">装飾の外観（DIP。安全域へ補正済み）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>タイルサイズ（物理ピクセル。切り上げ・最低 1）。</returns>
    /// <remarks>タイルサイズの安全域への補正は <see cref="LoupeAppearance"/>（生成時）が単一所有する。</remarks>
    public static int ScaleCheckerSize(LoupeAppearance appearance, double scaling)
        => DpiScaler.ScaleToPixel(appearance.CheckerSize, scaling);

    /// <summary>外枠の太さを物理ピクセルへ換算する。</summary>
    /// <param name="appearance">装飾の外観（DIP。安全域へ補正済み）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>外枠の太さ（物理ピクセル。切り上げ・最低 1）。</returns>
    /// <remarks>外枠の太さの安全域への補正は <see cref="LoupeAppearance"/>（生成時）が単一所有する。</remarks>
    public static int ScaleFrameWidth(LoupeAppearance appearance, double scaling)
        => DpiScaler.ScaleToPixel(appearance.FrameWidth, scaling);
}

using EsUtil.Platform.Core.Coordinate;

namespace Estable.Loupe;

/// <summary>ルーペの外観値（DIP）を描画に使う物理ピクセルへ換算する計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 外観の寸法値（市松模様のタイルサイズ・外枠の太さ・座標ラベルのフォントサイズ）を、<br/>
/// 表示 DIP → 物理ピクセルのスケーリング係数で換算する規則を単一所有する。<br/>
/// UI 層が各自で換算すると、同じ外観設定でも DPI によって見た目がずれる（片方の UI だけ換算する等）。<br/>
/// 安全域への補正は <see cref="LoupeAppearance"/> が生成時に適用済みであり、本クラスは換算だけを行う<br/>
/// （整った値が来ることを型が保証するため、ここで再補正はしない）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeAppearanceScaler
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

    /// <summary>中心座標ラベルのフォントサイズを物理ピクセルへ換算する。</summary>
    /// <param name="appearance">装飾の外観（DIP。安全域へ補正済み）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>フォントサイズ（物理ピクセル）。</returns>
    /// <remarks>
    /// フォントサイズは描画 API へ実数で渡せるため切り上げない（文字の送り幅を実測値のまま使う）。<br/>
    /// 安全域への補正（非有限値を含む）は <see cref="LoupeAppearance"/>（生成時）が単一所有する。
    /// </remarks>
    public static double ScaleCoordLabelFontSize(LoupeAppearance appearance, double scaling)
        => appearance.CoordLabelFontSize * scaling;

    /// <summary>中心座標ラベルの背景の角丸半径を物理ピクセルへ換算する。</summary>
    /// <param name="appearance">装飾の外観（DIP。安全域へ補正済み）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>角丸半径（物理ピクセル）。補正後の値が 0 の場合は 0（角丸なし）。</returns>
    /// <remarks>
    /// 角丸なし（0）は「描かない」ではなく「四角形として描く」指定のため、他の寸法値と異なり最低 1 へ切り上げない<br/>
    /// （切り上げると角丸なしが指定できなくなる）。
    /// </remarks>
    public static int ScaleCoordLabelCornerRadius(LoupeAppearance appearance, double scaling)
        => appearance.CoordLabelCornerRadius <= 0 ? 0 : DpiScaler.ScaleToPixel(appearance.CoordLabelCornerRadius, scaling);
}

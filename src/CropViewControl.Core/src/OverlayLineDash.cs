namespace Estable.CropView;

/// <summary>オーバーレイ矩形の枠線スタイルに対応するダッシュ長を単一所有する。</summary>
/// <remarks>
/// 枠線は 2 つの経路で描画される（ベクター経路: UI の図形要素 / ラスタ経路: ルーペ画像のピクセル）。<br/>
/// 両経路が同じダッシュ長を使う必要があるため、本クラスへ集約する。<br/>
/// 長さの単位は経路ごとに解釈が異なる（ベクターは UI のダッシュ単位、ラスタは画像ピクセル）が、<br/>
/// 数値の所有者を 1 つにすることで見た目の不一致（片方だけ変更される事故）を防ぐ。
/// </remarks>
internal static class OverlayLineDash
{
    /// <summary>実線のダッシュ長（線を途切れさせない）。</summary>
    internal const double SolidLength = 0;

    /// <summary>破線（長いダッシュ）のダッシュ長。</summary>
    internal const double DashedLength = 8.0;

    /// <summary>破線（長いダッシュ）の間隔。</summary>
    internal const double DashedGap = 4.0;

    /// <summary>点線（短いダッシュ）のダッシュ長。</summary>
    internal const double DottedLength = 2.0;

    /// <summary>点線（短いダッシュ）の間隔。</summary>
    internal const double DottedGap = 2.0;

    /// <summary>枠線スタイルに対応するダッシュ長と間隔を取得する。</summary>
    /// <param name="style">枠線スタイル。</param>
    /// <returns>ダッシュ長と間隔（実線はどちらも <see cref="SolidLength"/>）。単位は呼び出し側の描画経路に従う。</returns>
    internal static (double DashLength, double GapLength) Get(OverlayLineStyle style) => style switch
    {
        OverlayLineStyle.Dashed => (DashedLength, DashedGap),
        OverlayLineStyle.Dotted => (DottedLength, DottedGap),
        _ => (SolidLength, SolidLength),
    };

    /// <summary>枠線スタイルが破線（ダッシュ）かどうかを判定する。</summary>
    /// <param name="style">枠線スタイル。</param>
    /// <returns>破線または点線の場合は true。実線の場合は false。</returns>
    internal static bool IsDashed(OverlayLineStyle style) => style != OverlayLineStyle.Solid;
}

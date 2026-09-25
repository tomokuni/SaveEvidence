namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の整数座標を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="EsPoint"/>（小数を扱う DIP 相当の座標）の<b>整数版</b>である。<br/>
/// 画面物理ピクセル座標（DPI スケーリング適用後）・画像ローカルピクセル座標・仮想スクリーンの座標など、<br/>
/// 整数でしか意味を持たない座標に使用する。<br/>
/// <b>整数であることは「画面物理ピクセル」を意味しない</b>（画像ピクセルも整数である）ため、<br/>
/// 単位が読む側に必要な箇所はドキュメント（param / value）に単位を明記する。<br/>
/// 小数を扱う <see cref="EsPoint"/> への変換は本型が単一所有する（UI が成分を書き写すと、同じ換算が各所に散る）。<br/>
/// 各 UI 実装の整数座標型との変換は Interop 層が提供する。
/// </remarks>
/// <param name="X">X 座標（整数）。</param>
/// <param name="Y">Y 座標（整数）。</param>
public readonly record struct EsPointInt(int X, int Y)
{
    /// <summary>小数を扱う座標（<see cref="EsPoint"/>）へ変換する。</summary>
    /// <returns>X / Y をそのまま引き継いだ座標。</returns>
    /// <remarks>整数座標をコアの座標型として扱う場合に使用する（値は変わらない）。</remarks>
    public EsPoint ToEsPoint() => new(X, Y);
}

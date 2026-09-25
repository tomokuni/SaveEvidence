using System.Globalization;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元座標を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークの座標型へ変換する（一方向依存）。<br/>
/// Avalonia の座標型との変換は <c>EsGeometryAvaloniaInterop</c> が提供する。<br/>
/// 座標ラベルの文言は <see cref="Format(int)"/> が単一所有する。<br/>
/// </remarks>
/// <param name="X">X 座標（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
/// <param name="Y">Y 座標（呼び出し側の座標系に従う。DIP またはドメイン座標）。</param>
public readonly record struct EsPoint(double X, double Y)
{
    /// <summary>表示に使える小数点以下の最大桁数（<see cref="double"/> の有効桁数）。</summary>
    private const int MaxDecimals = 15;

    /// <summary>座標を <c>(x, y)</c> 形式の表示用テキストへ整形する。</summary>
    /// <param name="decimals">小数点以下の桁数（0〜<see cref="MaxDecimals"/>。既定 0 = 整数に丸める）。</param>
    /// <returns><c>(x, y)</c> 形式の表示テキスト（桁数に合わせて丸める）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimals"/> が 0 未満または <see cref="MaxDecimals"/> より大きい場合。</exception>
    /// <remarks>
    /// 座標ラベル（カーソル座標・ルーペ中心座標）の文言を単一所有する（各コントロールが書くと書式がずれる）。<br/>
    /// <c>ToString()</c> を上書きしないのは、レコードの診断表現（デバッガー・ログ・テストの失敗表示）を<br/>
    /// 丸めた表示文字列へ置き換えないためである（幾何の調査では小数を読む必要がある）。
    /// </remarks>
    public string Format(int decimals = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, MaxDecimals);

        var format = $"F{decimals}";
        return $"({X.ToString(format, CultureInfo.CurrentCulture)}, {Y.ToString(format, CultureInfo.CurrentCulture)})";
    }
}

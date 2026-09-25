using System.Globalization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペのログへ出力する値の文字列化（純粋関数）。</summary>
/// <remarks>
/// 設定値の変更ログが出力する値の書式を単一所有する。<br/>
/// UI 実装が自前で整形すると同じ設定でも書式がずれ（片方だけ修正される）、ログの比較ができなくなる。<br/>
/// <b>可視性</b>: UI が使うのは未設定の表現（<see cref="Null"/>）と画像の寸法（<see cref="DescribeImage"/>）だけであり、<br/>
/// 座標・数値の整形はログ定義（<see cref="LoupeLogMessage"/>）の内部手順であるため internal とする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class LoupeLogFormat
{
    /// <summary>未設定を表す文字列。</summary>
    public const string Null = "null";

    /// <summary>拡大中心（ドメイン座標）を文字列化する。</summary>
    /// <param name="point">拡大中心。未設定は null。</param>
    /// <returns><c>(X, Y)</c> 形式の文字列。未設定は <c>null</c>。</returns>
    /// <remarks>
    /// 座標はドメイン座標（元画像ピクセル）のため、そのままの値を表示する（丸めない）。<br/>
    /// 数値の書式は <see cref="FormatNumber(double)"/> と同一（現在のカルチャに依存しない。ログを環境非依存にする）。
    /// </remarks>
    internal static string FormatPoint(EsPoint? point)
        => point is { } p ? $"({FormatNumber(p.X)}, {FormatNumber(p.Y)})" : Null;

    /// <summary>数値を文字列化する。</summary>
    /// <param name="value">変換する値。</param>
    /// <returns>現在のカルチャに依存しない文字列（ログを環境非依存にする）。</returns>
    internal static string FormatNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>数値を文字列化する。</summary>
    /// <param name="value">変換する値。</param>
    /// <returns>現在のカルチャに依存しない文字列（ログを環境非依存にする）。</returns>
    internal static string FormatNumber(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>拡大元画像を文字列化する。</summary>
    /// <param name="width">画像の幅（ピクセル）。未設定は null。</param>
    /// <param name="height">画像の高さ（ピクセル）。未設定は null。</param>
    /// <returns><c>WidthxHeight</c> 形式の文字列。未設定は <c>null</c>。</returns>
    /// <remarks>
    /// 画像の型は UI ごとに異なるため、<br/>
    /// 各 UI が寸法を取り出して本メソッドへ渡す（書式は本メソッドが単一所有する）。
    /// </remarks>
    public static string DescribeImage(int? width, int? height)
        => width is { } w && height is { } h ? $"{w}x{h}" : Null;
}

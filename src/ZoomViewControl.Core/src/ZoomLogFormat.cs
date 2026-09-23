using System.Globalization;

namespace Estable.ZoomView;

/// <summary>ズームのログが出力する値の文字列化を単一所有する静的クラス。</summary>
/// <remarks>
/// UI 層が書式（丸め・桁数）を自前で持つと両 UI でずれるため、本クラスが単一所有する。<br/>
/// ログのテンプレートは <see cref="ZoomLogMessage"/> が持ち、値の整形は本クラスが担う（関心の分離）。
/// </remarks>
public static class ZoomLogFormat
{
    /// <summary>ズーム率（%）をログ用の整数へ丸める。</summary>
    /// <param name="zoomPercent">ズーム率（%）。</param>
    /// <returns>四捨五入した整数（%）。</returns>
    /// <remarks>ログは人が読む値のため、小数を持つズーム率（Fit 実効倍率・ホイール倍率の結果）は整数へ丸める。</remarks>
    public static int Percent(double zoomPercent) => (int)Math.Round(zoomPercent);

    /// <summary>倍率をログ用の文字列へ整形する。</summary>
    /// <param name="scale">倍率（1.0 = 100%）。</param>
    /// <returns>不変カルチャの文字列（例: <c>1.25</c>）。</returns>
    /// <remarks>ログの数値は実行環境のカルチャに依存させない（小数点の表記ゆれを防ぐ）。</remarks>
    public static string Scale(double scale) => scale.ToString("0.###", CultureInfo.InvariantCulture);
}

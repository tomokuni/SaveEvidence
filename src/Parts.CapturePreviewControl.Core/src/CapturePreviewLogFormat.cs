using System.Globalization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>キャプチャプレビューのログに載せる値の文字列化を単一所有する。</summary>
/// <remarks>
/// 矩形の書式を両 UI で揃えるため（UI ごとに <c>PixelRect.ToString()</c> と手組みが混在していた）。<br/>
/// ズーム率の丸め・倍率の桁数も本クラスが単一所有する（UI 層が書式を持つと両 UI でずれる）。<br/>
/// カルチャに依存しない書式を使う（ログの数値は実行環境のカルチャに依存させない）。<br/>
/// ログのテンプレートは <see cref="CapturePreviewLogMessage"/> が持ち、値の整形は本クラスが担う（関心の分離）。
/// </remarks>
public static class CapturePreviewLogFormat
{
    /// <summary>矩形を <c>x,y,w,h</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形。</param>
    /// <returns><c>{X},{Y},{Width},{Height}</c> 形式の文字列。</returns>
    public static string Rect(EsRectInt rect) => $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";

    /// <summary>論理座標の矩形を <c>x,y,w,h</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形（論理座標）。</param>
    /// <returns><c>{X},{Y},{Width},{Height}</c> 形式の文字列。</returns>
    public static string Rect(EsRect rect) => $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";

    /// <summary>未検出の場合に空文字へ正規化した矩形の文字列を返す。</summary>
    /// <param name="rect">整形する矩形（未検出の場合は null）。</param>
    /// <returns>矩形がある場合は <c>x,y,w,h</c> 形式、null の場合は空文字。</returns>
    /// <remarks>「未検出は空文字」という検出結果の正規化を単一所有する（呼び出し側で <c>?? ""</c> を書かない）。</remarks>
    public static string RectOrEmpty(EsRectInt? rect) => rect is { } value ? Rect(value) : "";

    /// <summary>未検出・null の文字列を空文字へ正規化する。</summary>
    /// <param name="value">対象の文字列（null 許容）。</param>
    /// <returns>null の場合は空文字、それ以外は元の値。</returns>
    /// <remarks>検出結果のタイトル・クラス名のように「未取得は空文字」とする値を単一所有する。</remarks>
    public static string Text(string? value) => value ?? "";

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

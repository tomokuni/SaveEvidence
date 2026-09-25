using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>キャプチャセッションのログに載せる値の文字列化を単一所有する。</summary>
/// <remarks>
/// 矩形の書式を両 UI で揃えるため（UI ごとに <c>PixelRect.ToString()</c> と手組みが混在していた）。<br/>
/// カルチャに依存しない書式（整数の 10 進表記）を使う。
/// </remarks>
public static class CaptureLogFormat
{
    /// <summary>矩形を <c>x,y,w,h</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形。</param>
    /// <returns><c>{X},{Y},{Width},{Height}</c> 形式の文字列。</returns>
    public static string Rect(EsPixelRect rect) => $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";

    /// <summary>論理座標の矩形を <c>x,y,w,h</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形（論理座標）。</param>
    /// <returns><c>{X},{Y},{Width},{Height}</c> 形式の文字列。</returns>
    public static string Rect(EsRect rect) => $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";

    /// <summary>未検出の場合に空文字へ正規化した矩形の文字列を返す。</summary>
    /// <param name="rect">整形する矩形（未検出の場合は null）。</param>
    /// <returns>矩形がある場合は <c>x,y,w,h</c> 形式、null の場合は空文字。</returns>
    /// <remarks>「未検出は空文字」という検出結果の正規化を単一所有する（呼び出し側で <c>?? ""</c> を書かない）。</remarks>
    public static string RectOrEmpty(EsPixelRect? rect) => rect is { } value ? Rect((EsPixelRect)value) : "";

    /// <summary>未検出・null の文字列を空文字へ正規化する。</summary>
    /// <param name="value">対象の文字列（null 許容）。</param>
    /// <returns>null の場合は空文字、それ以外は元の値。</returns>
    /// <remarks>検出結果のタイトル・クラス名のように「未取得は空文字」とする値を単一所有する。</remarks>
    public static string Text(string? value) => value ?? "";
}

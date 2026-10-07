namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

/// <summary>検出結果などの文字列を正規化する。</summary>
/// <remarks>
/// 要素検出の結果（タイトル・クラス名）は「未取得は空文字」として扱う。<br/>
/// この正規化はログ用途ではない（表示名の状態保持にも使う）ため、ログ定義クラスとは別に本クラスが単一所有する。<br/>
/// UI 層は <c>?? ""</c> を書かず、本メソッドへ委譲する。
/// </remarks>
public static class TextNormalize
{
    /// <summary>未取得（null）の文字列を空文字へ正規化する。</summary>
    /// <param name="value">対象の文字列（null 許容）。</param>
    /// <returns>null の場合は空文字、それ以外は元の値。</returns>
    public static string Normalize(string? value) => value ?? "";
}

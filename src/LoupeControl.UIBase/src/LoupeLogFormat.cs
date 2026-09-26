namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの UI がログへ出力する値の文字列化（純粋関数）。</summary>
/// <remarks>
/// UI がログへ出す値の書式を単一所有する（UI 実装が自前で整形すると同じ値でも書式がずれ、片方だけ修正される）。<br/>
/// <b>公開である理由</b>: コントロール化の層の書式は UI 実装が使う契約である（設定値の変更ログの書式はコアが単一所有する）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class LoupeLogFormat
{
    /// <summary>未設定を表す文字列。</summary>
    public const string Null = "null";

    /// <summary>拡大元画像を文字列化する。</summary>
    /// <param name="width">画像の幅（ピクセル）。未設定は null。</param>
    /// <param name="height">画像の高さ（ピクセル）。未設定は null。</param>
    /// <returns><c>WidthxHeight</c> 形式の文字列。未設定は <c>null</c>。</returns>
    /// <remarks>
    /// 画像の型は UI ごとに異なるため、各 UI が寸法を取り出して本メソッドへ渡す（書式は本メソッドが単一所有する）。
    /// </remarks>
    public static string DescribeImage(int? width, int? height)
        => width is { } w && height is { } h ? $"{w}x{h}" : Null;
}

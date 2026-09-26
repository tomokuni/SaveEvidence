namespace EsUtil.Platform.Core.Geometry;

/// <summary>対象を基準点の垂直方向のどこへ合わせるかを表す（上端 / 中央 / 下端）。</summary>
/// <remarks>
/// <b>位置付け</b>: 高さが対象の実寸に依存する場面で、「基準点が対象のどこを指すか」を高さを知らずに指定するための値である。<br/>
/// 例: テキストの実寸から背景矩形を求める配置、画像を基準点へ合わせる配置、要素を親の領域へ寄せる配置。<br/>
/// <br/>
/// <b>なぜ列挙型か</b>: <see langword="bool"/> で指定すると呼び出し側で意味が読めない（<c>true</c> が上端か中央か分からない）ため、<br/>
/// 列挙型で指定する。<see cref="EsAxis"/>（軸の選択）と同じ方針である。<br/>
/// <b>本型は配置の規則を持たない</b>（基準点と実寸から位置を求める手順は利用側が単一所有する）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public enum EsVerticalAlign
{
    /// <summary>基準点を対象の上端に合わせる。</summary>
    Top,

    /// <summary>基準点を対象の垂直中央に合わせる。</summary>
    Middle,

    /// <summary>基準点を対象の下端に合わせる。</summary>
    Bottom,
}

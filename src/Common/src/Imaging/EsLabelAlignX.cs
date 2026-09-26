namespace EsUtil.SaveEvidence.Common.Imaging;

/// <summary>ラベル（テキスト）を基準点の水平方向のどこへ合わせるかを表す。</summary>
/// <remarks>
/// <b>位置付け</b>: コアは「テキストと基準点」だけを渡し、基準点がラベルのどこを指すかは本 enum が表す。<br/>
/// ラベルの幅はテキストの実寸に依存するため、背景矩形の算出（実寸の反映）は UI 層が行う。<br/>
/// 本 enum により、コアは「左上へ置く（<see cref="Left"/>）」「上端中央へ置く（<see cref="Center"/>）」を<br/>
/// 幅を知らずに指定できる。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public enum EsLabelAlignX
{
    /// <summary>基準点をラベルの左端に合わせる。</summary>
    Left,

    /// <summary>基準点をラベルの水平中央に合わせる。</summary>
    Center,

    /// <summary>基準点をラベルの右端に合わせる。</summary>
    Right,
}

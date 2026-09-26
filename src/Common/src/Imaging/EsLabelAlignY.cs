namespace EsUtil.SaveEvidence.Common.Imaging;

/// <summary>ラベル（テキスト）を基準点の垂直方向のどこへ合わせるかを表す。</summary>
/// <remarks>
/// <b>位置付け</b>: コアは「テキストと基準点」だけを渡し、基準点がラベルのどこを指すかは本 enum が表す。<br/>
/// ラベルの高さはテキストの実寸に依存するため、背景矩形の算出（実寸の反映）は UI 層が行う。<br/>
/// 本 enum により、コアは「上端へ置く（<see cref="Top"/>）」「上端中央へ置く（<see cref="Top"/> + <c>EsLabelAlignX.Center</c>）」を<br/>
/// 高さを知らずに指定できる。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public enum EsLabelAlignY
{
    /// <summary>基準点をラベルの上端に合わせる。</summary>
    Top,

    /// <summary>基準点をラベルの垂直中央に合わせる。</summary>
    Middle,

    /// <summary>基準点をラベルの下端に合わせる。</summary>
    Bottom,
}

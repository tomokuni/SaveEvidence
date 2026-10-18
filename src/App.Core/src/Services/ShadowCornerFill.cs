namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>画像の 4 隅へ落とす影の 1 つ分（位置・大きさ・放射グラデーションの中心）。</summary>
/// <param name="X">左端（DIP）。</param>
/// <param name="Y">上端（DIP）。</param>
/// <param name="Size">一辺（DIP。 常に <see cref="ShadowPlan.BlurRadius"/>）。</param>
/// <param name="CenterX">放射グラデーションの中心の X（0〜1。 隅の矩形に対する相対位置）。</param>
/// <param name="CenterY">放射グラデーションの中心の Y（0〜1。 隅の矩形に対する相対位置）。</param>
/// <param name="Stops">停止点（中心からの距離の順。 位置は距離 ÷ <see cref="ShadowPlan.BlurRadius"/>）。</param>
/// <remarks>
/// <b>中心が隅の頂点になる理由</b>: 矩形の外側の点の「矩形までの距離」は、隅の付近では<b>隅の頂点からの距離</b>と<br/>
/// 一致する。 したがって隅は「頂点を中心にした放射グラデーション」で、辺と同じ濃さの変化になる。<br/>
/// この型は値だけを持ち、描画 API には依存しない（4 つの UI 版が自分の描画 API へ写す）。
/// </remarks>
public readonly record struct ShadowCornerFill(
    double X,
    double Y,
    double Size,
    double CenterX,
    double CenterY,
    IReadOnlyList<ShadowStop> Stops);

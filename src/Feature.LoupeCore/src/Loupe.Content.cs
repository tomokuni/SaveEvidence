using EsUtil.Platform.Core.Geometry;

using EsUtil.Feature.LoupeCore.Decoration;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの 1 フレーム分の描画内容（図形の層）の解決を担う部分。</summary>
/// <remarks>
/// <b>描画に必要な値（矩形・タイル・層の順序）はすべて本部分が解決する</b>ため、<br/>
/// 描画実装は計算を持たず、返された <see cref="LoupeDrawContent.Layers"/> の順に描画ポートへ写すだけでよい。<br/>
/// <br/>
/// <b>描画順はコアが単一所有する</b>: 本部分が層の一覧を組み立てるため、UI 層は層の順序・有無を判断しない<br/>
/// （中心座標ラベルは必ず最後 = 常に最上位。UI の子要素は親の描画より後に描かれる性質とも一致する）。<br/>
/// <br/>
/// <b>可視性</b>: <see cref="ResolveContent"/> はファサードの公開メソッドであり、<br/>
/// 内部の計算（タイルの列挙子・矩形の導出・寸法の換算）は本アセンブリに限定する。
/// </remarks>
public sealed partial class Loupe
{
    /// <summary>描く層とその順序（中心座標ラベルは必ず最後 = 常に最上位）。</summary>
    /// <remarks>
    /// 図形の層の順序は固定であり、層の有無で変わらないため 1 つの一覧を共有する（描画のたびの確保を避ける）。<br/>
    /// 中心座標ラベルは UI のテキスト要素が描くため、描画実装は当該の層を描かない<br/>
    /// （一覧に含めるのは「ラベルが最上位である」という契約の表現である）。
    /// </remarks>
    private static readonly IReadOnlyList<LoupeDrawLayer> s_layers =
    [
        LoupeDrawLayer.Background,
        LoupeDrawLayer.Checkerboard,
        LoupeDrawLayer.Image,
        LoupeDrawLayer.Crosshair,
        LoupeDrawLayer.Frame,
        LoupeDrawLayer.CoordLabel,
    ];

    /// <summary>描画入力から 1 フレーム分の描画内容（図形の層）を解決する。</summary>
    /// <param name="request">描画入力（描画のたびに決まる値。物理ピクセルサイズ・寸法の換算結果を含む）。</param>
    /// <returns>描画内容（描画面・タイル列挙子・矩形・層の順序）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 描画面の全体を物理ピクセルサイズから決める。<br/>
    /// 2. 市松模様の暗色タイルを列挙する構造体を換算済みのタイルサイズから作る。<br/>
    /// 3. 十字線と外枠の矩形を、物理ピクセルサイズと換算済みの太さから導出する。<br/>
    /// 4. 層の一覧（固定順）を添えて描画内容を返す。<br/>
    /// <br/>
    /// 色はコアが持たないため本メソッドは扱わない（描画時のアルファ判定は描画実装の責務であり、<br/>
    /// アルファ 0 の色は描かない）。<br/>
    /// 拡大画像の層は本値に含めない（切り出し元の型が UI に依存するため、描画の起動時に引数で受け取る）。
    /// </remarks>
    public LoupeDrawContent ResolveContent(in LoupeRenderRequest request)
    {
        var surface = request.SurfaceBounds;

        return new LoupeDrawContent(
            surface,
            new LoupeCheckerTiles(request.PixelSize, request.CheckerSizePixels),
            LoupeDecorationGeometry.ComputeCrosshairRects(request.PixelSize, request.ZoomLevel),
            LoupeDecorationGeometry.ComputeFrameRects(request.PixelSize, request.FrameWidthPixels),
            s_layers);
    }
}

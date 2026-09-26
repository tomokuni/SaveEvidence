using EsUtil.Platform.Core.Geometry;

using EsUtil.Feature.LoupeCore.Decoration;
using EsUtil.Feature.LoupeCore.Drawing;

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
    /// <summary>描く層とその順序（テキストなし）。</summary>
    /// <remarks>
    /// 図形の層の順序は固定であり、テキストの有無で末尾の層だけが変わる。<br/>
    /// 描画のたびに一覧を組み立てないよう、2 通りを事前生成して共有する
    /// （<see cref="LoupeDrawContent.Layers"/> は読取専用の参照として渡す）。
    /// </remarks>
    private static readonly IReadOnlyList<LoupeDrawLayer> s_layersWithoutLabel =
    [
        LoupeDrawLayer.Background,
        LoupeDrawLayer.Checkerboard,
        LoupeDrawLayer.Image,
        LoupeDrawLayer.Crosshair,
        LoupeDrawLayer.Frame,
    ];

    /// <summary>描く層とその順序（テキストあり。中心座標ラベルは常に最上位）。</summary>
    /// <remarks>
    /// テキストは UI 要素としても置ける（要素は親より後に描かれるため常に最上位になる）。<br/>
    /// その場合、描画実装はテキストの層を無視してよい（基底のフックの既定は何もしない）。
    /// </remarks>
    private static readonly IReadOnlyList<LoupeDrawLayer> s_layersWithLabel =
    [
        LoupeDrawLayer.Background,
        LoupeDrawLayer.Checkerboard,
        LoupeDrawLayer.Image,
        LoupeDrawLayer.Crosshair,
        LoupeDrawLayer.Frame,
        LoupeDrawLayer.Label,
    ];

    /// <summary>ルーペ上端から中心座標ラベルの基準点までの距離（DIP）。</summary>
    /// <remarks>ラベルを上端の帯へ収めるための距離である（ラベルの体裁（余白・角丸）は描画の設定が単一所有する）。</remarks>
    private const double CoordLabelTopMargin = 2.0;

    /// <summary>描画入力から 1 フレーム分の描画内容を解決する。</summary>
    /// <param name="request">描画入力（描画のたびに決まる値。物理ピクセルサイズ・寸法の換算結果を含む）。</param>
    /// <returns>描画内容（描画面・タイル列挙子・矩形・テキスト配置・層の順序）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 描画面の全体を物理ピクセルサイズから決める。<br/>
    /// 2. 市松模様の暗色タイルを列挙する構造体を換算済みのタイルサイズから作る。<br/>
    /// 3. 十字線と外枠の矩形を、物理ピクセルサイズと換算済みの太さから導出する。<br/>
    /// 4. テキスト（中心座標ラベルの文言と基準点）を解決する。<br/>
    /// 5. 描く層の一覧（テキストの有無で末尾が変わる）を添えて描画内容を返す。<br/>
    /// <br/>
    /// 色はコアが持たないため本メソッドは扱わない（描画時のアルファ判定は描画実装の責務であり、<br/>
    /// アルファ 0 の色は描かない）。<br/>
    /// 拡大画像の層は本値に含めない（切り出し元の型が UI に依存するため、描画の起動時に引数で受け取る）。
    /// </remarks>
    public LoupeDrawContent ResolveContent(in LoupeRenderRequest request)
    {
        var surface = request.SurfaceBounds;
        var coordLabelText = ResolveCoordLabelText(request);

        return new LoupeDrawContent(
            surface,
            new LoupeCheckerTiles(request.PixelSize, request.CheckerSizePixels),
            LoupeDecorationGeometry.ComputeCrosshairRects(request.PixelSize, request.ZoomLevel),
            LoupeDecorationGeometry.ComputeFrameRects(request.PixelSize, request.FrameWidthPixels),
            coordLabelText,
            coordLabelText is null ? s_layersWithoutLabel : s_layersWithLabel);
    }

    /// <summary>中心座標ラベルの文言と基準点を解決する。</summary>
    /// <param name="request">描画入力（拡大中心・表示サイズと設定を含む）。</param>
    /// <returns>中心座標ラベルのテキスト配置。非表示の設定または中心未設定の場合は null。</returns>
    /// <remarks>
    /// 文言はコア（<see cref="EsPoint.Format(int)"/>）が文字列化する。<br/>
    /// 基準点はルーペ上端の中央であり、アラインは水平中央・上端である<br/>
    /// （水平位置は基準点の中央寄せで決まるため、コアはラベルの幅を知らずに指定できる）。<br/>
    /// フォントサイズ・角丸・色は描画の設定（<c>LoupePaint</c>）が単一所有する。
    /// </remarks>
    private static LoupeTextPlacement? ResolveCoordLabelText(in LoupeRenderRequest request)
    {
        if (!request.Metrics.ShouldShowCoordLabel(request.Center is not null) || request.Center is not { } center)
        {
            return null;
        }

        return new LoupeTextPlacement(
            center.Format(),
            new EsPoint(request.DisplaySize / 2, CoordLabelTopMargin),
            EsHorizontalAlign.Center,
            EsVerticalAlign.Top,
            default);
    }
}

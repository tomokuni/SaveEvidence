using EsUtil.Platform.Core.Geometry;

using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの 1 フレーム分の描画内容（図形の層）の解決を担う部分。</summary>
/// <remarks>
/// <b>描画に必要な値（矩形・タイル・層の順序）はすべてコアが解決する</b>ため、<br/>
/// 描画実装は計算を持たず、返された <see cref="LoupeDrawContent.Layers"/> の順に描画ポートへ写すだけでよい。<br/>
/// <br/>
/// <b>描画順はコアが単一所有する</b>: 層の一覧を本部分が組み立てるため、UI 層は層の順序・有無を判断しない<br/>
/// （中心座標ラベルは必ず最後 = 常に最上位。UI の子要素は親の描画より後に描かれる性質とも一致する）。<br/>
/// 層ごとの有無は描画内容（設定の明示フラグと解決済みの値）から決まり、<br/>
/// 有無の全組合せは事前生成した表から選ぶ（描画のたびに一覧を組み立てない）。<br/>
/// <br/>
/// <b>可視性</b>: <see cref="ResolveContent"/> はファサードの公開メソッドであり、<br/>
/// 内部の計算（タイルの列挙子・矩形の導出・寸法の換算）は本アセンブリに限定する。
/// </remarks>
public sealed partial class Loupe
{
    /// <summary>層の組合せのうち、市松模様の有無を表すビット。</summary>
    private const int CheckerboardBit = 1;

    /// <summary>層の組合せのうち、拡大画像の有無を表すビット。</summary>
    private const int ImageBit = 2;

    /// <summary>層の組合せのうち、十字線の有無を表すビット。</summary>
    private const int CrosshairBit = 4;

    /// <summary>層の組合せのうち、外枠の有無を表すビット。</summary>
    private const int FrameBit = 8;

    /// <summary>層の組合せのうち、テキストの有無を表すビット。</summary>
    private const int TextBit = 16;

    /// <summary>層の並び（下地を除く 5 つの有無 = 32 通り）。</summary>
    /// <remarks>
    /// 描画のたびに一覧を組み立てないよう、全通りを事前生成して共有する<br/>
    /// （<see cref="LoupeDrawContent.Layers"/> は読取専用の参照として渡す）。層を追加した場合は本表も更新する。
    /// </remarks>
    private static readonly IReadOnlyList<LoupeDrawLayer>[] s_layers = CreateLayers();

    /// <summary>描く層だけを描画順に並べる。</summary>
    /// <param name="hasCheckerboard">市松模様を描くかどうか（設定の明示フラグ）。</param>
    /// <param name="hasImage">拡大画像を描くかどうか（切り出し領域が解決できたか）。</param>
    /// <param name="hasCrosshair">十字線を描くかどうか（設定の明示フラグ）。</param>
    /// <param name="hasFrame">外枠を描くかどうか（設定の明示フラグ）。</param>
    /// <param name="hasText">テキスト（中心座標ラベル）を描くかどうか。</param>
    /// <returns>描く層の一覧（先頭から順に処理する）。</returns>
    /// <remarks>
    /// 下地は常に含める（描画面を置き換えて前フレームの残像を残さないため。透明色を指定した場合も描画面の消去が必要）。<br/>
    /// テキストは末尾へ置き、常に最上位にする（UI 要素として置く UI は本層を無視してよい）。<br/>
    /// 一覧はビットの組合せで事前生成したものを返す（描画のたびの確保を避ける）。
    /// </remarks>
    private static IReadOnlyList<LoupeDrawLayer> ResolveLayers(
        bool hasCheckerboard, bool hasImage, bool hasCrosshair, bool hasFrame, bool hasText)
        => s_layers[
            (hasCheckerboard ? CheckerboardBit : 0)
            | (hasImage ? ImageBit : 0)
            | (hasCrosshair ? CrosshairBit : 0)
            | (hasFrame ? FrameBit : 0)
            | (hasText ? TextBit : 0)];

    /// <summary>層の全 32 通りを生成する。</summary>
    /// <returns>ビットの組合せを添字とする層の一覧。</returns>
    /// <remarks>ビットの意味は <see cref="CheckerboardBit"/> / <see cref="ImageBit"/> / <see cref="CrosshairBit"/> / <see cref="FrameBit"/> / <see cref="TextBit"/> が単一所有する。</remarks>
    private static IReadOnlyList<LoupeDrawLayer>[] CreateLayers()
    {
        var table = new IReadOnlyList<LoupeDrawLayer>[1 << 5];

        for (var index = 0; index < table.Length; index++)
        {
            var layers = new List<LoupeDrawLayer>(6) { LoupeDrawLayer.Background };

            if ((index & CheckerboardBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Checkerboard);
            }

            if ((index & ImageBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Image);
            }

            if ((index & CrosshairBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Crosshair);
            }

            if ((index & FrameBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Frame);
            }

            // テキストは常に最上位（下地・市松模様・拡大画像・十字線・外枠より後に描く）
            if ((index & TextBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Label);
            }

            table[index] = layers;
        }

        return table;
    }

    /// <summary>ルーペ上端から中心座標ラベルの基準点までの距離（DIP）。</summary>
    /// <remarks>ラベルを上端の帯へ収めるための距離である（ラベルの体裁（余白・角丸）は描画の設定が単一所有する）。</remarks>
    private const double CoordLabelTopMargin = 2.0;

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
        if (!request.Options.ShouldShowCoordLabel(request.Center is not null) || request.Center is not { } center)
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

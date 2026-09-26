using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="SurfaceBounds">描画面の全体（左上を原点とし、幅・高さが物理ピクセルサイズの正方形）。</param>
/// <param name="CheckerTiles">市松模様の暗色タイルの矩形を列挙する構造体（タイルなしの場合は空）。</param>
/// <param name="CrosshairRects">十字線の矩形（縦 1 本・横 1 本。拡大率 1 の場合は空）。</param>
/// <param name="FrameRects">外枠の矩形（上・下・左・右の 4 辺。太さ 0 の場合は空）。</param>
/// <param name="Layers">描く層とその順序（<b>描画順はコアが単一所有する</b>）。先頭から順に処理する。</param>
/// <remarks>
/// <b>描画に必要な値はすべて本値に解決済み</b>である（描画実装は計算を持たず、<paramref name="Layers"/> の順に写すだけ）。<br/>
/// したがって描画実装が幾何計算（タイルの位置・矩形の導出・寸法の換算）を行うことはない。<br/>
/// <b>色と「描くかどうか」の最終判断は描画側に残る</b>: アルファ 0 の色は描かない（描画にだけ必要な設定であるため）。<br/>
/// <b>ラベルは UI 要素が描く</b>: 中心座標ラベルは <see cref="Loupe.ResolveLabels"/> が配置指示を返し、本値の層には含めない<br/>
/// （層の一覧に含めるのは「ラベルが最上位である」という契約の表現である）。<br/>
/// <b>画像の層</b>: 拡大元の型は UI に依存するため本値に含めず、描画の起動時に引数で受け取る<br/>
/// （切り出し元・描画先の矩形はコアの <c>LoupeImageExtractor</c> が単一所有する）。
/// </remarks>
public readonly record struct LoupeDrawContent(
    EsRect SurfaceBounds,
    LoupeCheckerTiles CheckerTiles,
    IReadOnlyList<EsRect> CrosshairRects,
    IReadOnlyList<EsRect> FrameRects,
    IReadOnlyList<LoupeDrawLayer> Layers);

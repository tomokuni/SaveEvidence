using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore.Drawing;

/// <summary>拡大画像の切り出しと描画の矩形（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="Crop">元画像から切り出す領域（画像ローカルピクセル・正方形。範囲外は透明パディングになる）。</param>
/// <param name="Source">切り出した画像内の描画元矩形（左上原点・切り出しサイズ）。</param>
/// <param name="Destination">描画面への描画先矩形（左上原点・切り出しサイズ × 拡大率）。</param>
/// <remarks>
/// 中心 → 切り出し領域の左上の変換（切り上げを含む）と「切り出した領域を左上原点から拡大率倍で描く」規則を<br/>
/// コアが単一所有する（描画実装は計算を持たない）。<br/>
/// 切り出し元の画像（<see cref="EsUtil.Platform.Core.Imaging.EsBitmap"/>）は本値に含めない<br/>
/// （描画の起動時に引数で受け取る。切り出しの実行は <c>EsBitmap.Crop</c> が担う）。<br/>
/// <b>描画先は切り上げ分だけ表面より大きくなりうる</b>（最大 <c>拡大率 - 1</c> 物理ピクセル）。<br/>
/// はみ出した部分は描画先のクリップに任せる（切り捨てると拡大の倍率が整数でなくなり画素がぼやける）。
/// </remarks>
public readonly record struct LoupeMagnifiedImage(EsRectInt Crop, EsRect Source, EsRect Destination);

/// <summary>ルーペの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="SurfaceBounds">描画面の全体（左上を原点とし、幅・高さが物理ピクセルサイズの正方形）。</param>
/// <param name="CheckerTiles">市松模様の暗色タイルの矩形を列挙する構造体（タイルなしの場合は空）。</param>
/// <param name="CrosshairRects">十字線の矩形（縦 1 本・横 1 本。拡大率 1 の場合は空）。</param>
/// <param name="FrameRects">外枠の矩形（上・下・左・右の 4 辺。太さ 0 の場合は空）。</param>
/// <param name="MagnifiedImage">拡大画像の切り出しと描画の矩形。null の場合は描かない（拡大中心未設定）。</param>
/// <param name="CoordLabelText">中心座標ラベルの文言と基準点。null の場合は表示しない（非表示の設定または中心未設定）。</param>
/// <param name="Layers">描く図形の層とその順序（<b>描画順はコアが単一所有する</b>）。先頭から順に処理する。</param>
/// <remarks>
/// <b>描画に必要な値はすべて本値に解決済み</b>である（描画実装は計算を持たず、<paramref name="Layers"/> の順に写すだけ）。<br/>
/// したがって描画実装が幾何計算（タイルの位置・矩形の導出・寸法の換算・切り出し領域の算出）を行うことはない。<br/>
/// <b>色と「描くかどうか」の最終判断は描画側に残る</b>: アルファ 0 の色は描かない（描画にだけ必要な設定であるため）。<br/>
/// <b>文字の層は含まない</b>: テキストは <see cref="CoordLabelText"/> として値を渡し、<br/>
/// UI 層が要素として置くか画像として描くかを決める（要素は親より後に描かれるため常に最上位になる）。<br/>
/// <b>画像の層</b>: 拡大元の型は UI に依存するため本値に含めず、描画の起動時に引数で受け取る<br/>
/// （切り出す領域と描画の矩形は <see cref="MagnifiedImage"/> が単一所有する）。
/// </remarks>
public readonly record struct LoupeDrawContent(
    EsRect SurfaceBounds,
    LoupeCheckerTiles CheckerTiles,
    IReadOnlyList<EsRect> CrosshairRects,
    IReadOnlyList<EsRect> FrameRects,
    LoupeMagnifiedImage? MagnifiedImage,
    LoupeTextPlacement? CoordLabelText,
    IReadOnlyList<LoupeDrawLayer> Layers);

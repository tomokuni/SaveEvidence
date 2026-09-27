using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore.Drawing;

/// <summary>ルーペの描画 1 フレーム分の幾何（コアが責任を持つ値の全量）。</summary>
/// <param name="Center">拡大中心（ドメイン座標）。未設定は null（拡大画像を描かず、中心座標ラベルも表示しない）。</param>
/// <param name="DisplaySize">ルーペの一辺の表示サイズ（DIP。ラベルの位置の基準であり、物理ピクセルサイズの換算元）。</param>
/// <param name="PixelSize">ルーペの一辺のサイズ（物理ピクセル。切り上げ・最低 1。描画先が指定された場合はクランプ済み）。</param>
/// <param name="DipToDeviceScaling">表示 DIP → 物理ピクセルのスケーリング係数（環境値）。</param>
/// <param name="ZoomLevel">拡大率（設定の補正済みの値）。</param>
/// <param name="CaptureSize">切り出し領域の一辺（ドメインピクセル。<see cref="PixelSize"/> を <see cref="ZoomLevel"/> で割った切り上げ）。</param>
/// <remarks>
/// <b>本値がコアの公開する描画の入口である</b>: コアは拡大画像と描画面の幾何だけを返し、<br/>
/// 装飾（十字線・枠線）・文言・表示可否・描画順は描画の都合として描画層（<c>LoupeControl.UIBase</c> の <c>LoupePresenter</c>）が解決する。<br/>
/// <b>導出値も本値が単一所有する</b>: 描画面の矩形・切り出し領域の左上・拡大画像の切り出しと描画の矩形は本値のメンバーが持つ<br/>
/// （描画層が矩形を組み立てると原点の取り違えが生じる）。<br/>
/// <b>寸法は換算済みの値を保持する</b>: DIP の表示サイズを物理ピクセルへ換算する手順はコアが単一所有する<br/>
/// （描画層は <see cref="PixelSize"/> をそのまま使う）。<br/>
/// 拡大元画像（<c>EsBitmap</c>）は含めない（型が UI に依存するため、描画の起動時に引数で受け取る）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct LoupeGeometry(
    EsPoint? Center,
    double DisplaySize,
    int PixelSize,
    double DipToDeviceScaling,
    int ZoomLevel,
    int CaptureSize)
{
    /// <summary>描画先の全領域（ルーペの一辺が作る正方形）を取得する。</summary>
    /// <value>左上を原点（0, 0）とし、幅・高さが <see cref="PixelSize"/> の矩形（物理ピクセル）。</value>
    /// <remarks>描画先の原点は常に 0 である（描画層が矩形を組み立てると原点の取り違えが生じるため、本値が導出する）。</remarks>
    public EsRect SurfaceBounds => new(0, 0, PixelSize, PixelSize);

    /// <summary>拡大中心から切り出し領域の左上（ドメイン座標）を取得する。</summary>
    /// <value>切り出し領域の左上（ドメイン座標）。<see cref="Center"/> が未設定の場合は null（描画しない）。</value>
    /// <remarks>
    /// 中心の整数化（<c>(int)</c>）は切り出し（<c>EsBitmap.Crop</c>）と同じ丸めにする<br/>
    /// （切り出しの実行は描画層が、切り出し領域の通知は描画層が本値から組み立てる）。
    /// </remarks>
    public EsPoint? CaptureTopLeft => Center is { } center
        ? new EsPoint((int)center.X - CaptureSize / 2, (int)center.Y - CaptureSize / 2)
        : null;

    /// <summary>拡大画像の切り出しと描画の矩形を取得する。</summary>
    /// <value>切り出しと描画の矩形。<see cref="Center"/> が未設定の場合は null（描画しない）。</value>
    /// <remarks>
    /// 「切り出した領域を左上原点から <see cref="ZoomLevel"/> 倍の大きさで描く」規則を単一所有する。<br/>
    /// 切り出し元の画像は含めない（切り出しの実行は描画層が <c>EsBitmap.Crop</c> で行う）。<br/>
    /// <b>描画先は切り上げ分だけ表面より大きくなりうる</b>（最大 <c>ZoomLevel - 1</c> 物理ピクセル）。<br/>
    /// はみ出した部分は描画先のクリップに任せる（切り捨てると拡大の倍率が整数でなくなり画素がぼやける）。
    /// </remarks>
    public LoupeMagnifiedImage? MagnifiedImage => CaptureTopLeft is not { } topLeft
        ? null
        : new LoupeMagnifiedImage(
            new EsRectInt((int)topLeft.X, (int)topLeft.Y, CaptureSize, CaptureSize),
            new EsRect(0, 0, CaptureSize, CaptureSize),
            new EsRect(0, 0, CaptureSize * ZoomLevel, CaptureSize * ZoomLevel));
}

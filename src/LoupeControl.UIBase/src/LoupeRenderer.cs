using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Drawing;


namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの描画の既定実装（色を 1 値で保持し、層の順序は本層の基底が単一所有する）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（層の順序は <see cref="LoupeRendererBase"/> が所有し、<br/>
/// 本クラスが層ごとの描き方と色を与える）と Strategy（<see cref="ILoupeRenderer"/> として差し替えられる）。<br/>
/// <br/>
/// <b>色の所有者</b>: 色・フォントサイズ・角丸は<b>描画にだけ必要な設定</b>であり、コアの計算・判定には入らない。<br/>
/// したがって <see cref="LoupePaint"/> が単一所有し、本クラスが保持する（コアの型面にも描画の契約にも現れない）。<br/>
/// 描画の可否（アルファ 0 の色は描かない）の判定も <see cref="LoupePaint"/> の述語が単一所有する。<br/>
/// <b>共有しない理由</b>: 色はコントロールごとに異なる（各自の設定色で描く）。色を変更する場合はインスタンスを<br/>
/// 差し替える（同じ実装を共有したまま書き換えると、他のコントロールの見た目まで変わる）。<br/>
/// <br/>
/// <b>UI 非依存</b>: 描画 API は <see cref="IEsCanvas"/> の実装が担うため、本クラスは UI フレームワークを<br/>
/// 参照しない。各 UI は本クラスを継承し、自 UI の描画面を確保する薄い層（<c>LoupeRenderer~UI</c>）を用意する。
/// </remarks>
public class LoupeRenderer : LoupeRendererBase
{
    /// <summary>描画に使う色。</summary>
    private readonly LoupePaint _paint;

    /// <summary>描画に使う色を指定して生成する。</summary>
    /// <param name="paint">描画に使う色（<see cref="LoupePaint.Default"/> を基点に組み立てる）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> が null の場合。</exception>
    public LoupeRenderer(LoupePaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);

        _paint = paint;
    }

    /// <summary>描画に使う色を取得する。</summary>
    /// <value>本クラスが保持する色（描画にだけ必要な設定の単一値）。派生クラスも同じ値を参照する。</value>
    protected LoupePaint Paint => _paint;

    /// <summary>中心座標ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>安全域（6〜48）へ補正済みのフォントサイズ。既定は 12.0。</value>
    /// <remarks>
    /// 値の所有者は描画の設定（<see cref="LoupePaint"/>）である（本プロパティは同じ値への参照）。<br/>
    /// テキストを canvas へ直接描く実装（<see cref="LoupeRendererBase.DrawLabel"/> を上書きする派生）が参照する。<br/>
    /// <b>描画の契約（<c>ILoupeRenderer</c>）には含めない</b>（コアは値を必要とせず、UI 要素として置く UI も参照しない）。
    /// </remarks>
    public double CoordLabelFontSize => _paint.CoordLabelFontSize;

    /// <summary>中心座標ラベルの背景の角丸半径（DIP）を取得する。</summary>
    /// <value>角丸半径（DIP）。0〜64 へ補正済み（0 は四角形）。既定は 2。</value>
    /// <remarks>値の所有者は描画の設定（<see cref="LoupePaint"/>）である（本プロパティは同じ値への参照）。</remarks>
    public double CoordLabelCornerRadius => _paint.CoordLabelCornerRadius;

    /// <inheritdoc/>
    protected override void DrawBackground(IEsCanvas canvas, in LoupeDrawContent content)
        => canvas.Clear(content.SurfaceBounds, _paint.BackgroundColor);

    /// <inheritdoc/>
    protected override void DrawCheckerboard(IEsCanvas canvas, in LoupeDrawContent content)
    {
        canvas.FillRect(content.SurfaceBounds, _paint.CheckerColorA);

        // タイル数は描画先の大きさに比例して増えるため、リストを確保しない列挙子をそのまま渡す
        canvas.FillRects(new CheckerTileWalker(content.CheckerTiles), _paint.CheckerColorB);
    }

    /// <inheritdoc/>
    protected override void DrawCrosshair(IEsCanvas canvas, in LoupeDrawContent content)
        => canvas.FillRects(content.CrosshairRects, _paint.CrossColor);

    /// <inheritdoc/>
    protected override void DrawFrame(IEsCanvas canvas, in LoupeDrawContent content)
        => canvas.FillRects(content.FrameRects, _paint.FrameColor);

    /// <summary>コアが解決した市松模様の暗色タイルを、描画ポートの列挙子の契約へ適合させる構造体。</summary>
    /// <remarks>
    /// タイルの位置と個数の計算はコア（<see cref="LoupeCheckerTiles"/>）が単一所有する（描画側で計算しない）。<br/>
    /// 本構造体はコアの列挙子（コアは描画 API を知らない）を描画ポートの契約へ写すだけであり、<br/>
    /// リストを確保しないためタイル数が多くてもヒープ確保は発生しない。
    /// </remarks>
    /// <param name="tiles">コアが解決した暗色タイルの列挙子。</param>
    private struct CheckerTileWalker(LoupeCheckerTiles tiles) : IEsRectWalker<CheckerTileWalker>
    {
        /// <summary>コアの列挙子（走査位置を持つため値で保持する）。</summary>
        private LoupeCheckerTiles _tiles = tiles;

        /// <summary>次の暗色タイルへ進む。</summary>
        /// <returns>次のタイルがある場合は true。列の末尾を超えた場合は false。</returns>
        public bool MoveNext() => _tiles.MoveNext();

        /// <summary>現在の暗色タイルの矩形を取得する。</summary>
        /// <value>直近の <see cref="MoveNext"/> が true を返したときの矩形。</value>
        public readonly EsRect Current => _tiles.Current;
    }
}

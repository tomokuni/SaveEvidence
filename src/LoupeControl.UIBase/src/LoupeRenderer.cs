using EsUtil.Feature.LoupeCore;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Contract;


namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの描画の既定実装（色を 1 値で保持し、層の順序はコアが単一所有する）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（層の順序はコアの <see cref="LoupeRendererBase"/> が所有し、<br/>
/// 本クラスが層ごとの描き方と色を与える）と Strategy（<see cref="ILoupeRenderer"/> として差し替えられる）。<br/>
/// <br/>
/// <b>色の所有者</b>: 色は<b>描画にだけ必要な設定</b>であり、コアの計算・判定には入らない。したがって色は<br/>
/// <see cref="LoupePaint"/> が単一所有し、本クラスが保持する（コアの型面に色は現れない）。<br/>
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

    /// <inheritdoc/>
    public override double CoordLabelFontSize => _paint.CoordLabelFontSize;

    /// <inheritdoc/>
    public override double CoordLabelCornerRadius => _paint.CoordLabelCornerRadius;

    /// <inheritdoc/>
    protected override void DrawBackground(IEsCanvas canvas, in LoupeDrawContent content)
        => canvas.Clear(content.SurfaceBounds, _paint.BackgroundColor);

    /// <inheritdoc/>
    protected override void DrawCheckerboard(IEsCanvas canvas, in LoupeDrawContent content)
    {
        if (_paint.HasCheckerColorA)
        {
            canvas.FillRect(content.SurfaceBounds, _paint.CheckerColorA);
        }

        if (!_paint.HasCheckerColorB)
        {
            return;
        }

        // タイル数は描画先の大きさに比例して増えるため、リストを確保しない列挙子をそのまま渡す
        canvas.FillRects(content.CheckerTiles, _paint.CheckerColorB);
    }

    /// <inheritdoc/>
    protected override void DrawCrosshair(IEsCanvas canvas, in LoupeDrawContent content)
    {
        if (!_paint.HasCrosshair)
        {
            return;
        }

        canvas.FillRects(content.CrosshairRects, _paint.CrossColor);
    }

    /// <inheritdoc/>
    protected override void DrawFrame(IEsCanvas canvas, in LoupeDrawContent content)
    {
        if (!_paint.HasFrame)
        {
            return;
        }

        canvas.FillRects(content.FrameRects, _paint.FrameColor);
    }
}

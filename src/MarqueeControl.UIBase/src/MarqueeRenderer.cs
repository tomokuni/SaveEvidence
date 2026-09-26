using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Drawing;


namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>選択矩形コントロールの描画の既定実装（色を 1 値で保持し、層の順序は本層の基底が単一所有する）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（層の順序は <see cref="MarqueeRendererBase"/> が所有し、<br/>
/// 本クラスが層ごとの描き方と色を与える）と Strategy（<see cref="IMarqueeRenderer"/> として差し替えられる）。<br/>
/// <br/>
/// <b>色の所有者</b>: 色とフォントサイズは<b>描画にだけ必要な設定</b>であり、コアの計算・判定には入らない。したがって<br/>
/// <see cref="MarqueePaint"/> が単一所有し、本クラスが保持する（コアの型面にも描画の契約にも現れない）。<br/>
/// <b>共有しない理由</b>: 色はコントロールごとに異なる（各自の設定色で描く）。色を変更する場合はインスタンスを<br/>
/// 差し替える（同じ実装を共有したまま書き換えると、他のコントロールの見た目まで変わる）。<br/>
/// <br/>
/// <b>UI 非依存</b>: 描画 API は <see cref="IEsCanvas"/> の実装が担うため、本クラスは UI フレームワークを<br/>
/// 参照しない。各 UI は本クラスを継承し、自 UI の描画面を確保する薄い層（<c>MarqueeRenderer~UI</c>）を用意する。
/// </remarks>
public class MarqueeRenderer : MarqueeRendererBase
{
    /// <summary>描画に使う色。</summary>
    private readonly MarqueePaint _paint;

    /// <summary>描画に使う色を指定して生成する。</summary>
    /// <param name="paint">描画に使う色（<see cref="MarqueePaint.Default"/> を基点に組み立てる）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> が null の場合。</exception>
    public MarqueeRenderer(MarqueePaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);

        _paint = paint;
    }

    /// <summary>描画に使う色を取得する。</summary>
    /// <value>本クラスが保持する色（描画にだけ必要な設定の単一値）。派生クラスも同じ値を参照する。</value>
    protected MarqueePaint Paint => _paint;

    /// <summary>サイズラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>安全域（6〜48）へ補正済みのフォントサイズ。既定は 14.0。</value>
    /// <remarks>
    /// 値の所有者は描画の設定（<see cref="MarqueePaint"/>）である（本プロパティは同じ値への参照）。<br/>
    /// テキストを canvas へ直接描く実装（<see cref="MarqueeRendererBase.DrawLabel"/> を上書きする派生）が参照する。<br/>
    /// <b>描画の契約（<c>IMarqueeRenderer</c>）には含めない</b>（コアは値を必要とせず、UI 要素として置く UI も参照しない）。
    /// </remarks>
    public double SizeLabelFontSize => _paint.SizeLabelFontSize;

    /// <summary>カーソル座標ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>安全域（6〜48）へ補正済みのフォントサイズ。既定は 12.0。</value>
    /// <remarks>値の所有者は描画の設定（<see cref="MarqueePaint"/>）である（本プロパティは同じ値への参照）。</remarks>
    public double CoordLabelFontSize => _paint.CoordLabelFontSize;

    /// <inheritdoc/>
    /// <remarks>外側（暗転マスク・穴あけ枠）と内側（塗り・枠線）の描画は、それぞれの描画内容へ委譲する。</remarks>
    protected override void DrawDecoration(IEsCanvas canvas, in MarqueeDrawContent content)
    {
        MarqueeOuterRenderer.Draw(canvas, content.Decoration, _paint);
        MarqueeInnerRenderer.Draw(canvas, content.InnerDecoration, _paint);
    }

    /// <inheritdoc/>
    protected override void DrawHandles(IEsCanvas canvas, in MarqueeHandles handles)
    {
        foreach (var bounds in handles.Rects)
        {
            canvas.FillRect(bounds, _paint.HandleColor);
            canvas.StrokeRect(bounds, _paint.HandleBorderColor, handles.BorderThickness);
        }
    }

    /// <inheritdoc/>
    protected override void DrawStartMarker(IEsCanvas canvas, in MarqueeMarker marker)
    {
        var center = marker.Center;
        var half = marker.Size;

        canvas.DrawLine(
            new EsPoint(center.X - half, center.Y), new EsPoint(center.X + half, center.Y),
            _paint.MarkerColor, marker.Thickness);
        canvas.DrawLine(
            new EsPoint(center.X, center.Y - half), new EsPoint(center.X, center.Y + half),
            _paint.MarkerColor, marker.Thickness);
    }
}

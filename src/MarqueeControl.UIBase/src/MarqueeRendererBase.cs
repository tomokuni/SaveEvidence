using EsUtil.Feature.MarqueeCore;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.Feature.MarqueeCore.Selection;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using MarqueeFacade = EsUtil.Feature.MarqueeCore.Marquee;

namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>選択矩形コントロールの描画手順（層の順序に従った描画ポートの呼び出し）を単一所有する基底クラス（UI フレームワーク非依存）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（手順を本クラスが所有し、色は <see cref="Paint"/> を実装する派生クラスが与える）。<br/>
/// <br/>
/// <b>位置付け</b>: 「どう描くか」を単一所有する描画側の中核である。コアが決めた描画内容<br/>
/// （<see cref="MarqueeDrawContent"/>）を受け取り、描画ポート（<see cref="IEsCanvas"/>）へ写すだけであり、<br/>
/// 「描くかどうか」「どの順に描くか」「どの寸法か」の判断は一切持たない（色は本層の設定から引く）。<br/>
/// <br/>
/// <b>色の所有者</b>: 色は<b>描画にだけ必要な設定</b>であり、コアの計算には入らない。したがって色は本層の<br/>
/// <see cref="MarqueePaint"/> が単一所有し、派生クラスが <see cref="Paint"/> で与える（コアの契約に色を含めない）。<br/>
/// <br/>
/// <b>描画順はコアが単一所有する</b>: <see cref="MarqueeDrawContent.Layers"/> の順に層を処理する。<br/>
/// <b>ラベルは UI 要素が描く</b>: ラベル（サイズ・カーソル座標）は <see cref="ResolveLabels"/> が配置指示を返し、<br/>
/// UI の要素が描く（要素は親より後に描かれるため常に最上位になる）。本クラスはラベルの層を描かない。<br/>
/// <br/>
/// <b>UI 非依存</b>: 描画 API は <see cref="IEsCanvas"/> の実装（例: Skia は <c>SkiaEsCanvas</c>）が担うため、<br/>
/// 本クラスは UI フレームワークとウィンドウを必要としない（UI 初期化なしの <c>[Fact]</c> でピクセル検証できる）。
/// </remarks>
public abstract class MarqueeRendererBase : IMarqueeRenderer
{
    /// <summary>描画に使う色（描画にだけ必要な設定）を取得する。</summary>
    /// <value>本クラスの派生クラスが与える色。null 不可。</value>
    protected abstract MarqueePaint Paint { get; }

    /// <summary>図形の層（装飾・ハンドル・始点マーカー）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="content">コアが解決した描画内容（層の順序を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// ラベルの層は描かない（UI が要素として配置するため）。描くものが無い層は値が null のため何もしない。<br/>
    /// 描画面の消去（前フレームの残像の除去）は呼び出し側が行う（描画面の寿命は UI が所有するため）。
    /// </remarks>
    public void DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // 描画順はコア（Layers）が単一所有する。本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in content.Layers)
        {
            switch (layer)
            {
                case MarqueeDrawLayer.Decoration:
                    MarqueeOuterRenderer.Draw(canvas, content.Decoration, Paint);
                    MarqueeInnerRenderer.Draw(canvas, content.InnerDecoration, Paint);
                    break;

                case MarqueeDrawLayer.Handles when content.Handles is { } handles:
                    DrawHandles(canvas, handles);
                    break;

                case MarqueeDrawLayer.Marker when content.Marker is { } marker:
                    DrawStartMarker(canvas, marker);
                    break;

                // ラベルは UI が要素として配置する（本メソッドは描かない）
                case MarqueeDrawLayer.SizeLabel:
                case MarqueeDrawLayer.CoordLabel:
                    break;
            }
        }
    }

    /// <summary>ラベル要素の配置指示を描画順に解決する（UI 要素として配置する UI 用）。</summary>
    /// <param name="content">コアが解決した描画内容（文言・寸法・層の順序を持つ）。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素の一覧（サイズラベル → カーソル座標ラベルの順）。どちらも無い場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>実寸 → 配置矩形の写像は <see cref="MarqueeLabelPlan"/> が単一所有する（本メソッドは委譲するだけである）。</remarks>
    public IReadOnlyList<EsLabelElement> ResolveLabels(
        in MarqueeDrawContent content, Func<string, double, EsSize> measureText)
        => MarqueeLabelPlan.Resolve(content, Paint, measureText);

    /// <summary>ドラッグハンドル（塗り矩形 + 枠線）を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="handles">コアが解決したハンドルの描画内容（矩形・大きさ・枠線の太さを持つ）。</param>
    /// <remarks>
    /// 8 方向のハンドル（<see cref="SelectionGeometry.DirectionalHandles"/>。None / Move を除く）を列挙し、<br/>
    /// <see cref="SelectionGeometry.GetHandlePosition"/> から中心位置を取得して描く（ハンドル位置の計算はコアが単一所有する）。<br/>
    /// ハンドルの追加時は <see cref="HitTarget"/> の追加のみで描画が追従する。
    /// </remarks>
    private void DrawHandles(IEsCanvas canvas, in MarqueeHandles handles)
    {
        var half = handles.HandleSize / 2;

        foreach (var handle in SelectionGeometry.DirectionalHandles)
        {
            var center = SelectionGeometry.GetHandlePosition(handles.Rect, handle);
            var bounds = new EsRect(center.X - half, center.Y - half, handles.HandleSize, handles.HandleSize);

            canvas.FillRect(bounds, Paint.HandleColor);
            canvas.StrokeRect(bounds, Paint.HandleBorderColor, handles.BorderThickness);
        }
    }

    /// <summary>始点マーカー（十字線）を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="marker">コアが解決したマーカーの描画内容（中心・太さ・半幅を持つ）。</param>
    /// <remarks>太さはコア（<see cref="MarqueeMarker.Thickness"/>）が、色は <see cref="Paint"/> が単一所有する。</remarks>
    private void DrawStartMarker(IEsCanvas canvas, in MarqueeMarker marker)
    {
        var center = marker.Center;
        var half = marker.Size;

        canvas.DrawLine(
            new EsPoint(center.X - half, center.Y), new EsPoint(center.X + half, center.Y),
            Paint.MarkerColor, marker.Thickness);
        canvas.DrawLine(
            new EsPoint(center.X, center.Y - half), new EsPoint(center.X, center.Y + half),
            Paint.MarkerColor, marker.Thickness);
    }
}

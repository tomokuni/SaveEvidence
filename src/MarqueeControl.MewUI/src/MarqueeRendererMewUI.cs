using EsUtil.Feature.MarqueeCore;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Marquee.UIBase;
using SkiaSharp;

namespace EsUtil.SaveEvidence.Marquee.MewUI;

/// <summary>MewUI 版の描画の既定実装（自 UI の描画面を描画ポートへ適合させる）。</summary>
/// <remarks>
/// <b>位置付け</b>: 描画の手順（層の順序）はコアの <c>MarqueeRendererBase</c> が、色は <see cref="MarqueePaint"/> を<br/>
/// 保持する <see cref="MarqueeRenderer"/>（UIBase）が単一所有する。本クラスは<b>UI フレームワーク固有の描画</b>だけを<br/>
/// 担う層であり、現在は「自 UI の描画面（<see cref="SKCanvas"/>）を描画ポート（<c>SkiaEsCanvas</c>）へ<br/>
/// 適合させる」ことがその中身である。<br/>
/// <br/>
/// <b>拡張の場所</b>: <c>IEsCanvas</c> では扱えない UI 固有の描画が必要になった場合は本クラスへ追加する<br/>
/// （コアへ UI 型を持ち込まないため）。<br/>
/// <br/>
/// <b>差し替え</b>: 別の実装で描く場合は <c>MarqueeControl.Renderer</c> へ <c>IMarqueeRenderer</c> の実装を設定する<br/>
/// （その場合の描画はコアの <c>Marquee.Draw</c> 経由になる）。
/// </remarks>
public sealed class MarqueeRendererMewUI(MarqueePaint paint) : MarqueeRenderer(paint)
{
    /// <summary>MewUI（Skia）のキャンバスから図形の層を描く。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。null 不可。</param>
    /// <param name="content">コアが解決した描画内容（層の順序を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>描画面の確保（<see cref="SKCanvas"/> → <c>IEsCanvas</c> の適合）が本 UI 固有の処理である。</remarks>
    public void Draw(SKCanvas canvas, in MarqueeDrawContent content)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        DrawShapes(new SkiaEsCanvas(canvas), in content);
    }
}

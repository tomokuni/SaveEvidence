using Avalonia.Media;
using EsUtil.Feature.MarqueeCore;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Marquee.UIBase;
using EsUtil.Feature.MarqueeCore.Contracts;
using EsUtil.Feature.MarqueeCore.Drawing;


namespace EsUtil.SaveEvidence.Marquee.AvaloniaUI;

/// <summary>Avalonia 版の描画の既定実装（自 UI の描画面を描画ポートへ適合させる）。</summary>
/// <remarks>
/// <b>位置付け</b>: 描画の手順（層の順序）はコアの <c>MarqueeRendererBase</c> が、色は <see cref="MarqueePaint"/> を<br/>
/// 保持する <see cref="MarqueeRenderer"/>（UIBase）が単一所有する。本クラスは<b>UI フレームワーク固有の描画</b>だけを<br/>
/// 担う層であり、現在は「自 UI の描画面（<see cref="DrawingContext"/>）を描画ポート（<c>AvaloniaCanvas</c>）へ<br/>
/// 適合させる」ことがその中身である。<br/>
/// <br/>
/// <b>拡張の場所</b>: <c>IEsCanvas</c> では扱えない UI 固有の描画（要素ベースの描画・自 UI の API を要する処理等）が<br/>
/// 必要になった場合は本クラスへ追加する（コアへ UI 型を持ち込まないため）。<br/>
/// <br/>
/// <b>差し替え</b>: 別の実装で描く場合は <c>MarqueeControl.Renderer</c> へ <c>IMarqueeRenderer</c> の実装を設定する<br/>
/// （その場合の描画はコアの <c>Marquee.Draw</c> 経由になる）。
/// </remarks>
public sealed class MarqueeRendererAvaloniaUI(MarqueePaint paint) : MarqueeRenderer(paint)
{
    /// <summary>Avalonia の描画コンテキストから図形の層を描く。</summary>
    /// <param name="context">描画コンテキスト。<c>Render</c> の実行中のみ有効。null 不可。</param>
    /// <param name="content">コアが解決した描画内容（層の順序を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <remarks>描画面の確保（<c>DrawingContext</c> → <c>IEsCanvas</c> の適合）が本 UI 固有の処理である。</remarks>
    public void Draw(DrawingContext context, in MarqueeDrawContent content)
    {
        ArgumentNullException.ThrowIfNull(context);

        DrawShapes(new AvaloniaCanvas(context), in content);
    }
}

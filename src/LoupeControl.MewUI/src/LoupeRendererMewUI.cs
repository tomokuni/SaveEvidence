using EsUtil.Feature.LoupeCore;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Loupe.UIBase;
using SkiaSharp;
using EsUtil.Platform.Skia;



namespace EsUtil.SaveEvidence.Loupe.MewUI;

/// <summary>MewUI 版の描画の既定実装（自 UI の描画面を描画ポートへ適合させる）。</summary>
/// <remarks>
/// <b>位置付け</b>: 描画の手順（層の順序）はコアの <c>LoupeRendererBase</c> が、色は <see cref="LoupePaint"/> を<br/>
/// 保持する <see cref="LoupeRenderer"/>（UIBase）が単一所有する。本クラスは<b>UI フレームワーク固有の描画</b>だけを<br/>
/// 担う層であり、現在は「自 UI の描画面（<see cref="SKCanvas"/>）を描画ポート（<c>SkiaEsCanvas</c>）へ<br/>
/// 適合させる」ことがその中身である。<br/>
/// <br/>
/// <b>描画面の生成元</b>: MewUI 版は <c>SkiaCanvasView.PaintSurface</c> が渡すキャンバスをそのまま使う<br/>
/// （中間ビットマップを持たない同期直描のため、<c>LoupeRenderCoordinator</c> を使わない）。<br/>
/// <br/>
/// <b>拡張の場所</b>: <c>IEsCanvas</c> では扱えない UI 固有の描画が必要になった場合は本クラスへ追加する<br/>
/// （コアへ UI 型を持ち込まないため）。
/// </remarks>
public sealed class LoupeRendererMewUI(LoupePaint paint) : LoupeRenderer(paint)
{
    /// <summary>MewUI（Skia）のキャンバスから図形の層を描く。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル等倍で描画できる状態であること）。null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は市松模様のみを描く。</param>
    /// <param name="request">描画入力（拡大画像の切り出しに使う値）。</param>
    /// <param name="content">コアが解決した描画内容（層の順序・矩形を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>描画面の確保（<see cref="SKCanvas"/> → <c>IEsCanvas</c> の適合）が本 UI 固有の処理である。</remarks>
    public void Draw(
        SKCanvas canvas, EsBitmap? source, in LoupeRenderRequest request, in LoupeDrawContent content)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        DrawShapes(new SkiaEsCanvas(canvas), source, in request, in content);
    }
}

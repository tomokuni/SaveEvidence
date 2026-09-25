using Avalonia.Media;
using EsUtil.Feature.LoupeCore;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Loupe.UIBase;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Loupe.AvaloniaUI;

/// <summary>Avalonia 版の描画の既定実装（自 UI の描画面を描画ポートへ適合させる）。</summary>
/// <remarks>
/// <b>位置付け</b>: 描画の手順（層の順序）はコアの <c>LoupeRendererBase</c> が、色は <see cref="LoupePaint"/> を<br/>
/// 保持する <see cref="LoupeRenderer"/>（UIBase）が単一所有する。本クラスは<b>UI フレームワーク固有の描画</b>だけを<br/>
/// 担う層であり、現在は「自 UI の描画面（<see cref="DrawingContext"/>）を描画ポート（<c>AvaloniaCanvas</c>）へ<br/>
/// 適合させる」ことがその中身である。<br/>
/// <br/>
/// <b>描画面の生成元</b>: Avalonia 版は中間ビットマップ（<c>RenderTargetBitmap</c>）の描画コンテキストを渡す<br/>
/// （<c>LoupeRenderPipeline</c> が生成する）。<br/>
/// <br/>
/// <b>拡張の場所</b>: <c>IEsCanvas</c> では扱えない UI 固有の描画が必要になった場合は本クラスへ追加する<br/>
/// （コアへ UI 型を持ち込まないため）。
/// </remarks>
public sealed class LoupeRendererAvaloniaUI(LoupePaint paint) : LoupeRenderer(paint)
{
    /// <summary>Avalonia の描画コンテキストから図形の層を描く。</summary>
    /// <param name="context">描画コンテキスト。null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は市松模様のみを描く。</param>
    /// <param name="request">描画入力（コアと UI の契約）。</param>
    /// <param name="notification">コアが解決した通知（層の順序を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <remarks>描画面の確保（<c>DrawingContext</c> → <c>IEsCanvas</c> の適合）が本 UI 固有の処理である。</remarks>
    public void Draw(
        DrawingContext context, EsBitmap? source, in LoupeRenderRequest request, in LoupeRenderNotification notification)
    {
        ArgumentNullException.ThrowIfNull(context);

        DrawShapes(new AvaloniaCanvas(context), source, in request, in notification);
    }
}

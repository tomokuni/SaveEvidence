using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Imaging;
using Estable.Loupe;
using Estable.Loupe.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの描画（拡大画像＋市松模様背景＋十字線＋外枠）を提供するレンダラー。</summary>
/// <remarks>
/// 描画する層とその順序は本クラスが単一所有し、各層（<see cref="LoupeBackgroundRenderer"/> /<br/>
/// <see cref="LoupeImageRenderer"/> / <see cref="LoupeDecorationRenderer"/>）へ描画入力を渡すだけにする。<br/>
/// 各層は描画の可否・値の単位・幾何をコア（<see cref="LoupeAppearance"/> / <see cref="LoupeAppearanceScaler"/> /<br/>
/// <see cref="LoupeDecorationGeometry"/> / <see cref="LoupeCheckerboard"/> / <see cref="LoupeImageExtractor"/>）へ問い合わせる<br/>
/// （MewUI 版の描画層と同じ形。外観値はコアが単一所有し、本プロジェクトは保持しない）。<br/>
/// ブラシの共有は <see cref="LoupeBrushes"/>（本プロジェクト内で単一所有）へ委譲する。
/// </remarks>
internal static class LoupeRenderer
{
    /// <summary>ビットマップ生成時の DPI（1 ピクセル = 1 DIP として描く）。</summary>
    private static readonly Vector s_dpi = new(DpiScaler.StandardDpi, DpiScaler.StandardDpi);

    /// <summary>ルーペ画像を描画する（切り出せない場合は市松模様のみを描画する）。</summary>
    /// <param name="source">拡大元の 100% 画像（切り出し前）。未設定は null。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・外観・スケーリング・拡大中心）。</param>
    /// <returns>描画結果の EsBitmap（ホストがピクセルにオーバーレイを描画できる）。</returns>
    /// <remarks>
    /// 0. 下地（ベース色。透明なら描かない）を描画する。<br/>
    /// 1. 市松模様の背景を描画する。<br/>
    /// 2. ソース画像があれば切り出して拡大描画する（切り出しの可否はコアの <see cref="LoupeImageExtractor.TryExtractRegionAt"/> が判断する）。<br/>
    /// 3. 十字線を描画する。<br/>
    /// 4. 外枠を描画する。<br/>
    /// 描画は <see cref="RenderTargetBitmap"/> で行い、結果を <see cref="EsBitmap"/> へコピーして返す<br/>
    /// （ホストが <see cref="LoupeControl.Rendered"/> イベントの引数で受け取り、<c>Lock</c> して<br/>
    /// オーバーレイ（線分の破線等）を直接描画できるようにするため）。<br/>
    /// 例外はそのまま伝播し、記録は呼び出し側（<see cref="LoupeControl"/>）が一元的に行う。
    /// </remarks>
    public static EsBitmap DrawLoupe(EsBitmap? source, LoupeRenderRequest request)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(request.PixelSize, request.PixelSize), s_dpi);

        using (var ctx = rtb.CreateDrawingContext())
        {
            // 下地（ベース色。透明なら描かない）→ 市松模様 → 拡大画像 → 十字線 → 外枠
            DrawBackground(ctx, request);
            LoupeBackgroundRenderer.DrawCheckerboard(ctx, request);
            LoupeImageRenderer.Draw(ctx, source, request);
            LoupeDecorationRenderer.DrawCrosshair(ctx, request);
            LoupeDecorationRenderer.DrawFrame(ctx, request);
        }

        return rtb.ToEsBitmap();
    }

    /// <summary>下地（ベース色）を全面へ描画する（透明なら描かない）。</summary>
    /// <param name="ctx">描画コンテキスト。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観）。</param>
    /// <remarks>
    /// 市松模様の色は半透明のため、背後の要素を透かしたくない場合はホストが不透明色を指定する。<br/>
    /// 描画の可否（アルファ 0 は描画しない）はコアの <see cref="LoupeAppearance.HasBackground"/> が単一所有する。<br/>
    /// 描画先（<see cref="RenderTargetBitmap"/>）は一辺 <see cref="LoupeRenderRequest.PixelSize"/> で作るため、全面がこの矩形で覆われる。<br/>
    /// ブラシは <see cref="LoupeBrushes"/>（容量制限付きキャッシュ）で共有する。
    /// </remarks>
    private static void DrawBackground(DrawingContext ctx, LoupeRenderRequest request)
    {
        var appearance = request.Appearance;
        if (!appearance.HasBackground)
        {
            return;
        }

        ctx.DrawRectangle(
            LoupeBrushes.Get(appearance.BackgroundColor), null,
            new Rect(0, 0, request.PixelSize, request.PixelSize));
    }
}

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Imaging;
using Estable.Loupe;
using Estable.Loupe.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの描画（拡大画像＋市松模様背景＋十字線＋外枠）を提供するレンダラー。</summary>
/// <remarks>
/// <b>描く層とその順序はコアが単一所有する</b>: 本クラスは <see cref="LoupeDrawLayer"/> の順に各層を処理し、<br/>
/// 各層（<see cref="LoupeBackgroundRenderer"/> / <see cref="LoupeImageRenderer"/> /<br/>
/// <see cref="LoupeDecorationRenderer"/>）へ描画入力を渡すだけにする。<br/>
/// 各層は描画の可否・値の単位・幾何をコア（<see cref="LoupeAppearance"/> / <see cref="LoupeAppearanceScaler"/> /<br/>
/// <see cref="LoupeDecorationGeometry"/> / <see cref="LoupeCheckerboard"/> / <see cref="LoupeImageExtractor"/>）へ問い合わせる<br/>
/// （MewUI 版の描画層と同じ形。外観値はコアが単一所有し、本プロジェクトは保持しない）。<br/>
/// 中央座標ラベル（<see cref="LoupeDrawLayer.CoordLabel"/>）はテキストの実寸測定が必要なため、<br/>
/// 本層では描かず、コントロールがテキスト要素として表示する（層としては順序の契約に含まれる）。<br/>
/// ブラシの共有は <see cref="LoupeBrushes"/>（本プロジェクト内で単一所有）へ委譲する。
/// </remarks>
internal static class LoupeRenderer
{
    /// <summary>ビットマップ生成時の DPI（1 ピクセル = 1 DIP として描く）。</summary>
    private static readonly Vector s_dpi = new(DpiScaler.StandardDpi, DpiScaler.StandardDpi);

    /// <summary>ルーペ画像を描画する（切り出せない場合は市松模様のみを描画する）。</summary>
    /// <param name="source">拡大元の 100% 画像（切り出し前）。未設定は null。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・外観・スケーリング・拡大中心）。</param>
    /// <param name="layers">描く層とその順序（コアが単一所有する）。null 不可。</param>
    /// <returns>描画結果の EsBitmap（ホストがピクセルにオーバーレイを描画できる）。</returns>
    /// <remarks>
    /// <paramref name="layers"/> の順に層を処理する（描画順の判断を持たない）。<br/>
    /// 描画は <see cref="RenderTargetBitmap"/> で行い、結果を <see cref="EsBitmap"/> へコピーして返す<br/>
    /// （ホストが <see cref="LoupeControl.Rendered"/> イベントの引数で受け取り、<c>Lock</c> して<br/>
    /// オーバーレイ（線分の破線等）を直接描画できるようにするため）。<br/>
    /// 例外はそのまま伝播し、記録は呼び出し側（<see cref="LoupeControl"/>）が一元的に行う。
    /// </remarks>
    public static EsBitmap DrawLoupe(
        EsBitmap? source, LoupeRenderRequest request, IReadOnlyList<LoupeDrawLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);

        var rtb = new RenderTargetBitmap(new PixelSize(request.PixelSize, request.PixelSize), s_dpi);

        using (var ctx = rtb.CreateDrawingContext())
        {
            foreach (var layer in layers)
            {
                switch (layer)
                {
                    case LoupeDrawLayer.Background:
                        DrawBackground(ctx, request);
                        break;

                    case LoupeDrawLayer.Checkerboard:
                        LoupeBackgroundRenderer.DrawCheckerboard(ctx, request);
                        break;

                    case LoupeDrawLayer.Image:
                        LoupeImageRenderer.Draw(ctx, source, request);
                        break;

                    case LoupeDrawLayer.Crosshair:
                        LoupeDecorationRenderer.DrawCrosshair(ctx, request);
                        break;

                    case LoupeDrawLayer.Frame:
                        LoupeDecorationRenderer.DrawFrame(ctx, request);
                        break;

                    // 中央座標ラベルはテキスト要素（Border + TextBlock）が描く（親より後に描かれるため常に最上位になる）
                    case LoupeDrawLayer.CoordLabel:
                        break;
                }
            }
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
            request.SurfaceBounds.ToAvaloniaRect());
    }
}

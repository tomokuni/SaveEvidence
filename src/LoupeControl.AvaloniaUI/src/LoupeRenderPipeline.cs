using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Imaging;
using LoupeFacade = EsUtil.Feature.LoupeCore.Loupe;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.SaveEvidence.Loupe.AvaloniaUI;

/// <summary>ルーペのビットマップ生成パイプライン（ソース解決・描画の合成）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> から、Avalonia のソース画像を解決してルーペ画像（<see cref="EsBitmap"/>）を生成する<br/>
/// パイプラインを分離する（描画の手順と値の解釈は UI 非依存の描画層が単一所有する）。<br/>
/// <b>UI 固有の責務</b>: Avalonia の <see cref="IImage"/> 型判定と、<see cref="Bitmap"/> → <see cref="EsBitmap"/> の複製、<br/>
/// 描画の UI スレッドへのディスパッチ、<b>オフスクリーン ビットマップ（<see cref="RenderTargetBitmap"/>）の生成</b>。
/// 切り出しの可否（ソースと中心）と破棄済みの扱いはコアが単一所有する。
/// </remarks>
internal static class LoupeRenderPipeline
{
    /// <summary>オフスクリーン ビットマップの DPI（1 ピクセル = 1 DIP として描く）。</summary>
    private static readonly Vector s_dpi = new(DpiScaler.StandardDpi, DpiScaler.StandardDpi);

    /// <summary>画像抽出を非同期に実行し、Avaloniaの描画オブジェクトをUIスレッドで合成する。</summary>
    /// <param name="loupe">ルーペのファサード（描画実装を保持する。描画の起動は本型の入口 `Draw` を使う）。null 不可。</param>
    /// <param name="source">ルーペのソース画像。</param>
    /// <param name="request">描画入力（コアと UI の契約。UI が公開プロパティから組み立てる）。</param>
    /// <param name="content">コアが解決した描画内容（層の順序・矩形を持つ）。</param>
    /// <returns>生成したルーペ画像（<see cref="EsBitmap"/>）。</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="source"/> が <see cref="Bitmap"/> / <see cref="EsBitmapAvaloniaImage"/> 以外の場合。
    /// </exception>
    /// <remarks>
    /// 1. 未対応型の判定（<see cref="LoupeSourceResolver.IsSourceSupported"/>）を行い、未対応型は例外にする。<br/>
    /// 2. 切り出し元の <see cref="EsBitmap"/> を解決する（<see cref="Bitmap"/> は複製、<see cref="EsBitmapAvaloniaImage"/> は共有参照）。<br/>
    /// 3. 描画（切り出し・拡大・十字線・市松・外枠）は UI 非依存のレンダラへ委譲し、UI スレッドで行う<br/>
    /// （切り出しの可否と破棄済みソースの扱いはコアが単一所有する）。
    /// </remarks>
    public static async Task<EsBitmap> RenderAsync(
        LoupeFacade loupe, IImage? source, LoupeRenderRequest request, LoupeDrawContent content)
    {
        // 未対応型の判定は LoupeSourceResolver（単一所有）へ委譲する。
        if (!LoupeSourceResolver.IsSourceSupported(source))
        {
            throw new NotSupportedException(
                $"LoupeControl は {source!.GetType().FullName} をサポートしていません。Bitmap または EsBitmapAvaloniaImage を指定してください。");
        }

        // 抽出元の EsBitmap を解決する（Bitmap は複製、EsBitmapAvaloniaImage は共有参照。破棄済みは null）
        var resolved = LoupeSourceResolver.Resolve(source, out var sourceBitmap);

        try
        {
            // 切り出し（EsBitmap.Crop = Buffer.MemoryCopy のみで軽量）と合成（RenderTargetBitmap / DrawingContext）は UI スレッドで行う。
            // Task.Run によるバックグラウンド抽出は、マウス移動中の連続再描画でスレッドプールが競合し、
            // 抽出結果が UI スレッドへ戻る前に次の再描画が要求されてルーペ画像が移動停止まで更新されない問題を生む。
            return await Dispatcher.UIThread.InvokeAsync(
                () => Rasterize(loupe, sourceBitmap, request, content),
                DispatcherPriority.Render);
        }
        finally
        {
            // Bitmap 由来の場合のみ複製を破棄する（EsBitmapAvaloniaImage の共有参照は破棄しない）。
            if (resolved.IsSourceBitmap)
            {
                sourceBitmap!.Dispose();
            }
        }
    }

    /// <summary>描画の手順を描画実装へ委譲し、結果を <see cref="EsBitmap"/> として返す。</summary>
    /// <param name="loupe">ルーペのファサード（描画実装を保持する。描画の起動は本型の入口 `Draw` を使う）。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・拡大中心）。</param>
    /// <param name="content">コアが解決した描画内容（層の順序・矩形を持つ）。</param>
    /// <returns>描画結果（ホストがピクセルへオーバーレイを描画できる）。</returns>
    /// <remarks>
    /// オフスクリーン ビットマップは UI 固有の機構（Avalonia の <see cref="RenderTargetBitmap"/>）であるため本層が生成し、<br/>
    /// 描画面の適合（<see cref="AvaloniaCanvas"/>）を介して描画の手順（層の順序・写し方）を描画実装へ委譲する。
    /// </remarks>
    private static EsBitmap Rasterize(
        LoupeFacade loupe, EsBitmap? source, in LoupeRenderRequest request, in LoupeDrawContent content)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(request.PixelSize, request.PixelSize), s_dpi);

        using (var context = bitmap.CreateDrawingContext())
        {
            // 本 UI の描画実装なら描画面を適合させて描く（層の順序と起動はコアが単一所有する）
            if (loupe.Renderer is LoupeRendererAvaloniaUI uiRenderer)
            {
                uiRenderer.Draw(context, source, in request, in content);
            }
            else
            {
                // 描画実装が差し替えられている場合はコアの入口から描く
                loupe.Draw(new AvaloniaCanvas(context), source, in request, in content);
            }
        }

        return bitmap.ToEsBitmap();
    }
}

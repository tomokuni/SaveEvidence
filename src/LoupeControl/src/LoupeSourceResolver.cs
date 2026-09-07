using Avalonia.Media;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Platform.Imaging;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペのソース画像（<see cref="Bitmap"/> / <see cref="PbBitmapAvaloniaImage"/>）から切り出し元の解決ロジック（純粋関数）。</summary>
/// <remarks>
/// <see cref="LoupeControl.RenderBitmapAsync"/> のソース解決（型判定・破棄済み判定・<span>PbBitmap</span> 取得）を<br/>
/// UI 非依存の純粋ロジックとして提供する。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeSourceResolver
{
    /// <summary>ソース解決の結果。</summary>
    /// <param name="bitmap">切り出し元の PbBitmap。未対応・破棄済み・null の場合は null。</param>
    /// <param name="isSourceBitmap">ソースが <see cref="Bitmap"/> 由来か（複製破棄が必要か）。false の場合は共有参照（破棄しない）。</param>
    public readonly record struct Result(PbBitmap? Bitmap, bool IsSourceBitmap);

    /// <summary>ソース画像から切り出し元の PbBitmap を解決する。</summary>
    /// <param name="source">ルーペのソース画像（<see cref="Bitmap"/> / <see cref="PbBitmapAvaloniaImage"/>）。</param>
    /// <param name="sourceBitmap">解決した切り出し元。未対応・破棄済み・null の場合は null。</param>
    /// <returns>破棄済み・未対応型の場合は false（市松模様のみで描画する）。それ以外は true。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> は複製（<c>ToPbBitmap()</c>）を返し、破棄は呼び出し側が行う（<see cref="Result.IsSourceBitmap"/> = true）。<br/>
    /// <see cref="PbBitmapAvaloniaImage"/> は共有参照を返し、破棄しない（<see cref="Result.IsSourceBitmap"/> = false）。<br/>
    /// 破棄済み <see cref="Bitmap"/> は <c>PixelSize</c> 参照で検出する（<see cref="ObjectDisposedException"/> 防止）。
    /// </remarks>
    public static Result Resolve(IImage? source, out PbBitmap? sourceBitmap)
    {
        sourceBitmap = null;
        if (source is null)
        {
            return new Result(null, IsSourceBitmap: false);
        }

        if (source is Bitmap sourceBitmapImage)
        {
            try
            {
                _ = sourceBitmapImage.PixelSize;
            }
            catch (ObjectDisposedException)
            {
                // 破棄済み Source: 市松模様のみで描画する（前回のビットマップは維持）
                return new Result(null, IsSourceBitmap: false);
            }

            sourceBitmap = sourceBitmapImage.ToPbBitmap();
            return new Result(sourceBitmap, IsSourceBitmap: true);
        }

        if (source is PbBitmapAvaloniaImage unifiedImage)
        {
            sourceBitmap = unifiedImage.Bitmap;
            return new Result(sourceBitmap, IsSourceBitmap: false);
        }

        // 未対応型（Bitmap / PbBitmapAvaloniaImage 以外）は市松模様のみ
        return new Result(null, IsSourceBitmap: false);
    }
}

using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImage = System.Drawing.Image;

namespace EsUtil.SaveEvidence.App.WinForms.Services;

/// <summary>クリップボードとの画像の受け渡し（WinForms / GDI+）。</summary>
/// <remarks>
/// <b>UI 依存の境界</b>: <c>App.Core</c> の <c>MainViewModel</c> はコールバック（<c>Func</c>）として<br/>
/// クリップボード操作を受け取る（UI 型へ依存しないため）。 実体は本クラスが担う。<br/>
/// <b>所有権</b>: コピーは借用（引数の画像を破棄しない）。 貼り付けは<b>新しい画像を作って</b>返し、<br/>
/// 所有権を呼び出し側へ移す。<br/>
/// <b>UI スレッド</b>: クリップボード（OLE）は STA のスレッドを要求するため、UI スレッドから呼ぶ。
/// </remarks>
internal static class ClipboardImageService
{
    /// <summary>画像をクリップボードへコピーする。</summary>
    /// <param name="image">コピーする画像（借用。 破棄しない）。</param>
    /// <returns>コピーできた場合は true。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> が null の場合。</exception>
    /// <remarks>
    /// GDI+ のビットマップへ複製してから渡し、<c>copy: true</c>（OLE へ描き出す）で<br/>
    /// <b>呼び出し側が複製を破棄してもクリップボードの内容が残る</b>ようにする。
    /// </remarks>
    public static Task<bool> CopyAsync(EsReadOnlyBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var bitmap = GdiBitmapInterop.ToDrawingBitmap(image);
        Clipboard.SetDataObject(bitmap, copy: true);
        return Task.FromResult(true);
    }

    /// <summary>クリップボードの画像を書き換え可能な画像として取り出す。</summary>
    /// <returns>貼り付けた画像（所有権は呼び出し側へ移る）。 画像が無い場合は null。</returns>
    /// <remarks>画像以外（テキスト等）が入っている場合は null を返す（貼り付けを無効にする）。</remarks>
    public static Task<EsBitmap?> PasteAsync()
    {
        if (!Clipboard.ContainsImage())
        {
            return Task.FromResult<EsBitmap?>(null);
        }

        using DrawingImage? image = Clipboard.GetImage();
        if (image is null)
        {
            return Task.FromResult<EsBitmap?>(null);
        }

        // クリップボードが返す画像は形式が一定でないため、複製してから既定の形式へ揃える
        using var bitmap = new DrawingBitmap(image);
        return Task.FromResult<EsBitmap?>(GdiBitmapInterop.ToEsBitmap(bitmap));
    }
}

using SkiaSharp;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Skia;

/// <summary>コアの <see cref="EsBitmap"/> と Skia の <see cref="SKBitmap"/> を相互変換する拡張メソッド。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: Skia は UI フレームワークではないが<b>外部パッケージ</b>であるため、<br/>
/// 「外部パッケージへ依存しない」を判定基準とする <c>Platform.Core</c> には置けない。<br/>
/// Skia への依存は本層へ閉じ込め、コアは自前のピクセルバッファだけを所有する。<br/>
/// <br/>
/// <b>所有権</b>: 双方向とも<b>コアのバッファの所有権は移動しない</b>。<br/>
/// - <see cref="ToSKBitmap"/> は<b>非所有ビュー</b>を返す（コアのバッファを指すだけ。破棄してもコアのバッファは解放されない）。<br/>
/// - <see cref="ToEsBitmap(SKBitmap)"/> は<b>複製</b>を作る（引数の <see cref="SKBitmap"/> の所有権は呼び出し側に残る）。<br/>
/// <br/>
/// <b>ビューの寿命</b>: <see cref="ToSKBitmap"/> が返す <see cref="SKBitmap"/> は、対応する <see cref="EsBitmap"/> が<br/>
/// 生存している間だけ有効である（コアのバッファを指すため）。<see cref="EsBitmap"/> を破棄した後にビューを使うと<br/>
/// 解放済みメモリを読む。
/// </remarks>
public static class EsBitmapSkiaExtensions
{
    /// <summary>コアのビットマップを指す非所有ビューを返す。</summary>
    /// <param name="bitmap">対象のビットマップ。null 不可。</param>
    /// <returns>同じピクセルバッファを指す <see cref="SKBitmap"/>（非所有）。破棄してもコアのバッファは解放されない。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="bitmap"/> が破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">ビューの作成に失敗した場合。</exception>
    /// <remarks>
    /// ビューは <see cref="EsBitmap"/> の生存中のみ有効である（ピクセルをコピーしないため）。<br/>
    /// コピーが必要な場合は本メソッドの結果へ <c>Copy()</c> を適用する。
    /// </remarks>
    public static SKBitmap ToSKBitmap(this EsBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        bitmap.ThrowIfDisposed();

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var view = new SKBitmap();

        // コアのバッファをそのまま指す（ピクセルをコピーしない非所有ビュー）
        if (!view.InstallPixels(info, bitmap.Address, bitmap.Stride))
        {
            view.Dispose();
            throw new InvalidOperationException("SKBitmap のビューを作成できませんでした（ピクセル形式または寸法が不正です）。");
        }

        return view;
    }

    /// <summary>Skia のビットマップを複製してコアのビットマップを生成する。</summary>
    /// <param name="bitmap">元のビットマップ（BGRA8888・アルファ乗算済み）。null 不可。</param>
    /// <returns>複製されたコアのビットマップ（所有権は呼び出し側）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <remarks>
    /// 引数の所有権は呼び出し側に残る（本メソッドは引数を破棄しない）。不要になった時点で呼び出し側が破棄する。<br/>
    /// 元のビットマップが BGRA8888・アルファ乗算済みでない場合は、Skia が変換せずにバイト列を写すため<br/>
    /// 成分順が一致しない（呼び出し側で形式を保証する）。
    /// </remarks>
    public static EsBitmap ToEsBitmap(this SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var result = EsBitmap.Create(bitmap.Width, bitmap.Height);

        unsafe
        {
            var srcStride = bitmap.RowBytes;
            var dstStride = result.Stride;
            var rowBytes = Math.Min(srcStride, dstStride);

            var src = (byte*)bitmap.GetPixels();
            var dst = (byte*)result.Address;

            if (src is null)
            {
                return result;
            }

            for (var y = 0; y < bitmap.Height; y++)
            {
                Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
            }
        }

        return result;
    }
}

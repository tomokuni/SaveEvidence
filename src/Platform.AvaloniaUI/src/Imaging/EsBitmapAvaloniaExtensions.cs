using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Imaging;
using AvaloniaColor = Avalonia.Media.Color;
using Color = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

/// <summary>
/// <see cref="EsBitmap"/> の Avalonia 向け生成・変換拡張。
/// </summary>
/// <remarks>
/// <b>依存分離</b>: 本クラスは Avalonia との変換を担い、
/// <see cref="EsBitmap"/> 本体（コア）の依存を閉じる。<br/>
/// <see cref="EsBitmap"/> はピクセル操作と <c>Create</c> / <c>FromSKBitmap</c> / <c>ToSKBitmap</c>（SkiaSharp のみに依存）を提供し、
/// 外部ビットマップ種別（Avalonia）の生成・変換は本クラスへ分離する。<br/>
/// System.Drawing 向けの生成・変換は <see cref="EsBitmapDrawingExtensions"/> が担う。<br/>
/// <br/>
/// <b>C# 拡張メソッド</b>: ファイル種別からの生成（<c>bitmap.ToEsBitmap()</c>）とインスタンス変換（<c>bmp.ToAvaloniaWriteableBitmap()</c> 等）は
/// <c>this EsBitmap</c> / <c>this Bitmap</c> の拡張メソッドとして実装する。<br/>
/// <br/>
/// <b>所有権</b>: <c>ToAvaloniaWriteableBitmap()</c> は複製を返す（呼び出し側が所有）。 <c>CreateAvaloniaImage</c> は
/// 生成した画像を内包する <see cref="EsImageSourceAdapter"/>（非所有）を返す。
/// </remarks>
public static class EsBitmapAvaloniaExtensions
{

    // ====================================================================
    // 生成・変換 (Avalonia)
    // ====================================================================

    /// <summary>単色の Avalonia WriteableBitmap を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 正の値。</param>
    /// <param name="height">高さ（ピクセル）。 正の値。</param>
    /// <param name="color">塗りつぶし色（Avalonia Color。 アルファ乗算して書き込む）。</param>
    /// <returns>単色の <see cref="WriteableBitmap"/>（所有権は呼び出し側）。</returns>
    /// <remarks>
    /// テスト・サンプルで重複していた <c>new WriteableBitmap(...)</c> + 色塗りループを一元化する。<br/>
    /// <see cref="EsBitmap.Create(int, int, System.Drawing.Color)"/> + <see cref="ToAvaloniaWriteableBitmap"/> で構築する。
    /// 生成に Skia（Avalonia ヘッドレス）が必要。<br/>
    /// <b>命名の理由</b>: <see cref="EsBitmap"/> の <c>Create</c>（<see cref="EsBitmap"/> を返すコア）と区別するため、
    /// 表示境界で <see cref="WriteableBitmap"/> を返す本メソッドは <c>CreateAvaloniaWriteableBitmap</c> とする。
    /// </remarks>
    public static WriteableBitmap CreateAvaloniaWriteableBitmap(int width, int height, AvaloniaColor color)
    {
        // Avalonia Color を System.Drawing.Color へ変換して EsBitmap.Create（コア）へ委譲する。
        var colorArgb = Color.FromArgb(color.A, color.R, color.G, color.B);
        using var bitmap = EsBitmap.Create(width, height, colorArgb);
        return bitmap.ToAvaloniaWriteableBitmap();
    }

    /// <summary>単色の Avalonia 画像（<see cref="IImage"/>）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 正の値。</param>
    /// <param name="height">高さ（ピクセル）。 正の値。</param>
    /// <param name="color">塗りつぶし色（Avalonia Color。 アルファ乗算して書き込む）。</param>
    /// <returns>単色の画像を所有する <see cref="EsImageSourceAdapter"/>（<b>破棄は呼び出し側</b>。 画像も併せて解放される）。</returns>
    /// <remarks>
    /// テスト・サンプルで多用する単色画像を、表示境界の <see cref="IImage"/> として簡便に生成する。<br/>
    /// 画像はコアの <see cref="EsBitmap"/> で作り、アダプタが所有する（<see cref="EsImageSourceAdapter.Adopt"/>。
    /// Avalonia の描画バックエンドを必要としないため、初期化前のテストでも使える）。<br/>
    /// 表示用の変換は<b>初回の描画で 1 回だけ</b>行う（ピクセルの複製は 1 枚につき 1 回）。
    /// </remarks>
    public static EsImageSourceAdapter CreateAvaloniaImage(int width, int height, AvaloniaColor color)
    {
        var colorArgb = Color.FromArgb(color.A, color.R, color.G, color.B);
        // 新しく生成した画像を解放する人がいないため、所有する入口（Adopt）で包む
        return EsImageSourceAdapter.Adopt(EsBitmap.Create(width, height, colorArgb));
    }

    /// <summary>Avalonia Bitmap からラッパーを生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の Avalonia Bitmap。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>
    /// <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）で、SKBitmap のピクセルバッファへ直接コピーする。<br/>
    /// 中間バッファ（byte[]）を介さないため高速・低アロケーション。 PNG エンコード/デコードや System.Drawing を経由しない（可逆）。
    /// </remarks>
    public static unsafe EsBitmap ToEsBitmap(this Bitmap bitmap)
    {
        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;
        // CopyPixels は矩形の全行を埋めるため、0 埋めは行わない
        var result = EsBitmap.CreateUninitialized(w, h);
        var dstStride = result.Stride;
        var dst = (byte*)result.Address;

        // CopyPixels は宛先バッファを stride のピッチで埋める。 SKBitmap のピクセルバッファを直接宛先にし、中間 byte[] を排除する。
        bitmap.CopyPixels(
            new PixelRect(0, 0, w, h),
            (nint)dst,
            checked(dstStride * h),
            dstStride);

        return result;
    }

    /// <summary>Avalonia WriteableBitmap を返す（複製を所有するため安全に使用できる）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>変換された Avalonia WriteableBitmap。</returns>
    /// <remarks>
    /// 内部 SKBitmap を複製して Avalonia に渡すため、返された Bitmap の破棄は呼び出し側が所有する。<br/>
    /// ゼロコピーで受け渡したい場合は <c>ToSKBitmap</c> を使用すること。
    /// </remarks>
    public static unsafe WriteableBitmap ToAvaloniaWriteableBitmap(this EsBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        bitmap.ThrowIfDisposed();
        // Width / Height を一度だけ取得し、PixelSize 生成と行ループの両方で共有する（Size 値生成を削減）
        var size = bitmap.Size;
        var height = size.Height;
        var pixelSize = new PixelSize(size.Width, height);
        var vector = new Vector(96, 96);
        var wb = new WriteableBitmap(pixelSize, vector, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = wb.Lock();
        var dstStride = fb.RowBytes;
        var srcStride = bitmap.Stride;
        var rowBytes = Math.Min(srcStride, dstStride);
        var src = (byte*)bitmap.Address;
        var dst = (byte*)fb.Address;
        if (srcStride == dstStride)
        {
            // ストライドが同一の場合は連続領域なので、行単位ではなく全領域を 1 回でコピーする（高速化）
            var totalBytes = rowBytes * height;
            Buffer.MemoryCopy(src, dst, totalBytes, totalBytes);
        }
        else
        {
            for (var y = 0; y < height; y++)
            {
                Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
            }
        }
        return wb;
    }

    /// <summary>公開コンストラクタ（<c>new Bitmap(PixelFormat, AlphaFormat, Address, PixelSize, dpi, Stride)</c>）で Avalonia の標準 <see cref="Bitmap"/> を生成する。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>標準描画経路（<c>IImage</c>）で確実に描画される <see cref="Bitmap"/>。 所有権は呼び出し側。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> の公開コンストラクタへ <see cref="EsBitmap.Address"/>（ピクセルバッファ）を直接渡して生成する。<br/>
    /// Skia の内部実装（<c>ImmutableBitmap</c>）がピクセルを複製するためコピーは発生するが、<br/>
    /// <c>WriteableBitmap</c> の <c>Lock()</c> オーバーヘッドがなく、<c>Bitmap</c> として標準経路で確実に描画される。<br/>
    /// <b>注意</b>: 変換時に <see cref="EsBitmap.Address"/> のピクセルを<b>複製する</b>ため、その後 <see cref="EsBitmap"/> を
    /// 書き換えても本 <see cref="Bitmap"/> には反映されない（スナップショット）。
    /// </remarks>
    public static Bitmap ToAvaloniaBitmap(this EsBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        bitmap.ThrowIfDisposed();
        var size = bitmap.Size;
        return new Bitmap(
            PixelFormat.Bgra8888,
            AlphaFormat.Premul,
            bitmap.Address,
            new PixelSize(size.Width, size.Height),
            new Vector(96, 96),
            bitmap.Stride);
    }

    /// <summary>指定座標（Avalonia PixelPoint）へ Avalonia Color を書き込む。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する Avalonia Color。 アルファ乗算して書き込む。</param>
    public static void SetPixel(this EsBitmap bitmap, PixelPoint point, AvaloniaColor color)
        => bitmap.SetPixel(new EsPointInt(point.X, point.Y), Color.FromArgb(color.A, color.R, color.G, color.B));
}

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Native.Imaging;
using SkiaSharp;
using Color = System.Drawing.Color;
using AvaloniaColor = Avalonia.Media.Color;

namespace EsUtil.Platform.AvaloniaUI.Imaging;

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
/// <b>所有権</b>: <c>ToAvaloniaWriteableBitmap()</c> / <c>ToAvaloniaImage()</c> は複製またはラップを返す（呼び出し側が所有）。
/// </remarks>
public static class EsBitmapAvaloniaExtensions
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    private const int BytesPerPixel = 4;

    // ====================================================================
    // 生成・変換 (Avalonia)
    // ====================================================================

    /// <summary>単色の Avalonia WriteableBitmap を生成する。</summary>
    /// <param name="width">幅（ピクセル）。正の値。</param>
    /// <param name="height">高さ（ピクセル）。正の値。</param>
    /// <param name="color">塗りつぶし色（Avalonia Color。アルファ乗算して書き込む）。</param>
    /// <returns>単色の <see cref="WriteableBitmap"/>（所有権は呼び出し側）。</returns>
    /// <remarks>
    /// 各テスト・サンプルで重複していた <c>new WriteableBitmap(...)</c> + 色塗りループを一元化する。<br/>
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

    /// <summary>単色の <see cref="EsBitmapAvaloniaImage"/> を生成する（Avalonia 表示境界の <see cref="IImage"/>）。</summary>
    /// <param name="width">幅（ピクセル）。正の値。</param>
    /// <param name="height">高さ（ピクセル）。正の値。</param>
    /// <param name="color">塗りつぶし色（Avalonia Color。アルファ乗算して書き込む）。</param>
    /// <returns>単色の <see cref="EsBitmapAvaloniaImage"/>（破棄は呼び出し側）。</returns>
    /// <remarks>
    /// <c>WriteableBitmap</c> を生成せず、<see cref="EsBitmap"/> を内包する <see cref="EsBitmapAvaloniaImage"/> を返す<br/>
    /// （テスト・サンプルで多用する単色画像を、表示境界の <see cref="IImage"/> として簡便に生成する）。<br/>
    /// <see cref="CreateAvaloniaWriteableBitmap"/>（<see cref="WriteableBitmap"/> を返す）とは異なり、<br/>
    /// Skia 直アクセス（ゼロコピー）で描画されるため画像生成が高速。
    /// </remarks>
    public static EsBitmapAvaloniaImage CreateAvaloniaImage(int width, int height, AvaloniaColor color)
    {
        var colorArgb = Color.FromArgb(color.A, color.R, color.G, color.B);
        var bitmap = EsBitmap.Create(width, height, colorArgb);
        return new EsBitmapAvaloniaImage(bitmap);
    }

    /// <summary>Avalonia Bitmap からラッパーを生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の Avalonia Bitmap。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>
    /// <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）で、SKBitmap のピクセルバッファへ直接コピーする。<br/>
    /// 中間バッファ（byte[]）を介さないため高速・低アロケーション。PNG エンコード/デコードや System.Drawing を経由しない（可逆）。
    /// </remarks>
    public static unsafe EsBitmap ToEsBitmap(this Bitmap bitmap)
    {
        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;
        var result = EsBitmap.Create(w, h);
        var dstStride = result.Stride;
        var dst = (byte*)result.Address;

        // CopyPixels は宛先バッファを stride のピッチで埋める。SKBitmap のピクセルバッファを直接宛先にし、中間 byte[] を排除する。
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
    /// ゼロコピーで受け渡したい場合は <see cref="ToSKBitmap(EsBitmap)"/> を使用すること。
    /// </remarks>
    public static unsafe WriteableBitmap ToAvaloniaWriteableBitmap(this EsBitmap bitmap)
    {
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
    /// <returns>標準描画経路（<c>IImage</c>）で確実に描画される <see cref="Bitmap"/>。所有権は呼び出し側。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> の公開コンストラクタへ <see cref="EsBitmap.Address"/>（ピクセルバッファ）を直接渡して生成する。<br/>
    /// Skia の内部実装（<c>ImmutableBitmap</c>）がピクセルを複製するためコピーは発生するが、<br/>
    /// <c>WriteableBitmap</c> の <c>Lock()</c> オーバーヘッドがなく、<c>Bitmap</c> として標準経路で確実に描画される。<br/>
    /// <b>注意</b>: 変換時に <see cref="EsBitmap.Address"/> のピクセルを<b>複製する</b>ため、その後 <see cref="EsBitmap"/> を
    /// 書き換えても本 <see cref="Bitmap"/> には反映されない（スナップショット）。
    /// </remarks>
    public static Bitmap ToAvaloniaBitmap(this EsBitmap bitmap)
    {
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

    /// <summary>Avalonia の画像入力（<see cref="IImage"/>）として公開するアダプタを生成する（ゼロコピー・複製なし）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns><see cref="EsBitmapAvaloniaImage"/>（所有権は <see cref="EsBitmapAvaloniaImage.Dispose"/> で解放する）。</returns>
    /// <remarks>
    /// <c>WriteableBitmap</c> / <c>Bitmap</c> を生成せず、内部 SKBitmap を Skia へ直接描画する（ゼロコピー）。<br/>
    /// 内包する <see cref="EsBitmap"/> の所有権は生成した <see cref="EsBitmapAvaloniaImage"/> へ移るため、
    /// 呼び出し側は元の <paramref name="bitmap"/> を破棄しないこと（破棄漏れ・二重破棄防止）。
    /// </remarks>
    public static IImage ToAvaloniaImage(this EsBitmap bitmap)
        => new EsBitmapAvaloniaImage(bitmap);

    /// <summary>画像サイズ（Avalonia PixelSize）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>Avalonia PixelSize。</returns>
    public static PixelSize Size(this EsBitmap bitmap)
        => new(bitmap.Size.Width, bitmap.Size.Height);

    /// <summary>Avalonia PixelRect で指定された矩形領域を切り出す。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="rect">切り出し領域（画像ローカルピクセル）。</param>
    /// <returns>切り出された新しいビットマップ（要求サイズ。ソース範囲外は透明パディング）。</returns>
    /// <remarks>
    /// コアの <c>Crop(int, int, int, int)</c> へ委譲する。<br/>
    /// 幅・高さが 0 以下の場合は <see cref="ArgumentOutOfRangeException"/>。
    /// </remarks>
    public static EsBitmap Crop(this EsBitmap bitmap, PixelRect rect)
        => bitmap.Crop(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>指定座標（Avalonia PixelPoint）のピクセルを Avalonia Color で取得する。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="point">座標（ピクセル）。</param>
    /// <returns>取得した Avalonia Color。範囲外は IndexOutOfRangeException。</returns>
    public static AvaloniaColor GetPixel(this EsBitmap bitmap, PixelPoint point)
    {
        var color = bitmap.GetPixel(new EsPointInt(point.X, point.Y));
        return AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B);
    }

    /// <summary>指定座標（Avalonia PixelPoint）へ Avalonia Color を書き込む。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する Avalonia Color。アルファ乗算して書き込む。</param>
    public static void SetPixel(this EsBitmap bitmap, PixelPoint point, AvaloniaColor color)
        => bitmap.SetPixel(new EsPointInt(point.X, point.Y), Color.FromArgb(color.A, color.R, color.G, color.B));

    /// <summary>指定座標（ピクセル）へ Avalonia Color を書き込む（範囲外は無視）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="color">設定する Avalonia Color。</param>
    /// <returns>書き込めたかどうか。範囲外の場合は false。</returns>
    /// <remarks>
    /// <see cref="EsBitmap.TrySetPixel"/> へ委譲する（Avalonia Color → System.Drawing Color へ変換）。<br/>
    /// 範囲外チェックはコア側で行う。
    /// </remarks>
    public static bool TrySetPixel(this EsBitmap bitmap, int x, int y, AvaloniaColor color)
        => bitmap.TrySetPixel(x, y, color.ToDrawingColor());

    /// <summary>全ピクセルを指定色（Avalonia Color）で塗りつぶす。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="color">塗りつぶし色。アルファ乗算して書き込む。</param>
    public static void Clear(this EsBitmap bitmap, AvaloniaColor color)
    {
        var premult = Premultiply(color);
        // EsBitmap.Clear(byte b, byte g, byte r, byte a) は BGRA 順のため、SKColor(RGB) から BGRA へ並べ替えて委譲する。
        bitmap.Clear(premult.Blue, premult.Green, premult.Red, premult.Alpha);
    }

    // 内部ヘルパー

    /// <summary>Avalonia Color をアルファ乗算済み SKColor へ変換する。</summary>
    /// <param name="color">変換対象の色。</param>
    /// <returns>アルファ乗算済みの SKColor。</returns>
    private static SKColor Premultiply(AvaloniaColor color)
    {
        var a = color.A;
        if (a == 255) return new SKColor(color.R, color.G, color.B, a);
        return new SKColor(
            (byte)(color.R * a / 255),
            (byte)(color.G * a / 255),
            (byte)(color.B * a / 255),
            a);
    }
}

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EsUtil.Platform.Core.Imaging;
using AvaloniaColor = Avalonia.Media.Color;
using Color = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

/// <summary>
/// <see cref="EsBitmap"/> / <see cref="EsWritableBitmap"/> の Avalonia 向け生成・変換拡張。
/// </summary>
/// <remarks>
/// <b>依存分離</b>: 本クラスは Avalonia との変換を担い、<see cref="EsBitmap"/> 本体（コア）の依存を閉じる。<br/>
/// System.Drawing 向けの生成・変換は <see cref="EsUtil.Platform.Core.Native.Imaging.EsBitmapDrawingExtensions"/> が担う。<br/>
/// <br/>
/// <b>権利の向き</b>: 生成（<c>ToEsBitmap</c> / <c>CreateAvaloniaWriteableBitmap</c>）は書き換え可能な画像を作る。
/// 変換（<c>ToAvaloniaBitmap</c> / <c>ToAvaloniaWriteableBitmap</c>）は読み取り専用の面（<see cref="IEsBitmap"/>）を受け取る
/// （複製を返すため入力の書き換え権限は要らない）。<br/>
/// <b>所有権</b>: 返された Avalonia の画像は呼び出し側が所有する（破棄する）。
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
    /// <see cref="EsWritableBitmap.Create(int, int, Color)"/> + <see cref="ToAvaloniaWriteableBitmap"/> で構築する。
    /// 生成に Skia（Avalonia ヘッドレス）が必要。<br/>
    /// <b>命名の理由</b>: コアの <c>Create</c>（<see cref="EsWritableBitmap"/> を返す）と区別するため、
    /// 表示境界で <see cref="WriteableBitmap"/> を返す本メソッドは <c>CreateAvaloniaWriteableBitmap</c> とする。
    /// </remarks>
    public static WriteableBitmap CreateAvaloniaWriteableBitmap(int width, int height, AvaloniaColor color)
    {
        // Avalonia Color を System.Drawing.Color へ変換してコアの生成へ委譲する
        var colorArgb = Color.FromArgb(color.A, color.R, color.G, color.B);
        using var bitmap = EsWritableBitmap.Create(width, height, colorArgb);
        return bitmap.ToAvaloniaWriteableBitmap();
    }

    /// <summary>単色の Avalonia 画像（表示境界の <see cref="IImage"/>）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 正の値。</param>
    /// <param name="height">高さ（ピクセル）。 正の値。</param>
    /// <param name="color">塗りつぶし色（Avalonia Color。 アルファ乗算して書き込む）。</param>
    /// <returns>単色の画像を包むアダプタ（<b>破棄は呼び出し側</b>）。</returns>
    /// <remarks>
    /// テスト・サンプルで多用する単色画像を、表示境界の <see cref="IImage"/> として簡便に生成する。<br/>
    /// 画像はコアの <see cref="EsWritableBitmap"/> で作り、アダプタが所有する（<see cref="EsBitmapAdapter.Adopt"/>）。<br/>
    /// 表示用の変換は<b>初回の描画で 1 回だけ</b>行う（ピクセルの複製は 1 枚につき 1 回）。
    /// </remarks>
    public static EsBitmapAdapter CreateAvaloniaImage(int width, int height, AvaloniaColor color)
    {
        var colorArgb = Color.FromArgb(color.A, color.R, color.G, color.B);
        // 新しく生成した画像を解放する人がいないため、所有する入口（Adopt）で包む
        return EsBitmapAdapter.Adopt(EsWritableBitmap.Create(width, height, colorArgb));
    }

    /// <summary>Avalonia Bitmap からラッパーを生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の Avalonia Bitmap。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）で、コアのピクセル バッファへ直接コピーする。<br/>
    /// 中間バッファ（byte[]）を介さないため高速・低アロケーション。 PNG エンコード/デコードや System.Drawing を経由しない（可逆）。
    /// </remarks>
    public static unsafe EsWritableBitmap ToEsBitmap(this Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;
        // CopyPixels は矩形の全行を埋めるため、0 埋めは行わない
        var result = EsWritableBitmap.CreateUninitialized(w, h);
        var dstStride = result.Stride;

        fixed (byte* dst = result.WritablePixels)
        {
            // CopyPixels は宛先バッファを stride のピッチで埋める。 コアのバッファを直接宛先にし、中間 byte[] を排除する
            bitmap.CopyPixels(
                new PixelRect(0, 0, w, h),
                (nint)dst,
                checked(dstStride * h),
                dstStride);
        }

        return result;
    }

    /// <summary>Avalonia WriteableBitmap を返す（複製を所有するため安全に使用できる）。</summary>
    /// <param name="bitmap">対象の画像。 借用であり破棄しない。</param>
    /// <returns>変換された Avalonia WriteableBitmap。 所有権は呼び出し側。</returns>
    /// <remarks>
    /// コアのピクセルを複製して Avalonia へ渡すため、返された Bitmap の破棄は呼び出し側が所有する。
    /// </remarks>
    public static unsafe WriteableBitmap ToAvaloniaWriteableBitmap(this IEsBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        // 寸法を一度だけ取得し、PixelSize 生成と行ループの両方で共有する（値型の生成を削減）
        var size = bitmap.Size;
        var height = size.Height;
        var pixelSize = new PixelSize(size.Width, height);
        var vector = new Vector(96, 96);
        var wb = new WriteableBitmap(pixelSize, vector, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = wb.Lock();
        var dstStride = fb.RowBytes;
        var srcStride = bitmap.Stride;
        var rowBytes = Math.Min(srcStride, dstStride);
        fixed (byte* src = bitmap.Pixels)
        {
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
        }

        return wb;
    }

    /// <summary>公開コンストラクタで Avalonia の標準 <see cref="Bitmap"/> を生成する。</summary>
    /// <param name="bitmap">対象の画像。 借用であり破棄しない。</param>
    /// <returns>標準描画経路（<c>IImage</c>）で確実に描画される <see cref="Bitmap"/>。 所有権は呼び出し側。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> の公開コンストラクタへコアのピクセル バッファを直接渡して生成する。<br/>
    /// Skia の内部実装がピクセルを複製するためコピーは発生するが、<c>WriteableBitmap</c> の <c>Lock()</c> オーバーヘッドがなく、
    /// <c>Bitmap</c> として標準経路で確実に描画される。<br/>
    /// <b>注意</b>: 変換時にピクセルを<b>複製する</b>ため、その後コアの画像を書き換えても本 <see cref="Bitmap"/> には反映されない（スナップショット）。
    /// </remarks>
    public static unsafe Bitmap ToAvaloniaBitmap(this IEsBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var size = bitmap.Size;
        fixed (byte* src = bitmap.Pixels)
        {
            return new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Premul,
                (nint)src,
                new PixelSize(size.Width, size.Height),
                new Vector(96, 96),
                bitmap.Stride);
        }
    }

    /// <summary>指定座標（Avalonia PixelPoint）へ Avalonia Color を書き込む。</summary>
    /// <param name="bitmap">対象の画像（<b>書き換えるため <see cref="IEsWritableBitmap"/> を受ける</b>）。</param>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する Avalonia Color。 アルファ乗算して書き込む。</param>
    public static void SetPixel(this IEsWritableBitmap bitmap, PixelPoint point, AvaloniaColor color)
        => bitmap.SetPixel(point.X, point.Y, color.B, color.G, color.R, color.A);

    /// <summary>縮小した Avalonia の画像を生成する（<b>表示用</b>。 元の寸法が上限以内なら等倍の変換へ委譲する）。</summary>
    /// <param name="bitmap">対象の画像。 借用であり破棄しない。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 正の値。</param>
    /// <returns>縮小（または等倍）の画像。 所有権は呼び出し側。</returns>
    /// <remarks>
    /// <b>用途</b>: 大きな画像を小さく表示する場面（一覧のサムネイル等）。 <see cref="ToAvaloniaBitmap(IEsBitmap)"/> は
    /// 元の寸法で複製するため、4K の画像を 96px のサムネイルに使うと<b>32 MB の複製が表示のたびに残る</b>
    /// （実測: 4K の等倍は 32 MB / 7.8 ms、同じ 4K からの縮小版は 96x54 で 17 µs / 20 KB = 約 450 倍速・
    /// 約 1600 分の 1 のメモリ）。<br/>
    /// <b>画質</b>: <b>最近傍で標本化する</b>（縮小専用の高速経路）。 高品質な縮小が必要な場面（保存・印刷等）では
    /// 本メソッドを使わず、等倍で取得してから縮小すること。<br/>
    /// <b>縦横比</b>: 切り捨てで丸めるため、<b>1 px 未満の誤差が出うる</b>（座標の対応付けが必要な場面では使わない）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> が 0 以下の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="bitmap"/> が破棄済みの場合。</exception>
    public static unsafe Bitmap ToAvaloniaBitmap(this IEsBitmap bitmap, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "長辺の上限は正の値である必要があります。");
        }

        var size = bitmap.Size;
        if (size.Width <= maxSize && size.Height <= maxSize)
        {
            // 収まっている場合は縮小しない（等倍の変換経路へ委譲する）
            return bitmap.ToAvaloniaBitmap();
        }

        // 長辺を上限へ収める（切り捨てで上限を超えない）
        var scale = Math.Min((double)maxSize / size.Width, (double)maxSize / size.Height);
        var dstWidth = Math.Max(1, (int)(size.Width * scale));
        var dstHeight = Math.Max(1, (int)(size.Height * scale));

        // 中間のピクセル コンテナを作らず、書き込み先のバッファへ直接標本化する（確保は 1 回）
        var wb = new WriteableBitmap(new PixelSize(dstWidth, dstHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = wb.Lock();
        var dstStride = fb.RowBytes;
        var srcStride = bitmap.Stride;
        var bytesPerPixel = IEsBitmap.BytesPerPixel;

        // 列・行の対応（最近傍）を先に求め、内側のループでは掛け算をしない
        var srcWidth = size.Width;
        var srcHeight = size.Height;
        var colMap = new int[dstWidth];
        for (var x = 0; x < dstWidth; x++)
        {
            colMap[x] = Math.Min(srcWidth - 1, (int)((long)x * srcWidth / dstWidth));
        }

        fixed (byte* src = bitmap.Pixels)
        {
            var dst = (byte*)fb.Address;
            for (var y = 0; y < dstHeight; y++)
            {
                var srcRow = src + ((long)Math.Min(srcHeight - 1, (int)((long)y * srcHeight / dstHeight)) * srcStride);
                var dstRow = dst + ((long)y * dstStride);
                for (var x = 0; x < dstWidth; x++)
                {
                    var from = srcRow + (colMap[x] * bytesPerPixel);
                    var to = dstRow + (x * bytesPerPixel);
                    to[0] = from[0];
                    to[1] = from[1];
                    to[2] = from[2];
                    to[3] = from[3];
                }
            }
        }

        return wb;
    }
}

using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using Windows.Graphics.Imaging;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>画像ファイルを縮小してサムネイル用の画像（読み取りの面）として読み込むローダー。</summary>
/// <remarks>
/// <b>縮小デコード</b>: WIC（<see cref="BitmapDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat, BitmapAlphaMode, BitmapTransform, ExifOrientationMode, ColorManagementMode)"/>）の<br/>
/// 変換へ縮小後の寸法を渡し、原寸のバッファを確保せずにデコードする（4K のスクリーンショットを多数並べるときの<br/>
/// メモリと時間を抑える）。<br/>
/// <b>キャッシュ</b>: 同一ファイル・同一サイズのデコード結果を共有する。 ファイルの更新日時をキーに含めるため、<br/>
/// 内容が変われば再デコードされる。 容量管理は <see cref="CapacityLimitedCache"/> へ委譲し、<br/>
/// 上限超過時は <see cref="EsReadOnlyBitmap"/>（<see cref="IDisposable"/>）が破棄されてから破棄される。<br/>
/// <b>失敗時</b>: 存在しない・非画像・破損などの場合は null を返す（例外にしない）。<br/>
/// <b>スレッド</b>: 背景の生成からも呼べる（キャッシュの上限判定・登録は直列化される。 デコードは並列のまま）。<br/>
/// <b>WIC の API は非同期である</b>が、呼び出し側（一覧の背景生成）は逐次処理であるため、本クラスの公開面は同期のまま<br/>
/// 内部で 1 つのスレッドに閉じる（呼び出し側へ非同期の伝播を持ち込まない）。
/// </remarks>
internal static class ImageThumbnailLoader
{
    /// <summary>デコード結果のキャッシュ（キー: パス・要求サイズ・更新日時）。</summary>
    private static readonly ConcurrentDictionary<(string Path, int Size, DateTime LastWrite), EsReadOnlyBitmap?> s_cache = new();

    /// <summary>画像ファイルを読み込み、長辺が指定サイズを超えないサムネイルを返す。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">サムネイルの長辺の上限（ピクセル）。 0 以下は 1 として扱う。</param>
    /// <param name="capacity">キャッシュの最大保持数（一覧が表示する件数を渡す）。 1 未満は 1 として扱う。</param>
    /// <returns>サムネイル画像（読み取りの面）。 読み込めない場合は null。</returns>
    /// <remarks>
    /// <b>保持数を呼び出し側が渡す理由</b>: 一覧が表示する件数より小さい上限だと、生成の途中で表示中の<br/>
    /// サムネイルが破棄される（上限超過時はキャッシュが保持する値を破棄するため、描画が破棄済みの<br/>
    /// バッファへ触れる）。<br/>
    /// <b>キャッシュの値は本クラスが所有し、呼び出し側は破棄しない。</b>
    /// </remarks>
    public static EsReadOnlyBitmap? Load(string filePath, int maxSize, int capacity)
    {
        if (!ImageFileExtensions.IsImageFile(filePath))
        {
            return null;
        }

        var size = Math.Max(1, maxSize);

        try
        {
            var key = (Path: filePath, Size: size, LastWrite: File.GetLastWriteTimeUtc(filePath));
            return CapacityLimitedCache.GetOrAdd(s_cache, Math.Max(1, capacity), key, static k => Decode(k.Path, k.Size));
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // アクセス拒否・削除競合など、ファイルの状態による失敗は「表示できない」として扱う
            return null;
        }
    }

    /// <summary>画像ファイルを縮小してデコードする（WIC の非同期 API を 1 つのスレッドに閉じて呼ぶ）。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 以上。</param>
    /// <returns>デコードした画像。 デコードできない場合は null。</returns>
    /// <remarks>
    /// <b><c>Task.Run</c> で隔離する理由</b>: WIC の API は非同期であり、UI スレッドの同期コンテキストで待つと<br/>
    /// デッドロックする。 隔離したスレッドでは同期コンテキストが無いため、待機しても安全である。
    /// </remarks>
    private static EsReadOnlyBitmap? Decode(string filePath, int maxSize)
        => Task.Run(() => DecodeAsync(filePath, maxSize)).GetAwaiter().GetResult();

    /// <summary>画像ファイルを縮小してデコードする。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 以上。</param>
    /// <returns>デコードした画像。 デコードできない場合は null。</returns>
    /// <remarks>
    /// 1. 長辺が上限に収まる倍率を求める（上限以下なら等倍）。<br/>
    /// 2. WIC の変換でその寸法へ縮小しながらデコードする（原寸バッファを確保しない）。<br/>
    /// 3. 画素を内部表現（BGRA8888・Premul）のまま読み取りの面へ写す。
    /// </remarks>
    private static async Task<EsReadOnlyBitmap?> DecodeAsync(string filePath, int maxSize)
    {
        using var stream = File.OpenRead(filePath);
        using var randomAccessStream = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream).AsTask().ConfigureAwait(false);

        var width = (int)decoder.PixelWidth;
        var height = (int)decoder.PixelHeight;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var scale = Math.Min((double)maxSize / width, (double)maxSize / height);
        var (targetWidth, targetHeight) = scale < 1.0
            ? (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)))
            : (width, height);

        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)targetWidth,
            ScaledHeight = (uint)targetHeight,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);

        if (bitmap is null)
        {
            return null;
        }

        var stride = targetWidth * EsReadOnlyBitmap.BytesPerPixel;
        var pixels = new byte[stride * targetHeight];
        var buffer = new Windows.Storage.Streams.Buffer((uint)pixels.Length);
        bitmap.CopyToBuffer(buffer);
        buffer.ToArray().CopyTo(pixels, 0);

        return EsReadOnlyBitmap.CreateFilled(targetWidth, targetHeight, span =>
        {
            for (var y = 0; y < targetHeight; y++)
            {
                pixels.AsSpan(y * stride, stride).CopyTo(span[(y * stride)..]);
            }
        });
    }
}
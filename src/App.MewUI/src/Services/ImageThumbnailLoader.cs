using System.Collections.Concurrent;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using SkiaSharp;
using EsUtil.SaveEvidence.Platform.Skia;


namespace EsUtil.SaveEvidence.App.MewUI.Services;

/// <summary>画像ファイルを縮小してサムネイル用の <see cref="EsBitmap"/> として読み込むローダー。</summary>
/// <remarks>
/// <b>縮小デコード</b>: <see cref="SKCodec.GetPixels(SKImageInfo, out byte[])"/> へ縮小後の寸法を渡し、原寸のバッファを確保せずにデコードする<br/>
/// （4K のスクリーンショットを多数並べるときのメモリと時間を抑える）。<br/>
/// <b>キャッシュ</b>: 同一ファイル・同一サイズのデコード結果を共有する。 ファイルの更新日時をキーに含めるため、<br/>
/// 内容が変われば再デコードされる。 容量管理は <see cref="CapacityLimitedCache"/> へ委譲し、<br/>
/// 上限超過時は <see cref="EsBitmap"/>（<see cref="IDisposable"/>）が破棄されてから破棄される。<br/>
/// <b>失敗時</b>: 存在しない・非画像・破損などの場合は null を返す（例外にしない）。<br/>
/// <b>スレッド</b>: キャッシュは並行辞書のため同時アクセスできるが、UI からの呼び出しを前提とする。
/// </remarks>
internal static class ImageThumbnailLoader
{
    /// <summary>キャッシュの最大保持数。</summary>
    private const int CacheCapacity = 100;

    /// <summary>デコード結果のキャッシュ（キー: パス・要求サイズ・更新日時）。</summary>
    private static readonly ConcurrentDictionary<(string Path, int Size, DateTime LastWrite), EsBitmap?> s_cache = new();

    /// <summary>画像ファイルを読み込み、長辺が指定サイズを超えないサムネイルを返す。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">サムネイルの長辺の上限（ピクセル）。 0 以下は 1 として扱う。</param>
    /// <returns>サムネイル画像。 読み込めない場合は null。</returns>
    /// <remarks>
    /// 1. 拡張子が画像でなければ読み込まずに null を返す。<br/>
    /// 2. キャッシュにあればそれを返す（キャッシュの値は本クラスが所有し、呼び出し側は破棄しない）。<br/>
    /// 3. 無ければ縮小デコードしてキャッシュへ登録する。
    /// </remarks>
    public static EsBitmap? Load(string filePath, int maxSize)
    {
        if (!ImageFileExtensions.IsImageFile(filePath))
        {
            return null;
        }

        var size = Math.Max(1, maxSize);

        try
        {
            var key = (Path: filePath, Size: size, LastWrite: File.GetLastWriteTimeUtc(filePath));
            return CapacityLimitedCache.GetOrAdd(s_cache, CacheCapacity, key, static k => Decode(k.Path, k.Size));
        }
        catch (Exception)
        {
            // アクセス拒否・削除競合など、ファイルの状態による失敗は「表示できない」として扱う
            return null;
        }
    }

    /// <summary>画像ファイルを縮小してデコードする。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 以上。</param>
    /// <returns>デコードした画像。 デコードできない場合は null。</returns>
    /// <remarks>
    /// 1. 長辺が上限に収まる倍率を求める（上限以下なら等倍）。<br/>
    /// 2. コーデックが縮小デコードに対応していれば、その寸法でピクセルを取得する（原寸バッファを確保しない）。<br/>
    /// 3. 縮小されなかった場合は、デコード後に目標寸法へ縮小する。<br/>
    /// 4. 不完全なデータ（<c>IncompleteInput</c>）はデコードできた範囲を表示する。
    /// </remarks>
    private static EsBitmap? Decode(string filePath, int maxSize)
    {
        using var stream = File.OpenRead(filePath);
        using var codec = SKCodec.Create(stream);
        if (codec is null)
        {
            return null;
        }

        var source = codec.Info;
        if (source.Width <= 0 || source.Height <= 0)
        {
            return null;
        }

        // 長辺が maxSize に収まる倍率（上限以下なら等倍）
        var scale = Math.Min((double)maxSize / source.Width, (double)maxSize / source.Height);
        var target = scale < 1.0 ? ComputeTarget(source, scale) : new SKSizeI(source.Width, source.Height);

        // コーデックが対応していれば縮小した寸法でデコードする（対応可否は形式により異なる）
        var decodeSize = scale < 1.0 ? codec.GetScaledDimensions((float)scale) : target;

        // 内部表現（EsBitmap）と同じ BGRA8888・Premul を指定し、拡張子に依らず同じ形式へ揃える
        var bitmap = DecodePixels(codec, decodeSize);
        if (bitmap is null)
        {
            return null;
        }

        // コーデック側で縮小されなかった場合は、ここで目標寸法へ縮小する
        if (bitmap.Width != target.Width || bitmap.Height != target.Height)
        {
            var resized = Resize(bitmap, target);
            bitmap.Dispose();
            if (resized is null)
            {
                return null;
            }

            bitmap = resized;
        }

        return bitmap.ToEsBitmap();
    }

    /// <summary>縮小後に目標とする寸法を求める。</summary>
    /// <param name="source">元画像のピクセル情報。</param>
    /// <param name="scale">縮小倍率（1 未満）。</param>
    /// <returns>目標寸法（各辺 1 以上）。</returns>
    private static SKSizeI ComputeTarget(SKImageInfo source, double scale)
        => new(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)));

    /// <summary>指定寸法でピクセルを取得する。</summary>
    /// <param name="codec">対象のコーデック。</param>
    /// <param name="size">取得する寸法（1 以上）。</param>
    /// <returns>デコードしたビットマップ。 取得できない場合は null。</returns>
    private static SKBitmap? DecodePixels(SKCodec codec, SKSizeI size)
    {
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);

        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }

    /// <summary>ビットマップを指定寸法へ縮小する。</summary>
    /// <param name="source">元のビットマップ（破棄は呼び出し側）。</param>
    /// <param name="size">目標寸法（1 以上）。</param>
    /// <returns>縮小したビットマップ。 縮小できない場合は null。</returns>
    private static SKBitmap? Resize(SKBitmap source, SKSizeI size)
        => source.Resize(
            new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
}


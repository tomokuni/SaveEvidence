using System.Collections.Concurrent;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;

namespace EsUtil.SaveEvidence.App.MewUI.Services;

/// <summary>画像ファイルを縮小してサムネイル用の画像（読み取りの面）として読み込むローダー。</summary>
/// <remarks>
/// <b>デコードはコア（WIC）</b>: <see cref="EsReadOnlyBitmap.Load"/> が OS のコーデックで読み込み、
/// 縮小はコアの面積平均（<see cref="EsImageQuality.High"/>）へ委譲する。<br/>
/// <b>キャッシュ</b>: 同一ファイル・同一サイズのデコード結果を共有する。 ファイルの更新日時をキーに含めるため、<br/>
/// 内容が変われば再デコードされる。 容量管理は <see cref="CapacityLimitedCache"/> へ委譲し、<br/>
/// 上限超過時は <see cref="EsReadOnlyBitmap"/>（<see cref="IDisposable"/>）が破棄されてから破棄される<br/>
/// （そのため上限は呼び出し側が表示する件数を渡す。<see cref="Load"/> を参照）。<br/>
/// <b>失敗時</b>: 存在しない・非画像・破損などの場合は null を返す（例外にしない）。<br/>
/// <b>スレッド</b>: 背景の生成からも呼べる（キャッシュの上限判定・登録は直列化される。 デコードは並列のまま）。
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
    /// 1. 拡張子が画像でなければ読み込まずに null を返す。<br/>
    /// 2. キャッシュにあればそれを返す（キャッシュの値は本クラスが所有し、呼び出し側は破棄しない）。<br/>
    /// 3. 無ければデコードして縮小し、キャッシュへ登録する。<br/>
    /// <b>読み取りの面で返す理由</b>: サムネイルは表示するだけ（書き換えない）であるため、
    /// デコードの完了後に凍結して 0 コピーで読み取り専用の面へ渡す。<br/>
    /// <b>保持数を呼び出し側が渡す理由</b>: 一覧が表示する件数より小さい上限だと、生成の途中で表示中の
    /// サムネイルが破棄される（上限超過時はキャッシュが保持する値を破棄するため、描画が破棄済みの
    /// バッファへ触れる）。<br/>
    /// <b>複数スレッドから呼べる</b>（背景の生成から呼ぶ。 キャッシュの上限判定と登録は直列化される）。
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

    /// <summary>画像ファイルを縮小してデコードする。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 以上。</param>
    /// <returns>デコードした画像。 デコードできない場合は null。</returns>
    /// <remarks>
    /// 1. コア（WIC）で読み込む（形式は内容から判別される）。<br/>
    /// 2. 長辺が上限を超える場合はコアの面積平均で縮小する（拡大はしない）。<br/>
    /// <b>縮小をコアへ委譲する理由</b>: 4 つの UI 版で同じ画質にするため
    /// （Avalonia 版の <c>Bitmap.DecodeToWidth</c> と面積平均相当）。
    /// </remarks>
    private static EsReadOnlyBitmap? Decode(string filePath, int maxSize)
    {
        EsReadOnlyBitmap loaded;
        try
        {
            loaded = EsReadOnlyBitmap.Load(filePath);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 非画像・破損・削除競合は「表示できない」として扱う
            return null;
        }

        try
        {
            if (loaded.Width <= maxSize && loaded.Height <= maxSize)
            {
                return loaded;
            }

            // 縮小は書き換え可能な画像を返すため、凍結して 0 コピーで読み取り専用の面へ渡す
            using var resized = loaded.Resize(maxSize, EsImageQuality.High);
            resized.SetReadOnly();
            return EsReadOnlyBitmap.FromBitmap(resized);
        }
        finally
        {
            // 元の画像は（縮小した場合は）複製を作らないため、縮小結果とは別に解放する
            // （縮小しなかった場合は返した画像をそのまま使うため、ここでは解放しない）
            if (loaded.Width > maxSize || loaded.Height > maxSize)
            {
                loaded.Dispose();
            }
        }
    }
}

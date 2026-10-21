using System.Collections.Concurrent;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;

namespace EsUtil.SaveEvidence.App.MewUI.Services;

/// <summary>画像ファイルを縮小してサムネイル用の画像（読み取りの面）として読み込むローダー。</summary>
/// <remarks>
/// <b>縮小デコード</b>: コアの <see cref="EsReadOnlyBitmap.Load(string, int)"/> へ長辺の上限を渡し、
/// WIC のスケーラーで<b>デコードしながら縮小する</b>（原寸のバッファを確保しない。 補間は Fant であり、
/// WinUI 3 版と同じ画質になる）。<br/>
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
    /// 3. 無ければ縮小デコードしてキャッシュへ登録する。<br/>
    /// <b>保持数を呼び出し側が渡す理由</b>: 一覧が表示する件数より小さい上限だと、生成の途中で表示中の
    /// サムネイルが破棄される（上限超過時はキャッシュが保持する値を破棄するため、描画が破棄済みの
    /// バッファへ触れる）。
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
    /// <b>縮小はデコード時に行う</b>: 原寸のバッファを確保しないため、4K の画像を多数並べても使用量が増えない。<br/>
    /// <b>失敗は例外にしない</b>: 非画像・破損・削除競合は「表示できない」として null を返す。
    /// </remarks>
    private static EsReadOnlyBitmap? Decode(string filePath, int maxSize)
    {
        try
        {
            return EsReadOnlyBitmap.Load(filePath, maxSize);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 非画像・破損・削除競合は「表示できない」として扱う（一覧を壊さない）
            return null;
        }
    }
}

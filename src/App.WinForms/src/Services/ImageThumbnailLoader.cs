using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImage = System.Drawing.Image;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace EsUtil.SaveEvidence.App.WinForms.Services;

/// <summary>画像ファイルを縮小してサムネイル用の画像（読み取りの面）として読み込むローダー。</summary>
/// <remarks>
/// <b>縮小デコード</b>: GDI+ は「縮小した寸法でデコードする」要求を受け付けないため、一度デコードしてから<br/>
/// 目標寸法へ縮小する（<b>原寸のバッファが一時的に必要</b>になる）。<br/>
/// <b>キャッシュ</b>: 同一ファイル・同一サイズの結果を共有する。 ファイルの更新日時をキーに含めるため、<br/>
/// 内容が変われば再デコードされる。 容量管理は <see cref="CapacityLimitedCache"/> へ委譲し、<br/>
/// 上限を超えると <see cref="EsReadOnlyBitmap"/> が破棄されてから破棄される（そのため上限は<br/>
/// 呼び出し側が表示する件数を渡す）。<br/>
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
    /// 3. 無ければ縮小してキャッシュへ登録する。<br/>
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

    /// <summary>画像を縮小して設定の長辺へ収める（元より小さければ等倍の複製を返す）。</summary>
    /// <param name="source">縮小する画像（読み取りのみ。 破棄しない）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 未満は 1 として扱う。</param>
    /// <returns>縮小したビットマップ。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <remarks>
    /// 縮小はコアの面積平均（<see cref="EsImageQuality.High"/>）に委ねる（GDI+ の縮小は縮小率が大きいと<br/>
    /// 細線を実際より濃く残すため、ファイルのサムネイルと画質を揃える）。<br/>
    /// <b>拡大しない理由</b>: コアの縮小は拡大を要求すると例外になるため、長辺が上限以下なら複製だけを返す。
    /// </remarks>
    public static Bitmap ToThumbnailBitmap(EsReadOnlyBitmap source, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(source);

        var size = Math.Max(1, maxSize);
        if (Math.Max(source.Width, source.Height) <= size)
        {
            return GdiBitmapInterop.ToDrawingBitmap(source);
        }

        using var resized = source.Resize(size, EsImageQuality.High);
        resized.SetReadOnly();
        using var readOnly = EsReadOnlyBitmap.FromBitmap(resized);
        return GdiBitmapInterop.ToDrawingBitmap(readOnly);
    }

    /// <summary>画像ファイルを縮小してデコードする。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 以上。</param>
    /// <returns>デコードした画像。 デコードできない場合は null。</returns>
    /// <remarks>
    /// 1. 長辺が上限に収まる倍率を求める（上限以下なら等倍）。<br/>
    /// 2. 倍率が 1 未満なら目標寸法のビットマップへ高品質で縮小する。<br/>
    /// 3. 読み取りの面へ複製して返す（呼び出し側は破棄しない。 破棄はキャッシュが行う）。<br/>
    /// <b>補間方式</b>: GDI+ の高品質バイキュービックを使う（コアの面積平均とは画素が一致しない。
    /// サムネイルは一覧の表示専用であり、保存画像は元の画素のまま変わらない）。
    /// </remarks>
    private static EsReadOnlyBitmap? Decode(string filePath, int maxSize)
    {
        using var stream = File.OpenRead(filePath);
        using var source = DrawingImage.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
        if (source.Width <= 0 || source.Height <= 0)
        {
            return null;
        }

        var scale = Math.Min((double)maxSize / source.Width, (double)maxSize / source.Height);
        var targetWidth = scale < 1.0 ? Math.Max(1, (int)Math.Round(source.Width * scale)) : source.Width;
        var targetHeight = scale < 1.0 ? Math.Max(1, (int)Math.Round(source.Height * scale)) : source.Height;

        using var scaled = new DrawingBitmap(targetWidth, targetHeight, DrawingPixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = scale < 1.0 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, new System.Drawing.Rectangle(0, 0, targetWidth, targetHeight));
        }

        return GdiBitmapInterop.ToEsReadOnlyBitmap(scaled);
    }
}

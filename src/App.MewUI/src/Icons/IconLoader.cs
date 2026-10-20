using System.Collections.Concurrent;
using Aprillz.MewUI;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Icons;
using EsUtil.SaveEvidence.App.Core.Models;

namespace EsUtil.SaveEvidence.App.MewUI.Icons;

/// <summary>Icons フォルダの Fluent アイコン（SVG）からパス形状と画像を生成するヘルパー（MewUI 版）。</summary>
/// <remarks>
/// SVG の出所は <see cref="SvgIconLoader"/>（App.Core）が単一所有する（4 つの UI 版で同じファイルを使う）。<br/>
/// 各 SVG は Fluent System Icons 形式の単一 <c>&lt;path d="..."&gt;</c> で構成され、viewBox は 24x24。<br/>
/// <br/>
/// <b>ラスタライズは自前</b>: MewUI にはジオメトリを描く要素が無いため、パスデータを折れ線へ分解し、
/// 走査線で被覆率を数えて画素へ展開する（<see cref="SvgPathRasterizer"/>。 外部ライブラリを使わない）。<br/>
/// 生成した画素は BGRA8888 のまま <c>ImageSource.FromBgraPixels</c> へ渡す（PNG の往復をしない）。<br/>
/// 生成結果は容量制限付きのキャッシュで共有する（ナビ・ツールバーのアイコンは表示のたびに参照されるため）。
/// </remarks>
internal static class IconLoader
{
    /// <summary>画像キャッシュの最大保持数。</summary>
    private const int CacheCapacity = 48;

    /// <summary>生成済み画像のキャッシュ（ファイル名・サイズ・色で一意）。</summary>
    private static readonly ConcurrentDictionary<(string File, int Size, uint Argb), ImageSource> s_cache = new();

    /// <summary>アイコン色（明色テーマ用）。</summary>
    private static readonly Color s_foregroundLight = new(255, 26, 26, 26);

    /// <summary>アイコン色（暗色テーマ用）。</summary>
    private static readonly Color s_foregroundDark = new(255, 238, 238, 238);

    /// <summary>現在のテーマが暗色かどうかを取得する。</summary>
    /// <returns>暗色テーマの場合は true。</returns>
    /// <remarks>テーマ未確定（ウィンドウ生成前）は明色として扱う。</remarks>
    public static bool IsDarkTheme()
    {
        try
        {
            return Application.Current?.Theme?.IsDark ?? false;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // テーマ未確定（起動前）は明色として扱う
            return false;
        }
    }

    /// <summary>テーマに応じたアイコン・文字の色を取得する。</summary>
    /// <returns>前景色。</returns>
    public static Color ForegroundColor() => IsDarkTheme() ? s_foregroundDark : s_foregroundLight;

    /// <summary>指定 SVG の図形（折れ線へ分解する前のパス）を読み込む。</summary>
    /// <param name="fileName">SVG ファイル名（例: "ic_fluent_save_24_regular.svg"）。</param>
    /// <returns>SVG の図形。 読み込めない場合は空。</returns>
    /// <remarks>読み込み失敗（リソース欠落・パスデータ不正）でも例外にせず空の図形を返す（UI を壊さない）。</remarks>
    public static IReadOnlyList<SvgPathFigure> LoadFigures(string fileName)
    {
        var icon = SvgIconLoader.Load(fileName);
        if (icon.PathData.Count == 0)
        {
            return [];
        }

        // 各 SVG は単一の <path d="..."> で構成される（複数ある場合は連結して 1 つの図形として扱う）
        var figures = new List<SvgPathFigure>();
        foreach (var data in icon.PathData)
        {
            figures.AddRange(SvgPathData.Parse(data));
        }

        return figures;
    }

    /// <summary>指定 SVG を指定サイズ・指定色でラスタライズした画像を取得する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。 1 未満は 1 として扱う。</param>
    /// <param name="color">アイコンの色。</param>
    /// <returns>ラスタライズした画像。 生成結果はキャッシュして共有する。</returns>
    /// <remarks>
    /// 図形の外接矩形を一辺いっぱいへ広げて描く（Avalonia 版の <c>PathIcon</c> と同じ見え方）。<br/>
    /// 生成した画像は本クラスが所有し、呼び出し側は破棄しない（キャッシュから共有されるため）。
    /// </remarks>
    public static ImageSource LoadImage(string fileName, int size, Color color)
    {
        var length = Math.Max(1, size);
        var key = (fileName, length, color.ToArgb());

        return CapacityLimitedCache.GetOrAdd(s_cache, CacheCapacity, key, static k =>
        {
            // 汎用ヘルパー経由ではタプルの要素名が失われるため、分解して受け取る
            var (file, size2, argb) = k;
            var pixels = Rasterize(file, size2, argb);
            return ImageSource.FromBgraPixels(size2, size2, pixels, hasAlpha: true);
        });
    }

    /// <summary>指定 SVG の画像からウィンドウ（タスクバー）アイコンを生成する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">アイコンの一辺（ピクセル。 既定 32）。</param>
    /// <param name="color">アイコンの色。</param>
    /// <returns>生成した <see cref="IconSource"/>。 複数サイズを含む。</returns>
    /// <remarks>
    /// タスクバー・Alt+Tab・タイトルバーの要求サイズに応じて選ばれるよう、複数のサイズを ICO へ詰めて渡す。<br/>
    /// 各画像は <b>32bpp の DIB（無圧縮）</b>で格納する（PNG を作らないため、エンコーダーを必要としない）。
    /// </remarks>
    public static IconSource LoadWindowIcon(string fileName, int size, Color color)
    {
        var sizes = new[] { 16, 24, 32, 48, 64, size }.Distinct().Where(static s => s > 0).OrderBy(static s => s).ToArray();
        return IconSource.FromBytes(BuildIco([.. sizes.Select(s => (s, Rasterize(fileName, s, color.ToArgb())))]));
    }

    /// <summary>SVG を指定サイズ・指定色の BGRA 画素へ展開する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>画素（行幅 = 幅 × 4）。</returns>
    private static byte[] Rasterize(string fileName, int size, uint argb)
    {
        var figures = LoadFigures(fileName);
        return figures.Count == 0 ? new byte[size * size * 4] : SvgPathRasterizer.Rasterize(figures, size, argb);
    }

    /// <summary>複数サイズの画素を 1 つの ICO（アイコン）バイナリへ詰める。</summary>
    /// <param name="images">サイズと BGRA 画素の組（1 件以上）。</param>
    /// <returns>ICO 形式のバイト列。</returns>
    /// <remarks>
    /// ICO の構造は ICONDIR（6 バイト）+ ICONDIRENTRY（16 バイト × 件数）+ 画像データ。<br/>
    /// 画像は <b>BITMAPINFOHEADER（40 バイト）+ 下から上の BGRA + AND マスク</b>で格納する（PNG を使わない）。
    /// </remarks>
    private static byte[] BuildIco(IReadOnlyList<(int Size, byte[] Bgra)> images)
    {
        const int DirectorySize = 6;
        const int EntrySize = 16;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // ICONDIR（予約・種別 1 = アイコン・件数）
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);

        var offset = DirectorySize + (EntrySize * images.Count);
        var payloads = new List<byte[]>(images.Count);
        foreach (var (size, bgra) in images)
        {
            var payload = BuildDib(size, bgra);
            payloads.Add(payload);

            // 256 以上のサイズは 0 として記録する（ICO の仕様）
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);   // パレット数（32bpp では未使用）
            writer.Write((byte)0);   // 予約
            writer.Write((ushort)1); // プレーン数
            writer.Write((ushort)32); // ビット深度
            writer.Write(payload.Length);
            writer.Write(offset);
            offset += payload.Length;
        }

        foreach (var payload in payloads)
        {
            writer.Write(payload);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>32bpp の DIB（BITMAPINFOHEADER + 画素 + AND マスク）を組み立てる。</summary>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <param name="bgra">画素（行幅 = 幅 × 4・上から下）。</param>
    /// <returns>DIB のバイト列。</returns>
    /// <remarks>
    /// <b>高さは 2 倍で書く</b>（ICO の仕様: 画素の高さ + AND マスクの高さ）。<br/>
    /// AND マスクは 1bpp で 4 バイト境界へ揃える（32bpp のアルファを使うため全 0 でよい）。
    /// </remarks>
    private static byte[] BuildDib(int size, byte[] bgra)
    {
        const int HeaderSize = 40;
        var maskStride = ((size + 31) / 32) * 4;
        var maskSize = maskStride * size;
        var payload = new byte[HeaderSize + (size * size * 4) + maskSize];

        BitConverter.GetBytes(HeaderSize).CopyTo(payload, 0);
        BitConverter.GetBytes(size).CopyTo(payload, 4);
        BitConverter.GetBytes(size * 2).CopyTo(payload, 8);
        BitConverter.GetBytes((ushort)1).CopyTo(payload, 12);
        BitConverter.GetBytes((ushort)32).CopyTo(payload, 14);
        BitConverter.GetBytes(size * size * 4).CopyTo(payload, 20);

        // 画素は下から上へ並べる
        for (var y = 0; y < size; y++)
        {
            Array.Copy(bgra, y * size * 4, payload, HeaderSize + ((size - 1 - y) * size * 4), size * 4);
        }

        return payload;
    }
}

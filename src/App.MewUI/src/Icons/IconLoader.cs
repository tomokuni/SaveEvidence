using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Aprillz.MewUI;
using EsUtil.SaveEvidence.App.Core.Models;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.MewUI.Icons;

/// <summary>Icons フォルダの Fluent アイコン（SVG）からパス形状と画像を生成するヘルパー（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>IconLoader</c> と同じ SVG を共有する（<c>App.AvaloniaUI/src/Icons/*.svg</c> を埋め込みリソースとして参照する）。<br/>
/// 各 SVG は Fluent System Icons 形式の単一 <c>&lt;path d="..."&gt;</c> で構成され、viewBox は 24x24。<br/>
/// <br/>
/// <b>Avalonia 版との違い</b>: MewUI には <c>PathIcon</c>（ジオメトリを色付きで描く要素）が無いため、<br/>
/// ジオメトリを Skia で指定サイズ・指定色へラスタライズし、<see cref="ImageSource"/> として <c>Image</c> 要素やウィンドウアイコンへ渡す。<br/>
/// 生成結果は容量制限付きのキャッシュで共有する（ナビ・ツールバーのアイコンは表示のたびに参照されるため）。
/// </remarks>
internal static partial class IconLoader
{
    /// <summary>SVG の viewBox サイズ（Fluent System Icons 標準の 24x24）。</summary>
    private const double ViewBoxSize = 24;

    /// <summary>画像キャッシュの最大保持数。</summary>
    private const int CacheCapacity = 48;

    /// <summary>埋め込みリソースの論理名プレフィックス。</summary>
    private const string ResourcePrefix = "SaveEvidence.MewUI.Icons.";

    /// <summary>生成済み画像のキャッシュ（ファイル名・サイズ・色で一意）。</summary>
    private static readonly ConcurrentDictionary<(string File, int Size, uint Argb), ImageSource> s_cache = new();

    /// <summary>アイコン色（明色テーマ用）。</summary>
    private static readonly Color s_foregroundLight = new Color(255, 26, 26, 26);

    /// <summary>アイコン色（暗色テーマ用）。</summary>
    private static readonly Color s_foregroundDark = new Color(255, 238, 238, 238);

    /// <summary>現在のテーマが暗色かどうかを取得する。</summary>
    /// <returns>暗色テーマの場合は true。</returns>
    /// <remarks>テーマ未確定（ウィンドウ生成前）は明色として扱う。</remarks>
    public static bool IsDarkTheme()
    {
        try
        {
            return Application.Current?.Theme?.IsDark ?? false;
        }
        catch (Exception)
        {
            // テーマ未確定（起動前）は明色として扱う
            return false;
        }
    }

    /// <summary>テーマに応じたアイコン・文字の色を取得する。</summary>
    /// <returns>前景色。</returns>
    public static Color ForegroundColor() => IsDarkTheme() ? s_foregroundDark : s_foregroundLight;

    /// <summary>指定 SVG のパス形状を読み込む。</summary>
    /// <param name="fileName">SVG ファイル名（例: "ic_fluent_save_24_regular.svg"）。</param>
    /// <returns>SVG のパス形状。 読み込めない場合は空の形状。</returns>
    /// <remarks>読み込み失敗（リソース欠落・パスデータ不正）でも例外にせず空の形状を返す（UI を壊さない）。</remarks>
    public static SKPath LoadPath(string fileName)
    {
        try
        {
            var svg = ReadSvg(fileName);
            var match = PathDataRegex().Match(svg);
            return match.Success ? SKPath.ParseSvgPathData(match.Groups[1].Value) ?? new SKPath() : new SKPath();
        }
        catch (Exception)
        {
            // リソース読み込み失敗（欠落等）は空の形状で代替する
            return new SKPath();
        }
    }

    /// <summary>指定 SVG を指定サイズ・指定色でラスタライズした画像を取得する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。 1 未満は 1 として扱う。</param>
    /// <param name="color">アイコンの色。</param>
    /// <returns>ラスタライズした画像。 生成結果はキャッシュして共有する。</returns>
    /// <remarks>
    /// 24x24 の viewBox を <paramref name="size"/> へ等方スケールして描画する。<br/>
    /// 生成した画像は本クラスが所有し、呼び出し側は破棄しない（キャッシュから共有されるため）。
    /// </remarks>
    public static ImageSource LoadImage(string fileName, int size, Color color)
    {
        var length = Math.Max(1, size);
        var key = (fileName, length, color.ToArgb());

        return CapacityLimitedCache.GetOrAdd(s_cache, CacheCapacity, key, k =>
        {
            // 汎用ヘルパー経由ではタプルの要素名が失われるため、分解して受け取る
            var (file, size2, argb) = k;
            using var path = LoadPath(file);

            using var bitmap = new SKBitmap(size2, size2);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);

                var scale = size2 / (float)ViewBoxSize;
                canvas.Scale(scale);
                using var paint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    // アイコンの色は 0xAARRGGBB で受け取る
                    Color = new SKColor(
                        (byte)((argb >> 16) & 0xFF),
                        (byte)((argb >> 8) & 0xFF),
                        (byte)(argb & 0xFF),
                        (byte)((argb >> 24) & 0xFF)),
                };
                canvas.DrawPath(path, paint);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return ImageSource.FromBytes(data.ToArray());
        });
    }

    /// <summary>指定 SVG の画像からウィンドウ（タスクバー）アイコンを生成する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">アイコンの一辺（ピクセル。 既定 32）。</param>
    /// <param name="color">アイコンの色。</param>
    /// <returns>生成した <see cref="IconSource"/>。 複数サイズを含む。</returns>
    /// <remarks>
    /// MewUI の <see cref="IconSource.FromBytes"/> は ICO 形式を要求するため、<br/>
    /// 各サイズの PNG を ICO コンテナ（Vista 以降の PNG 埋め込み形式）へ詰めて渡す。<br/>
    /// タスクバー・Alt+Tab・タイトルバーの要求サイズに応じて選ばれるよう、複数のサイズを登録する。
    /// </remarks>
    public static IconSource LoadWindowIcon(string fileName, int size, Color color)
    {
        var sizes = new[] { 16, 24, 32, 48, 64, size }.Distinct().Where(static s => s > 0).OrderBy(static s => s).ToArray();
        return IconSource.FromBytes(BuildIco([.. sizes.Select(s => (s, EncodePng(fileName, s, color)))]));
    }

    /// <summary>複数サイズの PNG を 1 つの ICO（アイコン）バイナリへ詰める。</summary>
    /// <param name="images">サイズと PNG バイト列の組（1 件以上）。</param>
    /// <returns>ICO 形式のバイト列。</returns>
    /// <remarks>
    /// ICO の構造は ICONDIR（6 バイト）+ ICONDIRENTRY（16 バイト × 件数）+ 画像データ。<br/>
    /// Vista 以降は各画像に PNG をそのまま格納できる（BMP への展開が不要）。
    /// </remarks>
    private static byte[] BuildIco(IReadOnlyList<(int Size, byte[] Png)> images)
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
        foreach (var (size, png) in images)
        {
            // 256 以上のサイズは 0 として記録する（ICO の仕様）
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);  // パレット数（32bpp では未使用）
            writer.Write((byte)0);  // 予約
            writer.Write((ushort)1); // プレーン数
            writer.Write((ushort)32); // ビット深度
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in images)
        {
            writer.Write(png);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>指定 SVG を指定サイズ・指定色で PNG バイト列へエンコードする。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <param name="color">アイコンの色。</param>
    /// <returns>PNG のバイト列。</returns>
    private static byte[] EncodePng(string fileName, int size, Color color)
    {
        using var path = LoadPath(fileName);
        using var bitmap = new SKBitmap(size, size);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var scale = size / (float)ViewBoxSize;
            canvas.Scale(scale);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(
                    (byte)((color.ToArgb() >> 16) & 0xFF),
                    (byte)((color.ToArgb() >> 8) & 0xFF),
                    (byte)(color.ToArgb() & 0xFF),
                    (byte)((color.ToArgb() >> 24) & 0xFF)),
            };
            canvas.DrawPath(path, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>埋め込み SVG リソースを読み込む。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <returns>SVG の XML テキスト。</returns>
    /// <remarks>
    /// 埋め込みリソースの論理名は csproj の <c>LogicalName</c> で固定する<br/>
    /// （トリミング / Native AOT でもマニフェスト名の列挙なしで読めるようにするため）。
    /// </remarks>
    private static string ReadSvg(string fileName)
    {
        var assembly = typeof(IconLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new FileNotFoundException($"アイコンリソースが見つかりません: {fileName}", fileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>SVG の path 要素から d 属性（パスデータ）を抽出する正規表現。</summary>
    [GeneratedRegex("d=\"([^\"]*)\"")]
    private static partial Regex PathDataRegex();
}

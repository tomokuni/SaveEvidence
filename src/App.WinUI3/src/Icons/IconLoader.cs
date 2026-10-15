using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.WinUI3.Views;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.WinUI3.Icons;

/// <summary>Icons フォルダの Fluent アイコン（SVG）から画像を生成するヘルパー（WinUI 3 版）。</summary>
/// <remarks>
/// 他の UI 版と同じ SVG を共有する（<c>App.AvaloniaUI/src/Icons/*.svg</c> を埋め込みリソースとして参照する）。<br/>
/// 各 SVG は Fluent System Icons 形式の単一 <c>&lt;path d="..."&gt;</c> で構成され、viewBox は 24x24。<br/>
/// <br/>
/// <b>ラスタライズする理由</b>: WinUI 3 の <c>PathIcon</c> はジオメトリの文字列を XAML の型変換でしか<br/>
/// 受け取れず、コードから組み立てるにはテキスト解析の反射経路が必要になる。 本クラスは Skia で<br/>
/// 指定サイズ・指定色へラスタライズし、<see cref="WriteableBitmap"/> として渡す（中間の符号化を行わない）。<br/>
/// 生成結果は容量制限付きのキャッシュで共有する（ナビ・ツールバーのアイコンは表示のたびに参照されるため）。
/// </remarks>
internal static partial class IconLoader
{
    /// <summary>SVG の viewBox サイズ（Fluent System Icons 標準の 24x24）。</summary>
    private const double ViewBoxSize = 24;

    /// <summary>画像キャッシュの最大保持数。</summary>
    private const int CacheCapacity = 48;

    /// <summary>埋め込みリソースの論理名プレフィックス。</summary>
    private const string ResourcePrefix = "SaveEvidence.WinUI3.Icons.";

    /// <summary>アイコン色（明色テーマ用）。</summary>
    private const uint ForegroundLight = 0xFF1A1A1A;

    /// <summary>アイコン色（暗色テーマ用）。</summary>
    private const uint ForegroundDark = 0xFFEEEEEE;

    /// <summary>生成済み画像のキャッシュ（ファイル名・サイズ・色で一意）。</summary>
    private static readonly ConcurrentDictionary<(string File, int Size, uint Argb), ImageSource> s_cache = new();

    /// <summary>テーマに応じたアイコン・文字の色を取得する。</summary>
    /// <returns>前景色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>テーマ未確定（ウィンドウ生成前）は明色として扱う。</remarks>
    public static uint ForegroundColor() => AppStyles.IsDarkTheme ? ForegroundDark : ForegroundLight;

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
            return match.Success ? SKPath.ParseSvgPathData(match.Groups["path"].Value) ?? new SKPath() : new SKPath();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // リソース読み込み失敗（欠落等）は空の形状で代替する
            return new SKPath();
        }
    }

    /// <summary>指定 SVG を指定サイズ・指定色でラスタライズした画像を取得する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。 1 未満は 1 として扱う。</param>
    /// <param name="argb">アイコンの色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>ラスタライズした画像。 生成結果はキャッシュして共有する。</returns>
    /// <remarks>
    /// 24x24 の viewBox を <paramref name="size"/> へ等方スケールして描画する。<br/>
    /// 生成した画像は本クラスが所有し、呼び出し側は破棄しない（キャッシュから共有されるため）。
    /// </remarks>
    public static ImageSource LoadImage(string fileName, int size, uint argb)
    {
        var length = Math.Max(1, size);
        var key = (File: fileName, Size: length, Argb: argb);

        // ラムダの引数型はタプル（要素名は保持される）。 Rasterize は static メソッドのため static ラムダにできない
        // （static ラムダの内側から型のメンバーを参照できない）
        return CapacityLimitedCache.GetOrAdd(s_cache, CacheCapacity, key, k => Rasterize(k.File, k.Size, k.Argb));
    }

    /// <summary>テーマに合うアイコンを生成する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。</param>
    /// <returns>現在のテーマの前景色でラスタライズした画像。</returns>
    public static ImageSource LoadThemedImage(string fileName, int size)
        => LoadImage(fileName, size, ForegroundColor());

    /// <summary>SVG のパス形状を画像へラスタライズする。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。 1 以上。</param>
    /// <param name="argb">描画色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>生成した画像。</returns>
    /// <remarks>
    /// 画素形式は <see cref="WriteableBitmap"/> が要求する BGRA8888・Premul に合わせる<br/>
    /// （ピクセル バッファへそのまま書き込める）。
    /// </remarks>
    private static ImageSource Rasterize(string fileName, int size, uint argb)
    {
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            using var path = LoadPath(fileName);
            canvas.Scale(size / (float)ViewBoxSize);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF),
                    (byte)((argb >> 24) & 0xFF)),
            };
            canvas.DrawPath(path, paint);
        }

        var image = new WriteableBitmap(size, size);
        using (var target = image.PixelBuffer.AsStream())
        {
            target.Write(bitmap.GetPixelSpan());
        }

        image.Invalidate();
        return image;
    }

    /// <summary>埋め込みリソースから SVG のテキストを読み込む。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <returns>SVG のテキスト。 読み込めない場合は空文字。</returns>
    private static string ReadSvg(string fileName)
    {
        using var stream = typeof(IconLoader).Assembly.GetManifestResourceStream(ResourcePrefix + fileName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>SVG の <c>path</c> 要素の <c>d</c> 属性を取り出す正規表現。</summary>
    /// <returns>パス データを <c>path</c> グループへ捕捉する正規表現。</returns>
    /// <remarks>引用符は 16 進エスケープで表す（検証用の正規表現に生の引用符を持ち込まない）。</remarks>
    [GeneratedRegex(@"<path\b[^>]*?\bd\s*=\s*\x22(?<path>[^\x22]+)\x22",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PathDataRegex();
}

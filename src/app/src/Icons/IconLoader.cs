using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using app.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace app.Icons;

/// <summary>
/// Icons フォルダの Fluent アイコン（SVG）からジオメトリ・ウィンドウアイコンを生成するヘルパー。
/// </summary>
/// <remarks>
/// 各 SVG は Fluent System Icons 形式の単一 <c>&lt;path d="..."&gt;</c> で構成され、<br/>
/// viewBox は 24x24。<br/>
/// - <see cref="LoadGeometry"/>: UI 上の <see cref="PathIcon"/> 用ジオメトリ（パスデータ）を返す。<br/>
/// - <see cref="LoadWindowIcon"/>: ウィンドウ / アプリのアイコン用 <see cref="WindowIcon"/> を返す。<br/>
/// 実装の詳細: SVG は AvaloniaResource として埋め込まれ（app.csproj の <c>AvaloniaResource</c>）、<br/>
/// 実行時に <c>avares://</c> で読み込む。パスデータは <c>StreamGeometry.Parse</c> で解析する。
/// </remarks>
internal static partial class IconLoader
{
    /// <summary>SVG の viewBox サイズ（Fluent System Icons 標準の 24x24）。</summary>
    private const double ViewBoxSize = 24;

    /// <summary>ジオメトリキャッシュの最大保持数。</summary>
    private const int GeometryCacheCapacity = 32;

    private static readonly ConcurrentDictionary<string, Geometry> s_geometryCache = new();

    /// <summary>指定 SVG ファイルからアイコン描画用のジオメトリを読み込む。</summary>
    /// <param name="fileName">SVG ファイル名（例: "ic_fluent_scan_table_24_regular.svg"）。</param>
    /// <returns>パスデータから生成したジオメトリ。</returns>
    /// <remarks>
    /// パスデータが抽出できない・リソースが見つからない場合は空ジオメトリ（M0,0）を返す<br/>
    /// （表示上のフェールセーフ。アイコン欠落時もアプリはクラッシュしない）。
    /// </remarks>
    public static Geometry LoadGeometry(string fileName)
        => CapacityLimitedCache.GetOrAdd(s_geometryCache, GeometryCacheCapacity, fileName, static f =>
        {
            try
            {
                var svg = ReadSvg(f);
                var match = PathDataRegex().Match(svg);
                return match.Success ? StreamGeometry.Parse(match.Groups[1].Value) : StreamGeometry.Parse("M0,0");
            }
            catch (Exception)
            {
                // リソース読み込み失敗（FileNotFoundException 等）は空ジオメトリで代替する（UI を壊さない）
                return StreamGeometry.Parse("M0,0");
            }
        });

    /// <summary>指定 SVG ファイルからウィンドウアイコンを生成する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">アイコンのサイズ（ピクセル。既定 32）。</param>
    /// <returns>生成した <see cref="WindowIcon"/>。</returns>
    /// <remarks>
    /// 24x24 のジオメトリを <paramref name="size"/> に等方スケールして描画し、<br/>
    /// <see cref="RenderTargetBitmap"/> 経由で <see cref="WindowIcon"/> に変換する。
    /// </remarks>
    public static WindowIcon LoadWindowIcon(string fileName, int size = 32)
    {
        var geometry = LoadGeometry(fileName);

        // 24x24 のジオメトリを指定サイズへスケールして RenderTargetBitmap に直接描画する
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var ctx = bitmap.CreateDrawingContext())
        {
            var scale = size / ViewBoxSize;
            using (ctx.PushTransform(Matrix.CreateScale(scale, scale)))
            {
                ctx.DrawGeometry(Brushes.Black, null, geometry);
            }
        }

        return new WindowIcon(bitmap);
    }

    /// <summary>埋め込み SVG リソースを読み込む。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <returns>SVG の XML テキスト。</returns>
    /// <remarks>
    /// アセンブリ名は本クラスが属するアセンブリ（app.csproj の <c>AssemblyName</c> = SaveEvidence）から<br/>
    /// 動的に解決する（<see cref="Assembly.GetName"/>）。アセンブリ名変更時も追従する。
    /// </remarks>
    private static string ReadSvg(string fileName)
    {
        var assemblyName = typeof(IconLoader).Assembly.GetName().Name;
        var uri = new Uri($"avares://{assemblyName}/Icons/{fileName}");
        using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>SVG の path 要素から d 属性（パスデータ）を抽出する正規表現。</summary>
    [GeneratedRegex("d=\"([^\"]*)\"")]
    private static partial Regex PathDataRegex();
}

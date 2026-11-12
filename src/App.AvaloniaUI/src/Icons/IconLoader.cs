using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Caching;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Icons;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Icons;

/// <summary>Icons フォルダの Fluent アイコン（SVG）からジオメトリ・ウィンドウアイコンを生成するヘルパー。</summary>
/// <remarks>
/// 各 SVG は Fluent System Icons 形式の単一 <c>&lt;path d="..."&gt;</c> で構成され、<br/>
/// viewBox は 24x24。<br/>
/// - <see cref="LoadGeometry"/>: UI 上の <see cref="PathIcon"/> 用ジオメトリ（パスデータ）を返す。<br/>
/// - <see cref="LoadWindowIcon"/>: ウィンドウ / アプリのアイコン用 <see cref="WindowIcon"/> を返す。<br/>
/// 実装の詳細: SVG は AvaloniaResource として埋め込まれ（EsUtil.SaveEvidence.csproj の <c>AvaloniaResource</c>）、<br/>
/// 実行時に <c>avares://</c> で読み込む。 パスデータは <c>StreamGeometry.Parse</c> で解析する。
/// </remarks>
internal static partial class IconLoader
{
    /// <summary>SVG の viewBox サイズ（Fluent System Icons 標準の 24x24）。</summary>
    private const double ViewBoxSize = 24;

    /// <summary>ジオメトリキャッシュの最大保持数。</summary>
    private const int GeometryCacheCapacity = 32;

    private static readonly CapacityLimitedCache<string, Geometry> s_geometryCache =
        new(static f =>
        {
            var icon = AppIcons.Source.Load(f);
            return icon.PathData.Count == 0
                ? StreamGeometry.Parse("M0,0")
                : StreamGeometry.Parse(icon.PathData[0]);
        }, GeometryCacheCapacity);

    /// <summary>指定 SVG ファイルからアイコン描画用のジオメトリを読み込む。</summary>
    /// <param name="fileName">SVG ファイル名（例: "ic_fluent_scan_table_24_regular.svg"）。</param>
    /// <returns>パスデータから生成したジオメトリ。</returns>
    /// <remarks>
    /// パスデータが抽出できない・リソースが見つからない場合は空ジオメトリ（M0,0）を返す<br/>
    /// （表示上のフェールセーフ。 アイコン欠落時もアプリはクラッシュしない）。
    /// </remarks>
    public static Geometry LoadGeometry(string fileName) => s_geometryCache.GetOrAdd(fileName);

    /// <summary>指定 SVG ファイルからウィンドウアイコンを生成する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">アイコンのサイズ（ピクセル。 既定 32）。</param>
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

    /// <summary>指定 SVG を指定サイズ・指定色で描いたビットマップを取得する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <param name="color">塗りの色。</param>
    /// <returns>描いたビットマップ。 所有権は呼び出し側へ移る。</returns>
    public static Bitmap LoadBitmap(string fileName, int size, Color color)
    {
        var pixels = Math.Max(1, size);
        var icon = AppIcons.Source.Load(fileName);
        var bitmap = new RenderTargetBitmap(new PixelSize(pixels, pixels), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            foreach (var data in icon.PathData)
            {
                var geometry = StreamGeometry.Parse(data);
                var bounds = geometry.Bounds;

                // 図形の実寸（余白を除いた外接矩形）を枠いっぱいへ広げる式はコアが単一所有する
                // （4 つの UI 版で同じ大きさに見えるようにする）
                if (IconFit.Resolve(new EsRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), pixels) is not { } fit)
                {
                    continue;
                }

                using (context.PushTransform(
                    Matrix.CreateTranslation(fit.OffsetX, fit.OffsetY) * Matrix.CreateScale(fit.Scale, fit.Scale)))
                {
                    context.DrawGeometry(new SolidColorBrush(color), null, geometry);
                }
            }
        }

        return bitmap;
    }
}

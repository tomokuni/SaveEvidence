using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Icons;

/// <summary>他の UI 版と共有する SVG アイコンを GDI+ のビットマップへ描く単一の描画層。</summary>
/// <remarks>
/// <b>SVG を出所にする理由</b>: アイコンは 4 つの UI 版で同じ見た目にする必要がある。 出所を<br/>
/// <c>App.AvaloniaUI/src/Icons/*.svg</c>（Fluent System Icons 形式。 viewBox は 24x24）の 1 つに固定し、<br/>
/// 本 UI はそれを埋め込みリソースとして読み、GDI+ のパスへ変換して描く（<see cref="SvgPathData"/>）。<br/>
/// <br/>
/// <b>描画の性質</b>: サイズと色を引数で受け取り、<b>用途ごとに 1 枚のビットマップ</b>を描く（テーマの変更時に描き直す）。<br/>
/// 描画は <see cref="Bitmap"/> と <c>Graphics.FromImage</c> だけで完結するため、ウィンドウ（ハンドル）を生成せずに画素を検証できる。
/// </remarks>
internal static partial class IconLoader
{
    /// <summary>SVG の viewBox の一辺（Fluent System Icons 標準の 24x24）。</summary>
    private const double ViewBoxSize = 24;

    /// <summary>埋め込みリソースの論理名の接頭辞。</summary>
    private const string ResourcePrefix = "SaveEvidence.WinForms.Icons.";

    /// <summary>読み込んだパス データのキャッシュ（ファイル名で一意）。</summary>
    private static readonly ConcurrentDictionary<string, string> s_pathData = new(StringComparer.Ordinal);

    /// <summary>ウィンドウ アイコンの元になるハンドル（プロセスの寿命まで保持する）。</summary>
    private static IntPtr s_windowIconHandle;

    /// <summary>アイコンをビットマップとして描く。</summary>
    /// <param name="icon">描くアイコン。</param>
    /// <param name="size">一辺のピクセル数（1 未満は 1 として扱う）。</param>
    /// <param name="color">塗りの色。</param>
    /// <returns>描いたビットマップ（<c>Format32bppPArgb</c>）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// 背景は透明にする（ボタンの背景・テーマの面の上へ重ねて使う）。<br/>
    /// 図形はアンチエイリアスを有効にして描く（16 ピクセルでも字形が潰れないようにする）。
    /// </remarks>
    public static Bitmap Create(AppIcon icon, int size, Color color)
    {
        var pixels = Math.Max(1, size);
        var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        if (icon == AppIcon.Menu)
        {
            // 他の UI 版は ☰ を文字で表す。 文字が無い環境でも同じ見た目になるよう 3 本線を図形で描く
            DrawMenu(graphics, pixels, color);
            return bitmap;
        }

        var data = ReadPathData(icon);
        if (data.Length == 0)
        {
            // リソースの欠落・パス データの非対応は空のビットマップで代替する（UI を壊さない）
            return bitmap;
        }

        using var path = SvgPathData.Parse(data);
        if (path.PointCount == 0)
        {
            return bitmap;
        }

        // 図形の実寸（余白を除いた外接矩形）を一辺いっぱいへ広げる。
        // Avalonia 版の PathIcon はジオメトリの外接矩形を枠いっぱいへ引き伸ばすため、
        // SVG の viewBox（24x24）基準で描くとアイコンだけが小さく見える（実測）
        var bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bitmap;
        }

        var scale = Math.Min(pixels / bounds.Width, pixels / bounds.Height);
        var offsetX = ((pixels - (bounds.Width * scale)) / 2f) - (bounds.X * scale);
        var offsetY = ((pixels - (bounds.Height * scale)) / 2f) - (bounds.Y * scale);

        var state = graphics.Save();
        graphics.TranslateTransform(offsetX, offsetY);
        graphics.ScaleTransform(scale, scale);
        try
        {
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, path);
        }
        finally
        {
            graphics.Restore(state);
        }

        return bitmap;
    }

    /// <summary>ウィンドウ（タスク バー）アイコンを取得する。</summary>
    /// <param name="size">一辺のピクセル数。</param>
    /// <returns>ウィンドウ アイコン。</returns>
    /// <remarks>
    /// 他の UI 版と同じく <see cref="AppIcon.Capture"/> のアイコンを使う。<br/>
    /// <b>ハンドルは保持し、<see cref="Icon"/> は呼び出しごとに作る</b>: <see cref="Icon.FromHandle"/> は<br/>
    /// ハンドルを所有せず、Windows Forms はフォームに設定された <see cref="Icon"/> を破棄するため、<br/>
    /// 同じインスタンスを共有すると 2 つ目のウィンドウで破棄済みのアイコンを使うことになる。<br/>
    /// ハンドルはプロセスの寿命まで保持する（破棄すると無効なハンドルを指し続ける）。
    /// </remarks>
    public static Icon CreateWindowIcon(int size = 32)
    {
        if (s_windowIconHandle == IntPtr.Zero)
        {
            using var bitmap = Create(AppIcon.Capture, size, Color.Black);
            s_windowIconHandle = bitmap.GetHicon();
        }

        return Icon.FromHandle(s_windowIconHandle);
    }

    /// <summary>アイコンに対応する SVG ファイル名を解決する。</summary>
    /// <param name="icon">対象のアイコン。</param>
    /// <returns>SVG ファイル名。 共有するファイルが無い場合は空文字。</returns>
    /// <remarks>対応は他の UI 版（Avalonia / MewUI / WinUI 3）と同じファイルを選ぶ。</remarks>
    private static string ResolveFile(AppIcon icon) => icon switch
    {
        AppIcon.NavToggle => "ic_fluent_arrow_autofit_width_dotted_24_regular.svg",
        AppIcon.Capture => AppIdentity.IconFileName,
        AppIcon.Folder => "ic_fluent_folder_search_24_regular.svg",
        AppIcon.Undo => "ic_fluent_arrow_undo_24_regular.svg",
        AppIcon.Settings => "ic_fluent_settings_24_regular.svg",
        AppIcon.Save => "ic_fluent_save_24_regular.svg",
        AppIcon.ScreenSelect => "ic_fluent_rectangle_landscape_24_regular.svg",
        AppIcon.WindowSelect => "ic_fluent_window_24_regular.svg",
        AppIcon.AreaSelect => "ic_fluent_select_object_24_regular.svg",
        AppIcon.Crop => "ic_fluent_crop_24_regular.svg",
        AppIcon.ZoomIn => "ic_fluent_add_circle_24_regular.svg",
        AppIcon.ZoomOut => "ic_fluent_subtract_circle_24_regular.svg",
        AppIcon.ZoomFit => "ic_fluent_page_fit_24_regular.svg",
        _ => "",
    };

    /// <summary>アイコンのパス データ（<c>d</c> 属性の値）を読み込む。</summary>
    /// <param name="icon">対象のアイコン。</param>
    /// <returns>パス データ。 読み込めない場合は空文字。 読み込み結果はキャッシュする。</returns>
    private static string ReadPathData(AppIcon icon)
    {
        var fileName = ResolveFile(icon);
        if (fileName.Length == 0)
        {
            return "";
        }

        return s_pathData.GetOrAdd(fileName, static name =>
        {
            using var stream = typeof(IconLoader).Assembly.GetManifestResourceStream(ResourcePrefix + name);
            if (stream is null)
            {
                return "";
            }

            using var reader = new StreamReader(stream);
            var match = PathDataRegex().Match(reader.ReadToEnd());
            return match.Success ? match.Groups["path"].Value : "";
        });
    }

    /// <summary>メニュー（☰）の 3 本線を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="pixels">一辺のピクセル数。</param>
    /// <param name="color">線の色。</param>
    /// <remarks>24x24 の座標系で他の UI 版の ☰ と同じ太さ・間隔になるよう描く。</remarks>
    private static void DrawMenu(Graphics graphics, int pixels, Color color)
    {
        var scale = pixels / (float)ViewBoxSize;
        var thickness = Math.Max(1f, 1.6f * scale);
        using var pen = new Pen(color, thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        var left = 4.5f * scale;
        var right = 19.5f * scale;
        foreach (var y in (float[])[7f, 12f, 17f])
        {
            graphics.DrawLine(pen, left, y * scale, right, y * scale);
        }
    }

    /// <summary>SVG の <c>path</c> 要素から <c>d</c> 属性（パス データ）を抽出する正規表現。</summary>
    [GeneratedRegex("d=\"(?<path>[^\"]*)\"", RegexOptions.None, 100)]
    private static partial Regex PathDataRegex();
}

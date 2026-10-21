using System.Runtime.InteropServices.WindowsRuntime;
using EsUtil.Platform.Core.Caching;
using EsUtil.Platform.Core.Svg;
using EsUtil.SaveEvidence.App.Core.Icons;
using EsUtil.SaveEvidence.App.WinUI3.Views;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace EsUtil.SaveEvidence.App.WinUI3.Icons;

/// <summary>Icons フォルダの Fluent アイコン（SVG）から画像を生成するヘルパー（WinUI 3 版）。</summary>
/// <remarks>
/// SVG の出所は <see cref="AppIcons"/>（App.Core）が単一所有する（4 つの UI 版で同じファイルを使う）。<br/>
/// 各 SVG は Fluent System Icons 形式の単一 <c>&lt;path d="..."&gt;</c> で構成され、viewBox は 24x24。<br/>
/// <br/>
/// <b>画素へ展開する理由</b>: WinUI 3 の <c>Path</c> / <c>PathIcon</c> へ形状を渡す方法は、本アプリの<br/>
/// 起動経路（ウィンドウ生成の途中）で <c>0x800F1000</c>（インストールされたコンポーネントが検出されない）になり<br/>
/// 実行時に失敗する（実測 2026-10-20）。 画素へ展開すれば XAML の形状を経由しないため、起動の順序に依存しない。<br/>
/// 展開は <see cref="SvgPathRasterizer"/>（自前の走査線塗り）が担い、外部ライブラリを使わない。<br/>
/// 生成結果は容量制限付きのキャッシュで共有する（ナビ・ツールバーのアイコンは表示のたびに参照されるため）。
/// </remarks>
internal static class IconLoader
{
    /// <summary>画像キャッシュの最大保持数。</summary>
    private const int CacheCapacity = 48;

    /// <summary>アイコン色（明色テーマ用）。</summary>
    private const uint ForegroundLight = 0xFF1A1A1A;

    /// <summary>アイコン色（暗色テーマ用）。</summary>
    private const uint ForegroundDark = 0xFFEEEEEE;

    /// <summary>生成済み画像のキャッシュ（ファイル名・サイズ・色で一意）。</summary>
    private static readonly CapacityLimitedCache<(string File, int Size, uint Argb), ImageSource> s_cache =
        new(static k => Rasterize(k.File, k.Size, k.Argb), CacheCapacity, KeyComparer.Instance);

    /// <summary>テーマに応じたアイコン・文字の色を取得する。</summary>
    /// <returns>前景色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>テーマ未確定（ウィンドウ生成前）は明色として扱う。</remarks>
    public static uint ForegroundColor() => AppStyles.IsDarkTheme ? ForegroundDark : ForegroundLight;

    /// <summary>指定 SVG を指定サイズ・指定色で展開した画像を取得する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。 1 未満は 1 として扱う。</param>
    /// <param name="argb">アイコンの色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>展開した画像。 生成結果はキャッシュして共有する。</returns>
    /// <remarks>
    /// 24x24 の viewBox を <paramref name="size"/> へ等方スケールして描画する。<br/>
    /// 生成した画像は本クラスが所有し、呼び出し側は破棄しない（キャッシュから共有されるため）。
    /// </remarks>
    public static ImageSource LoadImage(string fileName, int size, uint argb)
    {
        var length = Math.Max(1, size);
        var key = (File: fileName, Size: length, Argb: argb);

        return s_cache.GetOrAdd(key);
    }

    /// <summary>テーマに合うアイコンを生成する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。</param>
    /// <returns>現在のテーマの前景色で展開した画像。</returns>
    public static ImageSource LoadThemedImage(string fileName, int size)
        => LoadImage(fileName, size, ForegroundColor());

    /// <summary>SVG を画像へ展開する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="size">生成する画像の一辺（ピクセル）。 1 以上。</param>
    /// <param name="argb">描画色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>生成した画像。</returns>
    /// <remarks>
    /// 画素形式は <see cref="WriteableBitmap"/> が要求する BGRA8888 に合わせる<br/>
    /// （ピクセル バッファへそのまま書き込める）。
    /// </remarks>
    private static ImageSource Rasterize(string fileName, int size, uint argb)
    {
        var figures = SvgPathParser.Parse(AppIcons.Source.Load(fileName).PathData[0]);
        var pixels = SvgPathRasterizer.Rasterize(figures, size, argb);

        var image = new WriteableBitmap(size, size);
        using (var target = image.PixelBuffer.AsStream())
        {
            target.Write(pixels);
        }

        image.Invalidate();
        return image;
    }

    /// <summary>キャッシュ キーの比較（ファイル名は序数で比較する）。</summary>
    /// <remarks><c>ConcurrentDictionary</c> の既定の比較子は文字列に文化依存の比較を使うため、明示する。</remarks>
    private sealed class KeyComparer : IEqualityComparer<(string File, int Size, uint Argb)>
    {
        /// <summary>共有の実体。</summary>
        public static readonly KeyComparer Instance = new();

        /// <summary>等値判定。</summary>
        /// <param name="x">比較するキー。</param>
        /// <param name="y">比較するキー。</param>
        /// <returns>等しい場合 true。</returns>
        public bool Equals((string File, int Size, uint Argb) x, (string File, int Size, uint Argb) y)
            => x.Size == y.Size && x.Argb == y.Argb && string.Equals(x.File, y.File, StringComparison.Ordinal);

        /// <summary>ハッシュ値を求める。</summary>
        /// <param name="obj">対象のキー。</param>
        /// <returns>ハッシュ値。</returns>
        public int GetHashCode((string File, int Size, uint Argb) obj)
            => HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.File), obj.Size, obj.Argb);
    }
}

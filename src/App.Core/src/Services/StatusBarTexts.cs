using System.Globalization;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータス バーの「マウスポインタ位置」「画像サイズ」の文言（4 つの UI 版で揃えるための単一所有者）。</summary>
/// <remarks>
/// 接頭辞を UI 版ごとに組み立てると、幅の基準（<see cref="StatusBarMetrics"/>）と実際の表示がずれます。<br/>
/// 幅の基準は<b>接頭辞を含む最も長い値</b>（<c>pt: 8888, 8888</c> / <c>size: 8888 x 8888</c>）です。<br/>
/// 数値は現在のカルチャに依存させません（桁区切りが入ると幅の基準を超えるため）。
/// </remarks>
public static class StatusBarTexts
{
    /// <summary>マウスポインタ位置の接頭辞。</summary>
    public const string PointerPrefix = "pt: ";

    /// <summary>画像サイズの接頭辞。</summary>
    public const string ImageSizePrefix = "size: ";

    /// <summary>値が無いときの表示。</summary>
    public const string NoValueText = "-";

    /// <summary>幅の基準にするマウスポインタ位置の値（最も長い形）。</summary>
    public const string PointerReferenceValueText = "8888, 8888";

    /// <summary>幅の基準にする画像サイズの値（最も長い形）。</summary>
    public const string ImageSizeReferenceValueText = "8888 x 8888";

    /// <summary>画像外（値が無い）ときのマウスポインタ位置の表示テキスト。</summary>
    public const string PointerUnavailableText = PointerPrefix + NoValueText;

    /// <summary>画像が無いときの画像サイズの表示テキスト（空欄）。</summary>
    public const string ImageSizeUnavailableText = "";

    /// <summary>マウスポインタ位置の表示テキストを作る。</summary>
    /// <param name="x">対象座標の X（画像ピクセル）。</param>
    /// <param name="y">対象座標の Y（画像ピクセル）。</param>
    /// <returns>表示テキスト（例: <c>pt: 1920, 1080</c>）。</returns>
    public static string FormatPointer(int x, int y)
        => string.Create(CultureInfo.InvariantCulture, $"{PointerPrefix}{x}, {y}");

    /// <summary>画像サイズの表示テキストを作る。</summary>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <returns>表示テキスト（例: <c>size: 1920 x 1080</c>）。</returns>
    public static string FormatImageSize(int width, int height)
        => string.Create(CultureInfo.InvariantCulture, $"{ImageSizePrefix}{width} x {height}");
}

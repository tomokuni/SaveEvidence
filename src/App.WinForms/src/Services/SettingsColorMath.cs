using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.App.WinForms.Services;

/// <summary>設定に保存する色文字列（<c>#AARRGGBB</c>）と GDI+ の色の相互変換。</summary>
/// <remarks>
/// 設定（<c>Settings</c>）は色と不透明度を統合した <c>#AARRGGBB</c> 形式の文字列で保持する。<br/>
/// <b>形式と解釈は <see cref="DrawingColorExtensions.TryParseHexText"/> が単一所有する</b>
/// （以前は 3 つの UI 版が同じ解析をそれぞれ持っていた）。 本クラスは自 UI の色型との相互変換だけを担う。<br/>
/// <b>失敗時</b>: 解析できない文字列は既定色へフォールバックする（設定の破損で描画が落ちないようにする）。
/// </remarks>
internal static class SettingsColorMath
{
    /// <summary>設定の色文字列を GDI+ の色へ変換する。</summary>
    /// <param name="argb">設定の色文字列（<c>#AARRGGBB</c> / <c>#RRGGBB</c>。 前後の空白と <c>#</c> は省略可能）。 null 可。</param>
    /// <param name="fallback">解析できない場合に使用する色。</param>
    /// <returns>変換した色。 解析できない場合は <paramref name="fallback"/>。</returns>
    public static DrawingColor ToDrawingColor(string? argb, DrawingColor fallback)
        => DrawingColorExtensions.TryParseHexText(argb, out var color) ? color : fallback;

    /// <summary>GDI+ の色を設定の色文字列（<c>#AARRGGBB</c>）へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>設定へ保存する色文字列（大文字の 16 進・先頭に <c>#</c>）。</returns>
    public static string ToArgbString(DrawingColor color) => color.ToHexText();
}
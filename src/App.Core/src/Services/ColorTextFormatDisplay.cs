using EsUtil.Platform.Core.Palette;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>色選択コントロールの入力欄の表記（<see cref="ColorTextFormat"/>）の表示名と選択肢。</summary>
/// <remarks>
/// <b>表示名は本クラスが単一所有する</b>: <see cref="ColorTextFormat"/> は EsUtil（UI 非依存のライブラリ）の型であり
/// 表示名を持たないため、アプリ側の文言はここへ集約する（UI ごとに文言を書き分けない）。<br/>
/// 設定画面は <see cref="All"/> を並べ、選択された値を <c>Models.Settings.ColorTextFormat</c> へ書き戻す。
/// </remarks>
public static class ColorTextFormatDisplay
{
    /// <summary>設定画面へ並べる表記（既定の順序）。</summary>
    public static IReadOnlyList<ColorTextFormat> All { get; } =
        [ColorTextFormat.NameWithAlpha, ColorTextFormat.HexCode];

    /// <summary>表記の表示名を取得する。</summary>
    /// <param name="format">対象の表記。</param>
    /// <returns>表示名（設定画面の選択肢に出す文言）。</returns>
    public static string Get(ColorTextFormat format) => format switch
    {
        ColorTextFormat.HexCode => "#FFFFFFFF（カラーコード）",
        ColorTextFormat.NameWithAlpha => "DarkGray（色名 + 不透明度）",
        _ => format.ToString(),
    };
}

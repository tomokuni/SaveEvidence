using EsUtil.SaveEvidence.App.Core.Enums;
using Microsoft.Win32;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>アプリ共通の外観（面の色・文字色・枠線・アイテムの色）の単一所有。</summary>
/// <remarks>
/// <b>テーマ</b>: 設定のテーマ（ライト / ダーク / システム）に追従する。 「システム」は OS の<br/>
/// アプリ用テーマ（<c>HKCU\...\Themes\Personalize</c> の <c>AppsUseLightTheme</c>）を読んで解決する。<br/>
/// <b>色の値</b>: 他の実装と同じ実測値（ページは白 / 黒、ナビは <c>#E6E6E6</c> / <c>#1F1F1F</c>、<br/>
/// 一覧は <c>#F2F2F2</c> / <c>#2B2B2B</c>）を使う。<br/>
/// <b>面の単位</b>: ここで返すのは DIP 相当の値であり、コントロールは <c>AutoScaleMode = Dpi</c> で<br/>
/// 実際の DPI へ拡大する。
/// </remarks>
internal static class AppStyles
{
    /// <summary>アイテム（一覧のサムネイル・ナビ項目）の角丸の半径（DIP）。</summary>
    public const int CornerRadius = 4;

    /// <summary>枠線の太さ（DIP）。</summary>
    public const float BorderWidth = 1f;

    /// <summary>選択中の項目を示す枠の太さ（DIP）。</summary>
    public const float SelectedBorderWidth = 2f;

    /// <summary>明色テーマを使用中かどうか。</summary>
    private static bool s_isLight = true;

    /// <summary>テーマが変わったことを通知する。</summary>
    /// <remarks>画像アイコンやコントロールへ直接設定した色を持つページが購読して描き直す。</remarks>
    public static event EventHandler? ThemeChanged;

    /// <summary>ページ（ウィンドウ）の背景色を取得する。</summary>
    public static Color PageBackground => s_isLight ? Color.FromArgb(0xFF, 0xFF, 0xFF) : Color.FromArgb(0x00, 0x00, 0x00);

    /// <summary>ナビゲーション パネルの背景色を取得する。</summary>
    public static Color NavBackground => s_isLight ? Color.FromArgb(0xE6, 0xE6, 0xE6) : Color.FromArgb(0x1F, 0x1F, 0x1F);

    /// <summary>一覧の背景色を取得する。</summary>
    public static Color ListBackground => s_isLight ? Color.FromArgb(0xF2, 0xF2, 0xF2) : Color.FromArgb(0x2B, 0x2B, 0x2B);

    /// <summary>文字の色を取得する。</summary>
    public static Color Foreground => s_isLight ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.FromArgb(0xF0, 0xF0, 0xF0);

    /// <summary>枠線の色を取得する。</summary>
    public static Color Border => s_isLight ? Color.FromArgb(0xC8, 0xC8, 0xC8) : Color.FromArgb(0x55, 0x55, 0x55);

    /// <summary>選択中のナビ項目の背景色を取得する。</summary>
    public static Color NavItemSelected => s_isLight ? Color.FromArgb(0xD0, 0xD0, 0xD0) : Color.FromArgb(0x3A, 0x3A, 0x3A);

    /// <summary>ナビ項目にカーソルが重なったときの背景色を取得する。</summary>
    public static Color NavItemHover => s_isLight ? Color.FromArgb(0xDC, 0xDC, 0xDC) : Color.FromArgb(0x2A, 0x2A, 0x2A);

    /// <summary>一覧の項目にカーソルが重なったときの背景色を取得する。</summary>
    public static Color ItemHover => s_isLight ? Color.FromArgb(0xE0, 0xE0, 0xE0) : Color.FromArgb(0x3A, 0x3A, 0x3A);

    /// <summary>強調（選択・リンク）の色を取得する。</summary>
    public static Color Accent => Color.FromArgb(0x00, 0x78, 0xD4);

    /// <summary>ツールバーとステータスバーの背景色を取得する。</summary>
    public static Color BarBackground => s_isLight ? Color.FromArgb(0xF7, 0xF7, 0xF7) : Color.FromArgb(0x14, 0x14, 0x14);

    /// <summary>使用する UI フォントを取得する。</summary>
    /// <remarks>システムのメッセージ ボックス フォントを共用する（ページごとにフォントを作らない）。</remarks>
    public static Font UiFont => SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

    /// <summary>テーマを適用する。</summary>
    /// <param name="mode">適用するテーマ モード。</param>
    /// <remarks>「システム」は OS の設定を読んで解決する。 変更は <see cref="ThemeChanged"/> で通知する。</remarks>
    public static void ApplyTheme(ThemeMode mode)
    {
        var isLight = mode switch
        {
            ThemeMode.Light => true,
            ThemeMode.Dark => false,
            _ => IsSystemLightTheme(),
        };

        if (s_isLight == isLight)
        {
            return;
        }

        s_isLight = isLight;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>OS のアプリ用テーマが明色かどうかを判定する。</summary>
    /// <returns>明色の場合は true。 読めない場合は true（既定は明色）。</returns>
    /// <remarks>
    /// テーマの設定は利用者ごとのレジストリに保存される。 読めない場合は例外にせず既定値を使う<br/>
    /// （テーマが読めないことで起動できなくならないようにする）。
    /// </remarks>
    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception ex) when (EsUtil.Platform.Core.BoundaryCatch.IsRecoverable(ex))
        {
            // レジストリを読めない環境（権限・ポリシー）では明色として扱う
            return true;
        }
    }
}

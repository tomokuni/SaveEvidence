using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>アプリ共通の外観（面の色・文字色・枠線・アイテムの色）の単一所有。</summary>
/// <remarks>
/// <b>テーマ</b>: 本 UI 版（WinForms）は<b>ダークテーマを使用しない</b>（常に明色）。 ネイティブ コントロール<br/>
/// （タイトル バー・スクロールバー・コンボ ボックス等）の配色を標準機能では揃えられず、部分的な暗色化は<br/>
/// かえって読みにくくなるため、明色で統一する（設定ファイルの <c>ThemeMode</c> は読むが反映しない）。<br/>
/// <b>色の値</b>: 4 つの UI 版で共通に使う色（ページ・ナビ・画像領域）は <see cref="AppPalette"/>（App.Core）が<br/>
/// 単一所有する。 本クラスは明色の値を WinForms の色型へ変換して返す。<br/>
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

    /// <summary>ページ（ウィンドウ）の背景色を取得する。</summary>
    public static Color PageBackground => ToColor(AppPalette.Page(isDark: false));

    /// <summary>ナビゲーション パネルの背景色を取得する。</summary>
    public static Color NavBackground => ToColor(AppPalette.NavPanel(isDark: false));

    /// <summary>画像領域（プレビューの下地）の背景色を取得する。</summary>
    public static Color ImageAreaBackground => ToColor(AppPalette.ImageArea(isDark: false));

    /// <summary>一覧の背景色を取得する。</summary>
    /// <remarks>色は <see cref="AppPalette"/>（App.Core）が単一所有する（4 つの UI 版で同じ色にする）。</remarks>
    public static Color ListBackground => ToColor(AppPalette.ListBackground(isDark: false));

    /// <summary>ボタンの面の色を取得する。</summary>
    /// <remarks>色は <see cref="AppPalette"/>（App.Core）が単一所有する（WinUI 3 の既定のボタンの実測値）。</remarks>
    public static Color ButtonFace => ToColor(AppPalette.ButtonFace(isDark: false));

    /// <summary>ボタンの枠線の色を取得する。</summary>
    /// <remarks>色は <see cref="AppPalette"/>（App.Core）が単一所有する。</remarks>
    public static Color ButtonBorder => ToColor(AppPalette.ButtonBorder(isDark: false));

    /// <summary>選択中のトグル ボタンの面の色を取得する。</summary>
    /// <remarks>色は <see cref="AppPalette"/>（App.Core）が単一所有する。</remarks>
    public static Color SelectedButtonFace => ToColor(AppPalette.ButtonCheckedFace(isDark: false));

    /// <summary>選択中のトグル ボタンの枠線の色を取得する。</summary>
    /// <remarks>色は <see cref="AppPalette"/>（App.Core）が単一所有する。</remarks>
    public static Color SelectedButtonBorder => ToColor(AppPalette.ButtonCheckedBorder(isDark: false));

    /// <summary>選択中のトグル ボタンの文字色を取得する。</summary>
    /// <remarks>色は <see cref="AppPalette"/>（App.Core）が単一所有する。</remarks>
    public static Color SelectedButtonForeground => ToColor(AppPalette.ButtonCheckedForeground(isDark: false));

    /// <summary>ボタンにカーソルが重なったときの面の色を取得する。</summary>
    public static Color ButtonHover => ToColor(AppPalette.ToolbarButtonBackground(isDark: false, isPressed: false));

    /// <summary>ボタンを押している間の面の色を取得する。</summary>
    public static Color ButtonPressed => ToColor(AppPalette.ToolbarButtonBackground(isDark: false, isPressed: true));

    /// <summary>文字の色を取得する。</summary>
    public static Color Foreground => Color.FromArgb(0x1A, 0x1A, 0x1A);

    /// <summary>枠線の色を取得する。</summary>
    public static Color Border => Color.FromArgb(0xC8, 0xC8, 0xC8);

    /// <summary>通知メッセージ（「キャプチャしました」など）の文字色を取得する。</summary>
    /// <remarks>他の UI 版（WinUI3 / MewUI / AvaloniaUI）と同じ青（<c>#007ACC</c>）にする。</remarks>
    public static Color Announce => Color.FromArgb(0x00, 0x7A, 0xCC);

    /// <summary>選択中のナビ項目の背景色を取得する。</summary>
    public static Color NavItemSelected => Color.FromArgb(0xD0, 0xD0, 0xD0);

    /// <summary>ナビ項目にカーソルが重なったときの背景色を取得する。</summary>
    public static Color NavItemHover => Color.FromArgb(0xDC, 0xDC, 0xDC);

    /// <summary>一覧の項目にカーソルが重なったときの背景色を取得する。</summary>
    public static Color ItemHover => ToColor(ThumbnailTileMetrics.HoverBackground(isDark: false));

    /// <summary>強調（選択・リンク）の色を取得する。</summary>
    public static Color Accent => Color.FromArgb(0x00, 0x78, 0xD4);

    /// <summary>ツールバーとステータスバーの背景色を取得する。</summary>
    /// <remarks>
    /// 4 UI 版の他のページと同じく、ページの下地をそのまま見せる（ツールバーだけ色を変えない）。
    /// </remarks>
    public static Color BarBackground => PageBackground;

    /// <summary>使用する UI フォントを取得する。</summary>
    /// <remarks>システムのメッセージ ボックス フォントを共用する（ページごとにフォントを作らない）。</remarks>
    public static Font UiFont => SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

    /// <summary>ツールバーの文字サイズを使うフォントを取得する。</summary>
    /// <remarks>DIP からポイントへ換算したフォントを 1 つだけ生成して共用する。</remarks>
    public static Font ToolbarFont => s_toolbarFont;

    private static readonly Font s_toolbarFont = new(
        UiFont.FontFamily, (float)(ToolbarMetrics.FontSize * 72.0 / 96.0), UiFont.Style);

    /// <summary>使用する UI フォントの太字を取得する。</summary>
    /// <value>現在の画像（現在プレビューに表示中の履歴）の名前を強調するために使う。</value>
    /// <remarks>呼ぶたびに <c>Font</c> を作ると GDI ハンドルが増えるため、1 つだけ生成して共用する。</remarks>
    public static Font BoldUiFont => s_boldUiFont;

    private static readonly Font s_boldUiFont =
        new(UiFont.FontFamily, UiFont.Size, FontStyle.Bold);

    /// <summary>設定ページのフォントを取得する。</summary>
    /// <value>AvaloniaUI 版と同じ 14 DIP を pt へ換算したフォント。</value>
    /// <remarks>
    /// <b>1 つだけ生成して共用する</b>: 呼ぶたびに <c>Font</c> を作ると GDI ハンドルが増える。<br/>
    /// <c>Font</c> は pt 単位のため、DIP（96 DPI 基準）から <c>× 72 ÷ 96</c> で換算する。
    /// </remarks>
    public static Font SettingsFont => s_settingsFont;

    private static readonly Font s_settingsFont = new(
        UiFont.FontFamily, (float)(SettingsMetrics.FontSize * 72.0 / 96.0), UiFont.Style);

    /// <summary>設定ページの補足（例・説明）のフォントを取得する。</summary>
    /// <value>AvaloniaUI 版と同じ 11 DIP を pt へ換算したフォント。</value>
    /// <remarks>呼ぶたびに <c>Font</c> を作ると GDI ハンドルが増えるため、1 つだけ生成して共用する。</remarks>
    public static Font SettingsHintFont => s_settingsHintFont;

    private static readonly Font s_settingsHintFont = new(
        UiFont.FontFamily, (float)(SettingsMetrics.HintFontSize * 72.0 / 96.0), UiFont.Style);

    /// <summary>設定ページの入力コントロールの標準の高さ（ピクセル）を取得する。</summary>
    /// <value>設定フォントの行高 + 上下の余白。</value>
    /// <remarks>ボタン・入力欄の高さを固定値にすると、文字が大きくなったときに文字が欠ける。</remarks>
    public static int SettingsControlHeight => Math.Max(26, (int)Math.Ceiling(s_settingsFont.GetHeight()) + 10);

    /// <summary>ステータス バーのフォントを取得する。</summary>
    /// <value>4 UI 版で共通の文字サイズ（<see cref="StatusBarMetrics.FontSize"/>）を pt へ換算したフォント。</value>
    /// <remarks>
    /// <b>1 つだけ生成して共用する</b>: 呼ぶたびに <c>Font</c> を作ると GDI ハンドルが増える。<br/>
    /// <c>Font</c> は pt 単位のため、DIP（96 DPI 基準）から <c>× 72 ÷ 96</c> で換算する。
    /// </remarks>
    public static Font StatusBarFont => s_statusBarFont;

    private static readonly Font s_statusBarFont = new(
        UiFont.FontFamily, (float)(StatusBarMetrics.FontSize * 72.0 / 96.0), UiFont.Style);

    /// <summary>32 ビットの色値（0xAARRGGBB）を WinForms の色へ変換する。</summary>
    /// <param name="argb">色（0xAARRGGBB）。</param>
    /// <returns>WinForms の色。</returns>
    private static Color ToColor(uint argb)
        => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}

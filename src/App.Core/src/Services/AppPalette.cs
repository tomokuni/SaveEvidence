namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>アプリの配色（テーマ別）のうち、4 つの UI 版で共通に使う色。</summary>
/// <remarks>
/// 色は <c>0xAARRGGBB</c> の 32 ビット値で保持する（UI 非依存の形で単一所有するため）。<br/>
/// 各 UI 版は自分の色型へ変換して使う（Avalonia の <c>Color</c> / MewUI の <c>Color</c> / WinUI 3 の
/// <c>Windows.UI.Color</c> / WinForms の <c>System.Drawing.Color</c>）。<br/>
/// <br/>
/// <b>テーマ別にする理由</b>: 明色で指定された色を暗色でもそのまま使うと、画面全体がまぶしくなる<br/>
/// （暗色テーマの意味が無くなる）。 暗色では同じ役割の濃い色を使う（実測で調整した値）。
/// </remarks>
public static class AppPalette
{
    /// <summary>画像領域（プレビューの下地）の背景色（明色）。</summary>
    public const uint ImageAreaLight = 0xFFFAFAFA;

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色（明色）。</summary>
    public const uint ListBackgroundLight = 0xFFF2F2F2;

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色（暗色）。</summary>
    public const uint ListBackgroundDark = 0xFF2B2B2B;

    /// <summary>画像領域（プレビューの下地）の背景色（暗色）。</summary>
    public const uint ImageAreaDark = 0xFF202020;

    /// <summary>ナビゲーション パネルの背景色（明色）。</summary>
    public const uint NavPanelLight = 0xFFE6E6E6;

    /// <summary>ナビゲーション パネルの背景色（暗色）。 画像領域より少し明るくして境目を見せる。</summary>
    public const uint NavPanelDark = 0xFF2B2B2B;

    /// <summary>ページ（ウィンドウ）の下地の色（明色）。</summary>
    public const uint PageLight = 0xFFFFFFFF;

    /// <summary>ページ（ウィンドウ）の下地の色（暗色）。</summary>
    public const uint PageDark = 0xFF202020;

    /// <summary>ページ（ウィンドウ）の下地の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// ウィンドウの下地（透過部分の後ろ）に使う。 暗色で白のままだと、テーマを切り替えても<br/>
    /// ツールバーやステータス バーの背景だけが明るいまま残り、色が混ざって見える（実測）。
    /// </remarks>
    public static uint Page(bool isDark) => isDark ? PageDark : PageLight;

    /// <summary>画像領域の背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint ImageArea(bool isDark) => isDark ? ImageAreaDark : ImageAreaLight;

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// AvaloniaUI 版の <c>ListBox</c> の背景（<c>SystemControlBackgroundChromeMediumLowBrush</c>）と同じ値。<br/>
    /// <b>4 UI 版で同じ色にする理由</b>: 影は半透明の黒のため、下地が白いと薄く見え、灰色だと濃く見える<br/>
    /// （同じ不透明度でも見え方が変わる）。 一覧の下地を揃えると影の見え方も揃う。
    /// </remarks>
    public static uint ListBackground(bool isDark) => isDark ? ListBackgroundDark : ListBackgroundLight;

    /// <summary>ナビゲーション パネルの背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint NavPanel(bool isDark) => isDark ? NavPanelDark : NavPanelLight;

    /// <summary>ナビゲーション項目の選択中の背景色（薄いグレー）を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>WinUI 3 の <c>NavigationViewItem</c> の選択状態と同じ値（実測 2026-10-17）。</remarks>
    public static uint NavItemSelected(bool isDark) => isDark ? 0xFF373737 : 0xFFDEDEDE;

    /// <summary>ナビゲーション項目の選択を示す左端のマークの色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>WinUI 3 の選択マークと同じ値（明色 <c>#0067C0</c> / 暗色 <c>#4CC2FF</c>。 実測 2026-10-17）。</remarks>
    public static uint NavItemSelectionAccent(bool isDark) => isDark ? 0xFF4CC2FF : 0xFF0067C0;

    /// <summary>ツールバーのアイコン ボタンの角丸（DIP）。</summary>
    public const double ToolbarButtonCornerRadius = 4;

    /// <summary>ツールバーのアイコン ボタンの背景色（マウスを重ねたとき・明色）。</summary>
    public const uint ToolbarButtonHoverLight = 0xFFE9E9E9;

    /// <summary>ツールバーのアイコン ボタンの背景色（マウスを重ねたとき・暗色）。</summary>
    public const uint ToolbarButtonHoverDark = 0xFF3A3A3A;

    /// <summary>ツールバーのアイコン ボタンの背景色（押している間・明色）。 ホバーより濃い。</summary>
    public const uint ToolbarButtonPressedLight = 0xFFDBDBDB;

    /// <summary>ツールバーのアイコン ボタンの背景色（押している間・暗色）。 ホバーより明るい。</summary>
    public const uint ToolbarButtonPressedDark = 0xFF4E4E4E;

    /// <summary>ツールバーのアイコン ボタンの背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <param name="isPressed">押している間かどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。 マウスが重なっていないときは透明。</returns>
    /// <remarks>
    /// 通常時は下地を塗らない（アイコンだけが見える）。 マウスを重ねると薄いグレーの角丸な矩形を出し、<br/>
    /// 押している間はさらに濃くする（WinUI 3 のアイコン ボタンと同じ段階）。<br/>
    /// 暗色テーマでは「濃く」を「明るく」で表す（暗い下地の上では明るい方が押下として目立つ）。
    /// </remarks>
    public static uint ToolbarButtonBackground(bool isDark, bool isPressed)
        => isPressed
            ? (isDark ? ToolbarButtonPressedDark : ToolbarButtonPressedLight)
            : (isDark ? ToolbarButtonHoverDark : ToolbarButtonHoverLight);

    /// <summary>画像領域の背景色を <see cref="System.Drawing.Color"/> で取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色。</returns>
    /// <remarks>
    /// プレビューの下地（<c>ZoomViewAppearance.BackgroundColor</c>）は UI 非依存の <see cref="System.Drawing.Color"/> で<br/>
    /// 保持されるため、変換を各 UI 版へ書き分けずに済むよう本メソッドが担う。
    /// </remarks>
    public static System.Drawing.Color ImageAreaDrawingColor(bool isDark)
    {
        var argb = ImageArea(isDark);
        return System.Drawing.Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }
}


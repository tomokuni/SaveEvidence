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
    public const uint ImageAreaLight = 0xFFF7F7F7;

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色（明色）。</summary>
    public const uint ListBackgroundLight = 0xFFF2F2F2;

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色（暗色）。</summary>
    public const uint ListBackgroundDark = 0xFF2B2B2B;

    /// <summary>画像領域（プレビューの下地）の背景色（暗色）。</summary>
    public const uint ImageAreaDark = 0xFF202020;

    /// <summary>ナビゲーション パネルの背景色（明色）。</summary>
    public const uint NavPanelLight = 0xFFF0F0F0;

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

    /// <summary>文字とアイコンの前景色（明色テーマ）。</summary>
    public const uint ForegroundLight = 0xFF1A1A1A;

    /// <summary>文字とアイコンの前景色（暗色テーマ）。</summary>
    public const uint ForegroundDark = 0xFFEEEEEE;

    /// <summary>文字とアイコンの前景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// <b>4 UI 版で同じ値を使う理由</b>: 各 UI のテーマの既定色はそれぞれ違う（WinUI 3 と AvaloniaUI は
    /// 明色で <c>#000000</c>、WinForms はコントロールの既定色）。 同じアプリの同じ画面に見えるよう、
    /// アプリの前景色を 1 か所で決める（実測 2026-11-09: UI 版ごとに文字の黒さが違って見えた）。
    /// </remarks>
    public static uint Foreground(bool isDark) => isDark ? ForegroundDark : ForegroundLight;

    /// <summary>枠線（区切り・カードの縁）の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint Border(bool isDark) => isDark ? 0xFF5A5A5A : 0xFFC8C8C8;

    /// <summary>ナビゲーション項目にカーソルが重なったときの背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint NavItemHover(bool isDark) => isDark ? 0xFF30343C : 0xFFE5E9F0;

    /// <summary>ウィンドウを閉じるボタンにカーソルが重なったときの背景色を取得する。</summary>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>テーマによらず赤にする（閉じる操作の意味を色でも示す。 Windows の標準と同じ）。</remarks>
    public const uint CloseButtonHover = 0xFFC42B1C;

    /// <summary>メニュー（ポップアップ）の下地の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint MenuBackground(bool isDark) => isDark ? 0xFF323232 : 0xFFFCFCFC;

    /// <summary>メニュー（ポップアップ）の縁の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint MenuBorder(bool isDark) => isDark ? 0xFF5A5A5A : 0xFFC8C8C8;

    /// <summary>カード（設定のまとまり）の縁の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint CardBorder(bool isDark) => isDark ? 0xFF5A5A5A : 0xFFCCCCCC;

    /// <summary>強調（選択・リンク）の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public static uint Accent(bool isDark) => isDark ? 0xFF4CC2FF : 0xFF0078D4;

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

    /// <summary>リンク（青字）の文字色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// アクセント色と同じ値（明色 <c>#0067C0</c> / 暗色 <c>#4CC2FF</c>）を使う<br/>
    /// （暗色の下地では濃い青が沈むため、明るい青へ切り替える）。
    /// </remarks>
    public static uint Link(bool isDark) => isDark ? 0xFF4CC2FF : 0xFF0067C0;

    /// <summary>通知メッセージ（「保存しました」等）の文字色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// 4 UI 版で同じ青（<c>#007ACC</c>）にする。 暗色では明るい青へ切り替える<br/>
    /// （濃い青は暗い下地に沈む）。
    /// </remarks>
    public static uint Announce(bool isDark) => isDark ? 0xFF4CC2FF : 0xFF007ACC;

    /// <summary>無効（操作できない）ボタンのアイコンの色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// **アイコンは無効でも色が変わらない**（画像のためテーマの不透明度が効かない）。<br/>
    /// 無効であることが分かるよう、地色に近い灰色で描く。
    /// </remarks>
    public static uint DisabledForeground(bool isDark) => isDark ? 0xFF6A6A6A : 0xFFA0A0A0;

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
    /// <returns>色（<c>0xAARRGGBB</c>）。 マウスが重なっていないときは <see cref="ButtonFace"/> の上に重ねる色。</returns>
    /// <remarks>
    /// 通常時は <see cref="ButtonFace"/> の面を塗り、マウスを重ねると薄いグレー、押している間はさらに濃くする<br/>
    /// （WinUI 3 のアイコン ボタンと同じ段階）。<br/>
    /// 暗色テーマでは「濃く」を「明るく」で表す（暗い下地の上では明るい方が押下として目立つ）。
    /// </remarks>
    public static uint ToolbarButtonBackground(bool isDark, bool isPressed)
        => isPressed
            ? (isDark ? ToolbarButtonPressedDark : ToolbarButtonPressedLight)
            : (isDark ? ToolbarButtonHoverDark : ToolbarButtonHoverLight);

    /// <summary>ボタンの面の色（通常時）を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// <b>WinUI 3 の既定のボタンと同じ見え方にする</b>: WinUI 3 の面は半透明の白<br/>
    /// （明色 <c>#B3FFFFFF</c> / 暗色 <c>#0FFFFFFF</c>）であり、下地（明色 <c>#FFFFFF</c> / 暗色 <c>#202020</c>）<br/>
    /// と重ねた結果の実測値（明色 <c>#FFFFFF</c> / 暗色 <c>#2D2D2D</c>）を opaque な値として持つ<br/>
    /// （半透明のまま配ると、下地が変わったときに 4 UI 版で見え方がずれる）。
    /// </remarks>
    public static uint ButtonFace(bool isDark) => isDark ? 0xFF2D2D2D : 0xFFFFFFFF;

    /// <summary>ボタンの枠線の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>WinUI 3 の既定のボタンの枠線（下端）の実測値（明色 <c>#D6D6D6</c> / 暗色 <c>#303030</c>）。</remarks>
    public static uint ButtonBorder(bool isDark) => isDark ? 0xFF303030 : 0xFFD6D6D6;

    /// <summary>選択中（チェック済み）のトグル ボタンの面の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>WinUI 3 の既定のトグル ボタンの選択時（アクセント色）の実測値（明色 <c>#0067C0</c> / 暗色 <c>#4CC2FF</c>）。</remarks>
    public static uint ButtonCheckedFace(bool isDark) => isDark ? 0xFF4CC2FF : 0xFF0067C0;

    /// <summary>選択中（チェック済み）のトグル ボタンの枠線の色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>WinUI 3 の既定のトグル ボタンの選択時の枠線（下端）の実測値（明色 <c>#003E73</c> / 暗色 <c>#42A7DC</c>）。</remarks>
    public static uint ButtonCheckedBorder(bool isDark) => isDark ? 0xFF42A7DC : 0xFF003E73;

    /// <summary>選択中（チェック済み）のトグル ボタンの文字色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>面の色との組み合わせで決める（明色は濃い青の面に白 / 暗色は明るい青の面に黒）。</remarks>
    public static uint ButtonCheckedForeground(bool isDark) => isDark ? 0xFF000000 : 0xFFFFFFFF;

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


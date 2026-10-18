using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>アプリ共通のサイズ・余白・色と、テーマの適用（WinUI 3 版）。</summary>
/// <remarks>
/// <b>役割</b>: 各ページが使う寸法と色を 1 箇所に集約する（他の UI 版と同じ値を使い、見た目を揃える）。<br/>
/// <b>テーマ</b>: WinUI 3 のテーマはルート要素の <c>RequestedTheme</c> で切り替える（実行時に変更できる）。<br/>
/// テーマが変わったことは <see cref="ThemeChanged"/> で通知し、スタイルで表せない値（ラスタライズ済みの<br/>
/// アイコン色・Skia で描く下地色）を持つページが描き直す。<br/>
/// <b>定数の所有者</b>: 寸法・余白の値は本クラスが単一所有する（ページ側に数値を散らさない）。
/// </remarks>
internal static class AppStyles
{
    /// <summary>ツールバーの上余白（DIP）。</summary>
    public const double ToolbarMarginTop = 6;

    /// <summary>ツールバーの下余白（DIP）。</summary>
    public const double ToolbarMarginBottom = 2;

    /// <summary>ツールバーの右余白（DIP）。 ウィンドウ操作ボタンと重ならないようにする。</summary>
    public const double ToolbarMarginRight = 140;

    /// <summary>ツールバーの行の高さ（DIP）。 中身（ボタン 30）より少しだけ広げ、垂直方向中央揃えにする。</summary>
    public const double ToolbarRowHeight = 34;

    /// <summary>ツールバーのボタンの高さ（DIP）。</summary>
    public const double ToolbarButtonHeight = 30;

    /// <summary>ツールバーの左右の内側余白（DIP）。</summary>
    public const double ToolbarPaddingX = 8;

    /// <summary>ツールバーの上下の内側余白（DIP）。</summary>
    public const double ToolbarPaddingY = 4;

    /// <summary>ステータス バーの行の高さ（DIP）。 中身（スライダ 32・ボタン 20）より少しだけ広げる。</summary>
    /// <remarks>
    /// <b>32 にする理由</b>: スライダの標準テンプレートは高さ 32 を要求し、トラックはその中央に描かれる。<br/>
    /// 行をそれより低くするとスライダが上端で切れ、トラックがボタンの中心より下へずれる（実測 2026-10-17）。<br/>
    /// 行を 32 にすると、ボタン（20・垂直中央揃え）とトラックの中心が一致する。
    /// </remarks>
    public const double StatusBarRowHeight = 32;

    /// <summary>ステータス バーのボタンの高さ（DIP）。</summary>
    public const double StatusButtonHeight = 20;

    /// <summary>ステータス バーのスライダの高さ（DIP）。 標準テンプレートの要求値に合わせる（トラックが中央に来る）。</summary>
    public const double StatusSliderHeight = 32;

    /// <summary>ステータス バーの左右の内側余白（DIP）。</summary>
    public const double StatusPaddingX = 4;

    /// <summary>ステータス バーの上下の内側余白（DIP）。</summary>
    public const double StatusPaddingY = 1;

    /// <summary>一覧のステータス バーの上下の内側余白（DIP）。</summary>
    public const double ListStatusPaddingY = 2;

    /// <summary>ステータス バーのボタンの最小幅（DIP）。</summary>
    public const double StatusButtonMinWidth = 22;

    /// <summary>ステータス バーのアイコンの一辺（DIP）。</summary>
    public const double StatusIconSize = 14;

    /// <summary>ステータス バーの文字サイズ（DIP）。</summary>
    public const double StatusFontSize = 12;

    /// <summary>ナビゲーション パネルの幅（展開時・DIP）。</summary>
    /// <remarks>幅の根拠（アイコン + 最も長い項目名 + 余白）は <see cref="NavPanelMetrics"/>（App.Core）が単一所有する。</remarks>
    public const double NavPanelExpandedWidth = NavPanelMetrics.ExpandedWidth;

    /// <summary>ナビゲーション パネルの幅（折りたたみ時・DIP）。</summary>
    public const double NavPanelCompactWidth = NavPanelMetrics.CompactWidth;

    /// <summary>ナビゲーション項目の文字サイズ（DIP）。 4 つの UI 版で共通。</summary>
    public const double NavItemFontSize = NavPanelMetrics.ItemFontSize;

    /// <summary>ウィンドウの最小幅（DIP）。 4 つの UI 版で共通。</summary>
    /// <remarks>値は <see cref="WindowMetrics"/>（App.Core）が単一所有する。</remarks>
    public const double MinWindowWidth = WindowMetrics.MinimumWidth;

    /// <summary>ウィンドウの最小の高さ（DIP）。 4 つの UI 版で共通。</summary>
    /// <remarks>値は <see cref="WindowMetrics"/>（App.Core）が単一所有する。</remarks>
    public const double MinWindowHeight = WindowMetrics.MinimumHeight;

    /// <summary>自前のタイトル バーの高さ（DIP）。 他の UI 版と同じ値。</summary>
    public const double TitleBarHeight = 32;

    /// <summary>タイトル バーのウィンドウ操作ボタンの幅（DIP）。</summary>
    public const double WindowButtonWidth = 46;

    /// <summary>ウィンドウ操作ボタンのアイコンの文字サイズ（DIP）。</summary>
    public const double CaptionGlyphSize = 10;

    /// <summary>ウィンドウ操作ボタンのアイコンのフォント（Segoe Fluent Icons。 旧 OS は MDL2 へフォールバックする）。</summary>
    public const string CaptionFontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets";

    /// <summary>最小化のアイコン（Segoe Fluent Icons の文字コード）。</summary>
    public const string CaptionGlyphMinimize = "\uE921";

    /// <summary>最大化のアイコン。</summary>
    public const string CaptionGlyphMaximize = "\uE922";

    /// <summary>元のサイズに戻すアイコン。</summary>
    public const string CaptionGlyphRestore = "\uE923";

    /// <summary>閉じるのアイコン。</summary>
    public const string CaptionGlyphClose = "\uE8BB";

    /// <summary>閉じるボタンのホバー背景（他の UI 版と同じ赤）。</summary>
    public static Windows.UI.Color CloseButtonHoverBackground => Windows.UI.Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C);

    /// <summary>設定ページのグループ名（セクション見出し）の文字サイズ（DIP）。</summary>
    public const double SectionHeaderFontSize = SettingsMetrics.FontSize;

    /// <summary>ツールバーのアイコンの一辺（DIP）。</summary>
    public const double ToolbarIconSize = NavPanelMetrics.IconSize;

    /// <summary>ナビゲーション項目のアイコンの一辺（DIP）。</summary>
    public const double NavIconSize = NavPanelMetrics.IconSize;

    /// <summary>ナビゲーション パネルの文字サイズ（見出しのアプリ名。 DIP）。</summary>
    public const double NavTitleFontSize = 14;

    /// <summary>ナビゲーション パネルのフッター（UI 名とバージョン）の文字サイズ（DIP）。</summary>
    public const double NavVersionFontSize = 11;

    /// <summary>アイコンをラスタライズする解像度（ピクセル）。 高 DPI でも鮮明になるよう余裕を持たせる。</summary>
    public const int IconRasterSize = 32;

    /// <summary>サムネイル キャッシュの最小保持数。</summary>
    public const int ThumbnailCacheCapacity = 64;

    /// <summary>一覧のアイテム間の余白（DIP）。</summary>
    public const double ListItemSpacing = 8;

    /// <summary>メニューの上端の位置（DIP。 ツールバーの直下）。</summary>
    public const double MenuTop = ToolbarMarginTop + ToolbarButtonHeight;

    /// <summary>テーマが変わったときに発生するイベント。</summary>
    /// <remarks>
    /// ラスタライズ済みのアイコンや Skia で描く下地色を持つページが購読し、描き直す。
    /// </remarks>
    public static event EventHandler? ThemeChanged;

    /// <summary>現在のテーマが暗色かどうかを取得する。</summary>
    /// <value>暗色テーマが適用されている場合は true。 未適用（ウィンドウ生成前）は false。</value>
    public static bool IsDarkTheme { get; private set; }

    /// <summary>ナビゲーション パネルの背景色を取得する。</summary>
    /// <value>現在のテーマの色。 値は <see cref="AppPalette"/>（App.Core）が単一所有する。</value>
    /// <remarks>
    /// <c>NavigationView</c> のペイン背景はテーマ リソースから解決されるが、ルートの <c>RequestedTheme</c> を<br/>
    /// 実行時に変えてもペイン背景のブラシが追随しない（実測: 暗色にしても白のまま）。<br/>
    /// そのため <c>NavigationViewDefaultPaneBackground</c> を自前の色で上書きする。
    /// </remarks>
    public static SolidColorBrush NavPanelBackground => new(ToColor(AppPalette.NavPanel(IsDarkTheme)));

    /// <summary>画像領域（プレビューの下地）の背景色を取得する。</summary>
    /// <value>現在のテーマの色。 値は <see cref="AppPalette"/>（App.Core）が単一所有する。</value>
    public static SolidColorBrush ImageAreaBackground => new(ToColor(AppPalette.ImageArea(IsDarkTheme)));

    /// <summary>32 ビットの色値（0xAARRGGBB）を WinUI の色へ変換する。</summary>
    /// <param name="argb">色（0xAARRGGBB）。</param>
    /// <returns>WinUI の色。</returns>
    public static Windows.UI.Color ToColor(uint argb)
        => Windows.UI.Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    /// <summary>テーマを適用する。</summary>
    /// <param name="root">テーマを設定するルート要素（ウィンドウの内容）。 null の場合は色の判定だけを行う。</param>
    /// <param name="mode">適用するテーマの指定。</param>
    /// <remarks>
    /// <c>System</c> はルート要素の <c>ElementTheme.Default</c> に対応し、OS の設定へ追従する。<br/>
    /// 適用後の実効テーマ（<c>ActualTheme</c>）から暗色かどうかを判定し、変化していれば
    /// <see cref="ThemeChanged"/> を発火する。<br/>
    /// <b><c>ActualTheme</c> だけに頼らない理由</b>: 要素が読み込まれる前は <c>ActualTheme</c> が<br/>
    /// 既定（明色）を返すため、暗色で起動すると判定が明色のままになり、ナビゲーション パネルなどが<br/>
    /// 明色で塗られる（テーマと逆になる。 実測）。 明示指定は要求値で判定し、<c>System</c> だけ<br/>
    /// OS のアプリ用テーマ（<c>Application.RequestedTheme</c>）で判定する。
    /// </remarks>
    public static void ApplyTheme(FrameworkElement? root, ThemeMode mode)
    {
        var requested = mode switch
        {
            ThemeMode.Light => ElementTheme.Light,
            ThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        if (root is not null)
        {
            root.RequestedTheme = requested;
        }

        var isDark = mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => IsSystemDarkTheme(),
        };

        if (isDark == IsDarkTheme)
        {
            return;
        }

        IsDarkTheme = isDark;
        RaiseThemeChanged();
    }

    /// <summary>テーマが変わったことを購読者へ通知する。</summary>
    /// <remarks>イベントの発火は本メソッド 1 箇所へ集約する（発火漏れと二重発火を防ぐ）。</remarks>
    private static void RaiseThemeChanged() => ThemeChanged?.Invoke(null, EventArgs.Empty);

    /// <summary>OS のアプリ用テーマが暗色かどうかを判定する。</summary>
    /// <returns>暗色の場合は true。 読めない場合は false（既定は明色）。</returns>
    /// <remarks>
    /// ルート要素の <c>ActualTheme</c> は読み込み前だと既定（明色）を返すため、<c>System</c> の判定には使わない。<br/>
    /// 設定は利用者ごとのレジストリに保存されている。 読めない場合は例外にせず既定値を使う。
    /// </remarks>
    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (EsUtil.Platform.Core.BoundaryCatch.IsRecoverable(ex))
        {
            // レジストリを読めない環境（権限・ポリシー）では明色として扱う（起動を妨げない）
            return false;
        }
    }
}

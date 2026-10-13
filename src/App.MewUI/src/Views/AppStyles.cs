using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.MewUI.Icons;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>アプリ共通のスタイル（ナビパネル・ツールバー・ウィンドウ操作ボタン）を定義する。</summary>
/// <remarks>
/// <b>ローカル値との関係</b>: MewUI は <c>Background</c> 等へローカル値を設定すると、<br/>
/// スタイルの <c>StateTrigger</c>（ホバー・押下）が<b>適用されない</b>（ローカル値が優先される）。<br/>
/// そのため状態で変化させるプロパティは各ボタンへ直接設定せず、本クラスのスタイルで与える。<br/>
/// <br/>
/// <b>テーマ追従</b>: 色は <c>Func&lt;Theme, Color&gt;</c> で解決するため、テーマ変更時も自動で追従する。<br/>
/// <b>レイアウト値</b>: Avalonia 版（<c>App.axaml</c> の <c>toolbar</c> クラスと各ページの余白）と同じ値を使い、<br/>
/// 両 UI のツールバー高さとアイコンの縦位置を揃える。<br/>
/// <br/>
/// <b>適用パターン</b>: 名前付きスタイル（<c>StyleName</c>）を <see cref="Create"/> が返す <c>StyleSheet</c> へ登録し、<br/>
/// 使用側は本クラスの定数（<see cref="NavItem"/> 等）を <c>StyleName</c> へ設定する。
/// </remarks>
internal static class AppStyles
{
    /// <summary>テーマが変わったことを通知するイベント（テーマに依存する描画を手動で更新するページが購読する）。</summary>
    /// <remarks>
    /// テーマに追従するプロパティはスタイル（<c>Func&lt;Theme, Color&gt;</c>）で与えるのが原則。<br/>
    /// スタイルで表せない値（画像として描くアイコンの色・コントロールへ直接設定する色）だけが本イベントで更新する。
    /// </remarks>
    public static event EventHandler? ThemeChanged;

    /// <summary>テーマの変更を購読者へ通知する。</summary>
    /// <remarks>ウィンドウが受け取ったテーマ変更から呼ぶ（アプリ内のウィンドウは 1 つ）。</remarks>
    public static void NotifyThemeChanged() => ThemeChanged?.Invoke(null, EventArgs.Empty);

    // ====================================================================
    // レイアウト値（Avalonia 版と同一）
    // ====================================================================

    /// <summary>ツールバーの上余白（DIP）。 ウィンドウ最上部（タイトルバー領域）とのスペース。</summary>
    public const double ToolbarMarginTop = 6;

    /// <summary>ツールバーの下余白（DIP）。</summary>
    public const double ToolbarMarginBottom = 2;

    /// <summary>ツールバーの右余白（DIP）。 ウィンドウ右上のシステムボタンとの重なりを防ぐ。</summary>
    public const double ToolbarMarginRight = 140;

    /// <summary>ツールバーのボタン・テキストボックスの高さ（DIP）。</summary>
    public const double ToolbarButtonHeight = 32;

    /// <summary>ツールバーのボタンの水平余白（DIP）。</summary>
    public const double ToolbarPaddingX = 8;

    /// <summary>ツールバーのボタンの垂直余白（DIP）。</summary>
    public const double ToolbarPaddingY = 4;

    /// <summary>ツールバー行の高さ（DIP。 上余白 + ボタン高さ + 下余白）。</summary>
    public const double ToolbarRowHeight = ToolbarMarginTop + ToolbarButtonHeight + ToolbarMarginBottom;

    /// <summary>ツールバーのプルダウンを出す上端（DIP）。 ツールバーの直下に合わせる。</summary>
    public const double MenuTop = ToolbarMarginTop + ToolbarButtonHeight;

    /// <summary>ページ内容の上端（DIP）。 タイトルバー領域（32）に内容を重ねないための余白。</summary>
    public const double PageTopInset = ToolbarRowHeight;

    /// <summary>ステータスバーのボタンの高さ（DIP）。</summary>
    public const double StatusButtonHeight = 20;

    /// <summary>ステータスバーのスライダの高さ（DIP）。 Avalonia 版と同じ値。</summary>
    /// <remarks>Avalonia 版はスライダ（24）が行の高さを決めるため、本値がステータスバーの高さになる。</remarks>
    public const double StatusSliderHeight = 24;

    /// <summary>ステータスバーの左右の余白（DIP。 Avalonia 版の <c>Margin="4,1"</c> と同じ）。</summary>
    public const double StatusPaddingX = 4;

    /// <summary>ステータスバーの上下の余白（DIP。 キャプチャページ）。</summary>
    public const double StatusPaddingY = 1;

    /// <summary>ステータスバーの上下の余白（DIP。 一覧ページ）。</summary>
    public const double ListStatusPaddingY = 2;

    /// <summary>ステータスバーのボタンの最小幅（DIP）。</summary>
    public const double StatusButtonMinWidth = 22;

    /// <summary>ステータスバーのアイコンの一辺（DIP）。</summary>
    public const double StatusIconSize = 14;

    /// <summary>ステータスバーの文字サイズ（DIP）。</summary>
    public const double StatusFontSize = 12;

    /// <summary>ナビゲーション開閉トグルボタンの一辺（DIP）。</summary>
    public const double NavToggleSize = 28;

    /// <summary>ウィンドウ操作ボタンの最小幅（DIP）。</summary>
    public const double WindowButtonMinWidth = 46;

    /// <summary>ウィンドウ操作ボタンの高さ（DIP）。</summary>
    public const double WindowButtonHeight = 32;

    // ====================================================================
    // スタイル名
    // ====================================================================

    /// <summary>ナビゲーション開閉トグルボタンのスタイル名。</summary>
    public const string NavToggle = "nav-toggle";

    /// <summary>ナビゲーション項目（非選択）のスタイル名。</summary>
    public const string NavItem = "nav-item";

    /// <summary>ナビゲーション項目（選択中）のスタイル名。</summary>
    public const string NavItemSelected = "nav-item-selected";

    /// <summary>ツールバーのボタンのスタイル名。</summary>
    public const string ToolbarButton = "toolbar-button";

    /// <summary>ステータスバーのボタンのスタイル名。</summary>
    public const string StatusButton = "status-button";

    /// <summary>透過ボタン（ホバーで薄い背景）のスタイル名。</summary>
    public const string FlatButton = "flat-button";

    /// <summary>ウィンドウ操作ボタン（最小化・最大化）のスタイル名。</summary>
    public const string WindowButton = "window-button";

    /// <summary>ウィンドウ操作ボタン（閉じる。 ホバーで赤）のスタイル名。</summary>
    public const string WindowCloseButton = "window-close-button";

    /// <summary>サムネイルの枠のスタイル名。</summary>
    public const string ThumbnailFrame = "thumbnail-frame";

    /// <summary>サムネイルの枠（現在表示中の画像）のスタイル名。</summary>
    public const string ThumbnailFrameCurrent = "thumbnail-frame-current";

    /// <summary>ナビパネルの背景のスタイル名。</summary>
    public const string NavPanel = "nav-panel";

    /// <summary>ステータスバーの背景のスタイル名。</summary>
    public const string StatusBar = "status-bar";

    /// <summary>プルダウン（メニュー）の面のスタイル名。</summary>
    public const string MenuSurface = "menu-surface";

    /// <summary>区切り線のスタイル名。</summary>
    public const string Divider = "divider";

    /// <summary>設定ページのカードのスタイル名。</summary>
    public const string SettingsCard = "settings-card";

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景のスタイル名。</summary>
    public const string ListSurface = "list-surface";

    // ====================================================================
    // 色
    // ====================================================================

    /// <summary>ホバー時の背景色（明色テーマ）。</summary>
    private static readonly MewUIColor s_hoverLight = MewUIColor.FromArgb(255, 233, 233, 233);

    /// <summary>ホバー時の背景色（暗色テーマ）。</summary>
    private static readonly MewUIColor s_hoverDark = MewUIColor.FromArgb(255, 58, 58, 58);

    /// <summary>押下時の背景色（明色テーマ）。</summary>
    private static readonly MewUIColor s_pressedLight = MewUIColor.FromArgb(255, 219, 219, 219);

    /// <summary>押下時の背景色（暗色テーマ）。</summary>
    private static readonly MewUIColor s_pressedDark = MewUIColor.FromArgb(255, 69, 69, 69);

    /// <summary>ナビ項目のホバー背景色（明色テーマ）。</summary>
    private static readonly MewUIColor s_navHoverLight = MewUIColor.FromArgb(255, 229, 233, 240);

    /// <summary>ナビ項目のホバー背景色（暗色テーマ）。</summary>
    private static readonly MewUIColor s_navHoverDark = MewUIColor.FromArgb(255, 48, 52, 60);

    /// <summary>ナビ項目の選択背景色（明色テーマ）。</summary>
    private static readonly MewUIColor s_navSelectedLight = MewUIColor.FromArgb(255, 224, 231, 245);

    /// <summary>ナビ項目の選択背景色（暗色テーマ）。</summary>
    private static readonly MewUIColor s_navSelectedDark = MewUIColor.FromArgb(255, 58, 63, 75);

    /// <summary>閉じるボタンのホバー背景色（赤。 Windows の標準と同じ）。</summary>
    private static readonly MewUIColor s_closeHover = MewUIColor.FromArgb(255, 196, 43, 28);

    /// <summary>ウィンドウ操作ボタンのホバー背景色（半透明のグレー）。</summary>
    /// <remarks>半透明のため下地の色に応じて明暗し、明色・暗色のどちらのテーマでも「薄いグレー」に見える。</remarks>
    private static readonly MewUIColor s_windowButtonHover = MewUIColor.FromArgb(60, 128, 128, 128);

    /// <summary>ウィンドウ操作ボタンの押下背景色（半透明のグレー。 ホバーより濃い）。</summary>
    private static readonly MewUIColor s_windowButtonPressed = MewUIColor.FromArgb(96, 128, 128, 128);

    /// <summary>閉じるボタンのホバー時の文字色（白）。</summary>
    private static readonly MewUIColor s_closeHoverForeground = MewUIColor.FromArgb(255, 255, 255, 255);

    /// <summary>サムネイルの枠色（グレー）。</summary>
    private static readonly MewUIColor s_thumbnailBorder = MewUIColor.FromArgb(255, 128, 128, 128);

    /// <summary>サムネイルの枠色（ホバー時のハイライト。 青）。</summary>
    private static readonly MewUIColor s_thumbnailHoverBorder = MewUIColor.FromArgb(255, 0, 120, 212);

    /// <summary>サムネイルの枠色（現在表示中の画像。 青）。</summary>
    private static readonly MewUIColor s_thumbnailCurrentBorder = MewUIColor.FromArgb(255, 0, 120, 212);

    /// <summary>一覧アイテムのホバー時の背景（明色テーマ用。 黒 10%）。</summary>
    /// <remarks>
    /// Avalonia 版は <c>ListBox</c> の項目（<c>ListBoxItem</c>）がホバー時に薄い帯を表示する。<br/>
    /// MewUI は <c>ListBox</c> を使わないため、同じ見た目の帯をアイテム側で描く（実測値は黒 / 白の 10%）。
    /// </remarks>
    private static readonly MewUIColor s_thumbnailHoverBackgroundLight = MewUIColor.FromArgb(26, 0, 0, 0);

    /// <summary>一覧アイテムのホバー時の背景（暗色テーマ用。 白 10%）。</summary>
    private static readonly MewUIColor s_thumbnailHoverBackgroundDark = MewUIColor.FromArgb(26, 255, 255, 255);

    /// <summary>現在表示中のサムネイルの枠線の太さ（DIP）。</summary>
    private const double CurrentFrameThickness = 3.0;

    // ====================================================================
    // サムネイル（フォルダビュー / Undo 一覧）
    // ====================================================================

    /// <summary>サムネイルの枠の角丸（DIP）。</summary>
    public const double ThumbnailCornerRadius = 2;

    /// <summary>サムネイルの影のぼかし半径（DIP）。</summary>
    /// <remarks>
    /// Avalonia 版の <c>ThumbnailShadow</c>（<c>0 3 10 0 #8A000000</c>）の <c>Blur=10</c> に相当する値を、<br/>
    /// 実測の広がりが一致するよう MewUI の <see cref="ShadowDecorator"/> 向けに調整した値。<br/>
    /// MewUI のぼかしは Avalonia より狭く濃く出るため、同じ 10 では影が小さく見える。
    /// </remarks>
    public const double ThumbnailShadowBlur = 15;

    /// <summary>サムネイルの影の下方向のオフセット（DIP）。</summary>
    public const double ThumbnailShadowOffsetY = 3;

    /// <summary>サムネイルの影の色（黒 54%）。</summary>
    public static readonly MewUIColor ThumbnailShadowColor = MewUIColor.FromArgb(138, 0, 0, 0);

    /// <summary>ナビパネルの背景色（明色テーマ用）。</summary>
    /// <remarks>Avalonia 版の <c>SystemControlBackgroundChromeMediumBrush</c> と同じ値。</remarks>
    private static readonly MewUIColor s_navBackgroundLight = MewUIColor.FromArgb(255, 230, 230, 230);

    /// <summary>ナビパネルの背景色（暗色テーマ用）。</summary>
    private static readonly MewUIColor s_navBackgroundDark = MewUIColor.FromArgb(255, 31, 31, 31);

    /// <summary>ページ（ウィンドウ）の背景色（明色テーマ用）。</summary>
    /// <remarks>Avalonia 版のウィンドウ背景と同じ値。</remarks>
    private static readonly MewUIColor s_pageBackgroundLight = MewUIColor.FromArgb(255, 255, 255, 255);

    /// <summary>ページ（ウィンドウ）の背景色（暗色テーマ用）。</summary>
    private static readonly MewUIColor s_pageBackgroundDark = MewUIColor.FromArgb(255, 0, 0, 0);

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色（明色テーマ用）。</summary>
    /// <remarks>Avalonia 版の <c>SystemControlBackgroundChromeMediumLowBrush</c> と同じ値。</remarks>
    private static readonly MewUIColor s_listBackgroundLight = MewUIColor.FromArgb(255, 242, 242, 242);

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色（暗色テーマ用）。</summary>
    private static readonly MewUIColor s_listBackgroundDark = MewUIColor.FromArgb(255, 43, 43, 43);

    /// <summary>プルダウンの背景色（明色テーマ用）。</summary>
    private static readonly MewUIColor s_menuBackgroundLight = MewUIColor.FromArgb(255, 252, 252, 252);

    /// <summary>プルダウンの背景色（暗色テーマ用）。</summary>
    private static readonly MewUIColor s_menuBackgroundDark = MewUIColor.FromArgb(255, 50, 50, 50);

    /// <summary>プルダウンの枠・区切り線の色（明色テーマ用）。</summary>
    private static readonly MewUIColor s_menuBorderLight = MewUIColor.FromArgb(255, 200, 200, 200);

    /// <summary>プルダウンの枠・区切り線の色（暗色テーマ用）。</summary>
    private static readonly MewUIColor s_menuBorderDark = MewUIColor.FromArgb(255, 90, 90, 90);

    /// <summary>設定ページのカードの枠線の色（明色テーマ用）。</summary>
    /// <remarks>Avalonia 版の <c>SystemControlForegroundBaseLowBrush</c> の実測値と同じ。</remarks>
    private static readonly MewUIColor s_cardBorderLight = MewUIColor.FromArgb(255, 204, 204, 204);

    /// <summary>設定ページのカードの枠線の色（暗色テーマ用）。</summary>
    private static readonly MewUIColor s_cardBorderDark = MewUIColor.FromArgb(255, 90, 90, 90);

    /// <summary>サムネイルの枠線の太さ（DIP。 通常時）。</summary>
    private const double ThumbnailFrameThickness = 1;

    /// <summary>サムネイルの枠色（通常時）。</summary>
    /// <value>グレー。 テーマに依存しない（Avalonia 版の <c>SystemControlForegroundChromeGrayBrush</c> 相当）。</value>
    public static MewUIColor ThumbnailBorder => s_thumbnailBorder;

    /// <summary>サムネイルの枠色（ホバー時）。</summary>
    /// <value>青（ハイライト）。</value>
    public static MewUIColor ThumbnailHoverBorder => s_thumbnailHoverBorder;

    /// <summary>一覧アイテムのホバー時の背景色を取得する（現在のテーマ）。</summary>
    /// <returns>ホバー時の背景色（半透明のため一覧背景と合成される）。</returns>
    public static MewUIColor CurrentThumbnailHoverBackground
        => IconLoader.IsDarkTheme() ? s_thumbnailHoverBackgroundDark : s_thumbnailHoverBackgroundLight;

    /// <summary>ツールバーの区切り線（縦の細線）を生成する。</summary>
    /// <returns>高さ 20 DIP の縦線。</returns>
    /// <remarks>3 ページ（キャプチャ / フォルダビュー / Undo 一覧）のツールバーで共有する。</remarks>
    public static Border CreateToolbarDivider()
        => new()
        {
            Width = 1,
            Height = 20,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            StyleName = Divider,
        };

    /// <summary>アプリ共通のスタイルシートを生成する。</summary>
    /// <returns>生成したスタイルシート。</returns>
    /// <remarks>
    /// 1. ナビゲーション（トグル・項目・選択中の項目）を定義する。<br/>
    /// 2. ツールバー・ステータスバーのボタンとウィンドウ操作ボタンを定義する。<br/>
    /// 3. 返したシートをウィンドウの <c>StyleSheet</c> へ設定すると、配下の要素が <c>StyleName</c> で解決できる。
    /// </remarks>
    public static StyleSheet Create()
    {
        var sheet = new StyleSheet();

        sheet.Define(NavToggle, () => Flat(s_hoverLight, s_hoverDark));
        sheet.Define(NavItem, () => NavItemStyle(s_navHoverLight, s_navHoverDark, _ => MewUIColor.Transparent));
        sheet.Define(NavItemSelected, () => NavItemStyle(s_navSelectedLight, s_navSelectedDark, t => t.IsDark ? s_navSelectedDark : s_navSelectedLight));
        sheet.Define(ToolbarButton, () => Flat(s_hoverLight, s_hoverDark));
        sheet.Define(StatusButton, () => StatusButtonStyle());
        sheet.Define(FlatButton, () => Flat(s_hoverLight, s_hoverDark));
        sheet.Define(WindowButton, () => WindowButtonStyle());
        sheet.Define(WindowCloseButton, () => new Style(typeof(Button))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, _ => MewUIColor.Transparent),
                Setter.Create(Control.BorderThicknessProperty, 0.0),
            ],
            Triggers =
            [
                new StateTrigger
                {
                    Match = VisualStateFlags.Hot,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, _ => s_closeHover),
                        Setter.Create(TextElement.ForegroundProperty, _ => s_closeHoverForeground),
                    ],
                },
            ],
        });

        // サムネイルの枠: 通常はグレー、背景は一覧の背景（Avalonia 版の ChromeMediumLowBrush 相当）
        // ホバーのハイライトは ShadowDecorator 配下では StateTrigger が効かない（実測）ため、
        // ホスト側が MouseEnter / MouseLeave で直接切り替える
        sheet.Define(ThumbnailFrame, () => new Style(typeof(Border))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, t => ListBackground(t)),
                Setter.Create(Control.BorderBrushProperty, _ => s_thumbnailBorder),
                Setter.Create(Control.BorderThicknessProperty, ThumbnailFrameThickness),
                Setter.Create(Control.CornerRadiusProperty, ThumbnailCornerRadius),
            ],
        });

        // 現在表示中のサムネイルの枠: 常に青の太枠（ホバーでも変えない）
        sheet.Define(ThumbnailFrameCurrent, () => new Style(typeof(Border))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, t => ListBackground(t)),
                Setter.Create(Control.BorderBrushProperty, _ => s_thumbnailCurrentBorder),
                Setter.Create(Control.BorderThicknessProperty, CurrentFrameThickness),
                Setter.Create(Control.CornerRadiusProperty, ThumbnailCornerRadius),
            ],
        });

        // テーマに追従する面（背景・枠）はスタイルで与える。
        // 生成時に色を決めると、実行中にテーマを切り替えても変わらない（スタイルは変更時に自動で再解決される）
        sheet.Define(NavPanel, () => new Style(typeof(Border))
        {
            Setters = [Setter.Create(Control.BackgroundProperty, t => NavPanelBackground(t))],
        });

        // ステータスバーはページ背景をそのまま見せる（Avalonia 版は背景を指定しない）
        sheet.Define(StatusBar, () => new Style(typeof(Border))
        {
            Setters = [Setter.Create(Control.BackgroundProperty, _ => MewUIColor.Transparent)],
        });

        // 一覧（フォルダビュー / Undo 一覧）の背景（Avalonia 版の ListBox 背景相当）
        sheet.Define(ListSurface, () => new Style(typeof(Border))
        {
            Setters = [Setter.Create(Control.BackgroundProperty, t => ListBackground(t))],
        });
        sheet.Define(MenuSurface, () => new Style(typeof(Border))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, t => MenuBackground(t)),
                Setter.Create(Control.BorderBrushProperty, t => MenuBorder(t)),
                Setter.Create(Control.BorderThicknessProperty, 1.0),
            ],
        });
        sheet.Define(Divider, () => new Style(typeof(Border))
        {
            Setters = [Setter.Create(Control.BackgroundProperty, t => MenuBorder(t))],
        });
        sheet.Define(SettingsCard, () => new Style(typeof(Border))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, _ => MewUIColor.Transparent),
                Setter.Create(Control.BorderBrushProperty, t => SettingsCardBorder(t)),
                Setter.Create(Control.BorderThicknessProperty, 1.0),
            ],
        });

        return sheet;
    }

    // ====================================================================
    // テーマに追従する色
    // ====================================================================

    /// <summary>ナビパネルの背景色を取得する。</summary>
    /// <param name="theme">現在のテーマ。</param>
    /// <returns>ナビパネルの背景色。</returns>
    public static MewUIColor NavPanelBackground(Theme theme)
        => theme.IsDark ? s_navBackgroundDark : s_navBackgroundLight;

    /// <summary>ページ（ウィンドウ）の背景色を取得する（現在のテーマ）。</summary>
    /// <value>明色テーマは白・暗色テーマは黒（不透明）。</value>
    /// <remarks>
    /// スタイルではなくローカル値で設定する必要がある箇所（ウィンドウの背景・画像が無い部分の下地）で使う。<br/>
    /// 下地を塗らないと後ろの要素（プレビューの画像等）が透けて見える（Avalonia 版はページ背景の上へ重ねる）。
    /// </remarks>
    public static MewUIColor CurrentPageBackground
        => IconLoader.IsDarkTheme() ? s_pageBackgroundDark : s_pageBackgroundLight;

    /// <summary>一覧（フォルダビュー / Undo 一覧）の背景色を取得する。</summary>
    /// <param name="theme">現在のテーマ。</param>
    /// <returns>一覧の背景色。</returns>
    public static MewUIColor ListBackground(Theme theme)
        => theme.IsDark ? s_listBackgroundDark : s_listBackgroundLight;

    /// <summary>プルダウン（メニュー）の背景色を取得する。</summary>
    /// <param name="theme">現在のテーマ。</param>
    /// <returns>メニューの背景色。</returns>
    public static MewUIColor MenuBackground(Theme theme)
        => theme.IsDark ? s_menuBackgroundDark : s_menuBackgroundLight;

    /// <summary>区切り線・メニュー枠の色を取得する。</summary>
    /// <param name="theme">現在のテーマ。</param>
    /// <returns>線の色。</returns>
    public static MewUIColor MenuBorder(Theme theme)
        => theme.IsDark ? s_menuBorderDark : s_menuBorderLight;

    /// <summary>設定ページのカードの枠線の色を取得する。</summary>
    /// <param name="theme">現在のテーマ。</param>
    /// <returns>枠線の色。</returns>
    public static MewUIColor SettingsCardBorder(Theme theme)
        => theme.IsDark ? s_cardBorderDark : s_cardBorderLight;

    /// <summary>テーマモードをアプリ全体へ適用する。</summary>
    /// <param name="mode">適用するテーマモード。</param>
    /// <returns>適用した場合は true。 アプリが未初期化の場合は false。</returns>
    /// <remarks>
    /// テーマは <c>Application.Run</c> の後（起動時コールバック）でしか変更できない<br/>
    /// （それより前は <c>Application.Current</c> が未初期化で例外になる）。<br/>
    /// 適用するとテーマ変更が通知され、テーマに追従するスタイルと <see cref="ThemeChanged"/> の購読者が更新される。
    /// </remarks>
    public static bool ApplyTheme(ThemeMode mode)
    {
        if (Application.Current is not { } application)
        {
            return false;
        }

        application.SetThemeMode(mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.System,
        });

        return true;
    }

    /// <summary>背景を持たず、ホバー・押下で薄い背景を表示するボタンのスタイルを生成する。</summary>
    /// <param name="hoverLight">ホバー時の背景色（明色テーマ）。</param>
    /// <param name="hoverDark">ホバー時の背景色（暗色テーマ）。</param>
    /// <returns>生成したスタイル。</returns>
    private static Style Flat(MewUIColor hoverLight, MewUIColor hoverDark)
        => new(typeof(Button))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, _ => MewUIColor.Transparent),
                Setter.Create(Control.BorderThicknessProperty, 0.0),
            ],
            Triggers =
            [
                new StateTrigger
                {
                    Match = VisualStateFlags.Hot,
                    Setters = [Setter.Create(Control.BackgroundProperty, t => t.IsDark ? hoverDark : hoverLight)],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.Pressed,
                    Setters = [Setter.Create(Control.BackgroundProperty, t => t.IsDark ? s_pressedDark : s_pressedLight)],
                },
            ],
        };

    /// <summary>ウィンドウ操作ボタン（最小化・最大化）のスタイルを生成する。</summary>
    /// <returns>生成したスタイル。</returns>
    /// <remarks>
    /// 背景を持たず、ホバー・押下では**半透明のグレー**を重ねる（Windows のエクスプローラーと同じ見え方）。<br/>
    /// 半透明のため、テーマ（明色・暗色）や下地の色に関わらず薄いグレーとして浮き上がる。
    /// </remarks>
    private static Style WindowButtonStyle()
        => new(typeof(Button))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, _ => MewUIColor.Transparent),
                Setter.Create(Control.BorderThicknessProperty, 0.0),
            ],
            Triggers =
            [
                new StateTrigger
                {
                    Match = VisualStateFlags.Hot,
                    Setters = [Setter.Create(Control.BackgroundProperty, _ => s_windowButtonHover)],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.Pressed,
                    Setters = [Setter.Create(Control.BackgroundProperty, _ => s_windowButtonPressed)],
                },
            ],
        };

    /// <summary>ステータスバーのボタンのスタイルを生成する。</summary>
    /// <returns>生成したスタイル。</returns>
    /// <remarks>
    /// 背景を持たず、ホバー・押下で薄い背景を表示するのは他の透過ボタンと同じ。<br/>
    /// ボタンの既定スタイルは <c>MinHeight</c> をウィンドウの基準コントロール高（32 DIP）にするため、<br/>
    /// ステータスバーの小さな寸法（高さ 20）を指定しても既定値に負けて高さが変わらない。<br/>
    /// そのため最小寸法を 0 に上書きして、指定した高さをそのまま使えるようにする。
    /// </remarks>
    private static Style StatusButtonStyle()
        => new(typeof(Button))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, _ => MewUIColor.Transparent),
                Setter.Create(Control.BorderThicknessProperty, 0.0),
                Setter.Create(FrameworkElement.MinHeightProperty, 0.0),
                Setter.Create(FrameworkElement.MinWidthProperty, 0.0),
            ],
            Triggers =
            [
                new StateTrigger
                {
                    Match = VisualStateFlags.Hot,
                    Setters = [Setter.Create(Control.BackgroundProperty, t => t.IsDark ? s_hoverDark : s_hoverLight)],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.Pressed,
                    Setters = [Setter.Create(Control.BackgroundProperty, t => t.IsDark ? s_pressedDark : s_pressedLight)],
                },
            ],
        };

    /// <summary>ナビゲーション項目のスタイルを生成する。</summary>
    /// <param name="hoverLight">ホバー時の背景色（明色テーマ）。</param>
    /// <param name="hoverDark">ホバー時の背景色（暗色テーマ）。</param>
    /// <param name="resolveBase">通常時の背景色をテーマから解決する関数。</param>
    /// <returns>生成したスタイル。</returns>
    private static Style NavItemStyle(MewUIColor hoverLight, MewUIColor hoverDark, Func<Theme, MewUIColor> resolveBase)
        => new(typeof(Button))
        {
            Setters =
            [
                Setter.Create(Control.BackgroundProperty, resolveBase),
                Setter.Create(Control.BorderThicknessProperty, 0.0),
            ],
            Triggers =
            [
                new StateTrigger
                {
                    Match = VisualStateFlags.Hot,
                    Setters = [Setter.Create(Control.BackgroundProperty, t => t.IsDark ? hoverDark : hoverLight)],
                },
            ],
        };
}

using EsUtil.SaveEvidence.App.Core.Enums;
using Microsoft.UI.Xaml;

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

    /// <summary>ツールバーのボタンの高さ（DIP）。</summary>
    public const double ToolbarButtonHeight = 32;

    /// <summary>ツールバーの左右の内側余白（DIP）。</summary>
    public const double ToolbarPaddingX = 8;

    /// <summary>ツールバーの上下の内側余白（DIP）。</summary>
    public const double ToolbarPaddingY = 4;

    /// <summary>ステータス バーのボタンの高さ（DIP）。</summary>
    public const double StatusButtonHeight = 20;

    /// <summary>ステータス バーのスライダの高さ（DIP）。</summary>
    public const double StatusSliderHeight = 24;

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
    public const double NavPanelExpandedWidth = 180;

    /// <summary>ナビゲーション パネルの幅（折りたたみ時・DIP）。</summary>
    public const double NavPanelCompactWidth = 40;

    /// <summary>ツールバーのアイコンの一辺（DIP）。</summary>
    public const double ToolbarIconSize = 16;

    /// <summary>ナビゲーション項目のアイコンの一辺（DIP）。</summary>
    public const double NavIconSize = 16;

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

    /// <summary>テーマを適用する。</summary>
    /// <param name="root">テーマを設定するルート要素（ウィンドウの内容）。 null の場合は色の判定だけを行う。</param>
    /// <param name="mode">適用するテーマの指定。</param>
    /// <remarks>
    /// <c>System</c> はルート要素の <c>ElementTheme.Default</c> に対応し、OS の設定へ追従する。<br/>
    /// 適用後の実効テーマ（<c>ActualTheme</c>）から暗色かどうかを判定し、変化していれば
    /// <see cref="ThemeChanged"/> を発火する。
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

        var isDark = root is null
            ? requested == ElementTheme.Dark
            : root.ActualTheme == ElementTheme.Dark;

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
}

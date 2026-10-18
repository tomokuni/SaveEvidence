namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>メイン ウィンドウの配置（4 つの UI 版で共通）。</summary>
/// <remarks>
/// 左上のアプリ アイコンとナビゲーション パネルの位置を 1 か所で決める（UI 版ごとに余白がずれないようにする）。<br/>
/// <br/>
/// <b>アプリ アイコン</b>はナビゲーション アイコンと同じ大きさ（<see cref="AppIconSize"/>）にし、<br/>
/// 縦位置はツールバーのアイコンと揃え（<see cref="AppIconRowHeight"/> の中で中央）、<br/>
/// 横位置はナビゲーションの開閉アイコンと同じ列（<see cref="AppIconColumnWidth"/> の中で中央）に置く。<br/>
/// <b>タイトル バーの帯は作らない</b>（ページ領域はクライアント領域の先頭から始まる。 <see cref="TitleBarHeight"/> は<br/>
/// 上端のドラッグ領域とウィンドウ メニューの位置にだけ使う）。
/// </remarks>
public static class WindowLayout
{
    /// <summary>ツールバーの行の上端（DIP）。 左上のアプリ アイコンの縦位置の基準。</summary>
    public const double ToolbarRowTop = 6;

    /// <summary>ツールバーのボタンの高さ（DIP）。</summary>
    public const double ToolbarButtonHeight = 32;

    /// <summary>左上のアプリ アイコンを置く行の高さ（DIP）。</summary>
    /// <remarks>
    /// アイコンの中心がツールバーのアイコンの中心（<see cref="ToolbarRowTop"/> + ボタンの半分）と<br/>
    /// 一致するように、上端と下端へ同じ余白を取る（6 + 32 + 6 = 44）。
    /// </remarks>
    public const double AppIconRowHeight = (ToolbarRowTop * 2) + ToolbarButtonHeight;

    /// <summary>左上のアプリ アイコンの一辺（DIP）。 ナビゲーションのアイコンと同じ大きさにする。</summary>
    public const double AppIconSize = 16;

    /// <summary>左上のアプリ アイコンの列の幅（DIP）。 ナビゲーションの開閉アイコンと同じ列に置く。</summary>
    public const double AppIconColumnWidth = NavPanelMetrics.CompactWidth;

    /// <summary>ナビゲーションの開閉ボタンの高さ（DIP）。 ナビゲーション パネルの一番上に置く。</summary>
    public const double NavToggleHeight = 40;

    /// <summary>ナビゲーションの開閉ボタンの幅（DIP）。</summary>
    public const double NavToggleWidth = NavPanelMetrics.CompactWidth;

    /// <summary>上端のドラッグ領域の高さ（DIP）。</summary>
    public const double TitleBarHeight = 32;
}
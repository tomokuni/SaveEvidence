namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>メイン ウィンドウの配置（4 つの UI 版で共通）。</summary>
/// <remarks>
/// タイトル バーとナビゲーション パネルの位置を 1 か所で決める（UI 版ごとに余白がずれないようにする）。<br/>
/// <br/>
/// <b>タイトル バーにはナビゲーション パネルを重ねない</b>: 左上のアプリ アイコンとアプリ名は<br/>
/// タイトル バー（<see cref="TitleBarHeight"/>）に置き、ナビゲーション パネルは<b>その下</b>から始める<br/>
/// （ページ領域の上端に揃える）。 ナビゲーション パネルはページ領域の上へ重なり、<br/>
/// 下端はステータス バーに被る（高さはクライアント領域いっぱい）。
/// </remarks>
public static class WindowLayout
{
    /// <summary>タイトル バーの高さ（DIP）。</summary>
    public const double TitleBarHeight = 32;

    /// <summary>左上のアプリ アイコンの一辺（DIP）。</summary>
    public const double AppIconSize = 16;

    /// <summary>アプリ アイコンのボタンの一辺（DIP）。 押しやすい大きさにする。</summary>
    public const double AppIconButtonSize = TitleBarHeight;

    /// <summary>アプリ アイコンの左の余白（DIP）。</summary>
    public const double AppIconMargin = 8;

    /// <summary>アプリ名の左端（DIP）。 アプリ アイコンの右端から余白を空ける。</summary>
    public const double AppNameLeft = AppIconMargin + AppIconSize + 8;

    /// <summary>ナビゲーション パネルの開閉ボタンの高さ（DIP）。 パネルの一番上に置く。</summary>
    public const double NavToggleHeight = 40;
}
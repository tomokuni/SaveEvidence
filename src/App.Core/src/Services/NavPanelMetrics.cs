namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ナビゲーション パネルの寸法（4 つの UI 版で共通）。</summary>
/// <remarks>
/// 幅・フォント サイズを 1 か所で決める（UI 版ごとに幅や文字の大きさがずれないようにする）。<br/>
/// <br/>
/// <b>展開時はページ領域の上へ重ねる</b>: 展開してもページ領域は移動しない（<see cref="CompactWidth"/> の位置に留まる）。<br/>
/// 展開したパネルはページ領域の上にオーバーレイし、領域の外を操作したら閉じる（各 UI 版のホストが担う）。<br/>
/// <br/>
/// <b>展開時の幅の決め方</b>: 「アイコン + 文字 + 余白」で決める。 文字の幅は最も長い項目名<br/>
/// （「フォルダビュー」）を標準の文字サイズ（<see cref="ItemFontSize"/>）で描いた実測値を使う。<br/>
/// 末尾の <see cref="TrailingSlack"/> は UI ごとの内部余白の差（WinUI 3 の項目はアイコンの列が<br/>
/// 他より広い）を吸収するための <b>α</b> であり、実測で文字が欠けない最小値にしている。<br/>
/// <br/>
/// <b>収納時の幅</b>: アイコンのみを表示するため「左右の余白 + アイコン」で決まる。
/// </remarks>
public static class NavPanelMetrics
{
    /// <summary>項目の文字サイズ（DIP）。 全 UI 版で同じ値を使う（AvaloniaUI 版の項目と同じ大きさ）。</summary>
    public const double ItemFontSize = 14;

    /// <summary>項目の左右の余白（DIP）。</summary>
    public const double ItemHorizontalPadding = 12;

    /// <summary>アイコンの一辺（DIP）。 ナビゲーションとツールバーで同じ値を使う。</summary>
    /// <remarks>
    /// ナビゲーションの項目・開閉ボタン・左上のアプリ アイコンと、ページのツールバーのアイコンは<br/>
    /// <b>すべてこの値</b>を使う（UI 版ごとに大きさが変わらないようにする）。
    /// </remarks>
    public const double IconSize = 16;

    /// <summary>アイコンと文字の間隔（DIP）。</summary>
    public const double IconSpacing = 8;

    /// <summary>項目の高さ（DIP）。 全 UI 版で同じ値を使う（開閉しても変わらない）。</summary>
    public const double ItemHeight = 40;

    /// <summary>項目の一覧の上下の余白（DIP）。</summary>
    public const double ItemsMarginTop = 8;

    /// <summary>選択中のマークの幅（DIP）。</summary>
    public const double SelectionMarkWidth = 3;

    /// <summary>選択中のマークの左端（DIP）。</summary>
    public const double SelectionMarkLeft = 4;

    /// <summary>最も長い項目名（「フォルダビュー」）の幅（DIP・<see cref="ItemFontSize"/> で実測）。</summary>
    public const double LabelWidth = 68;

    /// <summary>UI ごとの内部余白の差を吸収する α（DIP）。</summary>
    /// <remarks>
    /// WinUI 3 の項目は自前のアイコン列（約 40 DIP）と左右の余白を持ち、他より内側が広い。<br/>
    /// 実測で最も長い項目名が省略記号なしに収まる値にしている（狭いと「フォルダビュ…」になる）。
    /// </remarks>
    public const double TrailingSlack = 20;

    /// <summary>ナビゲーション パネルの幅（収納時・DIP）。 アイコンのみを表示する。</summary>
    public const double CompactWidth = (ItemHorizontalPadding * 2) + IconSize;

    /// <summary>ナビゲーション パネルの幅（展開時・DIP）。</summary>
    /// <remarks>アイコン + 最も長い項目名 + 余白（α を含む）。 特定の UI 版へ合わせた固定値ではない。</remarks>
    public const double ExpandedWidth =
        (ItemHorizontalPadding * 2) + IconSize + IconSpacing + LabelWidth + TrailingSlack;

    /// <summary>ナビゲーション パネルの幅を取得する。</summary>
    /// <param name="isExpanded">展開しているかどうか。</param>
    /// <returns>パネルの幅（DIP）。</returns>
    public static double Width(bool isExpanded) => isExpanded ? ExpandedWidth : CompactWidth;
}

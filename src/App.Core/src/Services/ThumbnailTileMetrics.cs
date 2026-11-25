namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>サムネイル 1 件（保存フォルダ / 履歴）の見た目の寸法（4 つの UI 版で共通）。</summary>
/// <remarks>
/// サムネイルの影と名前の行を 1 か所で決める（UI 版ごとに影の濃さ・名前の位置がずれないようにする）。<br/>
/// <br/>
/// <b>画像の周りに枠線・下地を置かない</b>: 画像の周りは影だけにする（枠線・背景・角丸は描かない）。<br/>
/// <br/>
/// <b>名前の行を固定高にする理由</b>: 行内でアイテムの下端を揃えるため。 名前が 1 行でも 2 行でも<br/>
/// 高さを変えないので、同じ行のアイテムの名前の 1 文字目が同じ高さに並ぶ。<br/>
/// <br/>
/// <b>影</b>: 色・幅・濃さの変化は <see cref="ShadowPlan"/> が単一所有する（本クラスは持たない）。
/// </remarks>
public static class ThumbnailTileMetrics
{
    /// <summary>名前の文字サイズ（DIP）。</summary>
    public const double LabelFontSize = 11;

    /// <summary>名前の行の高さ（DIP）。 2 行分を固定で確保する。</summary>
    public const double LabelHeight = 30;

    /// <summary>名前の行の左右の余白（DIP）。</summary>
    public const double LabelHorizontalPadding = 1;

    /// <summary>画像の周囲に確保する余白（DIP）。 影が掛かる余地。</summary>
    /// <remarks>AvaloniaUI 版のアイテム（<c>Border Margin="2"</c>）に相当する。</remarks>
    public const double ItemMargin = 2;

    /// <summary>一覧がアイテムの外側に確保する余白（DIP）。</summary>
    /// <remarks>
    /// AvaloniaUI 版の <c>ListBoxItem</c> の既定の余白（12 DIP）に相当する。<br/>
    /// 4 UI 版で同じ間隔にするため、各 UI 版はこの値をアイテムの外側へ確保する。
    /// </remarks>
    public const double ItemPadding = 12;

    /// <summary>隣り合うサムネイルの画像の間隔（DIP）。</summary>
    /// <remarks>
    /// 「余白 + 画像の外側の間隔」を両側に確保した値（AvaloniaUI 版の実測 28 DIP と一致する）。
    /// </remarks>
    public const double ItemGap = (ItemPadding + ItemMargin) * 2;

    /// <summary>アイテムにマウスを重ねたときの背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    /// <remarks>
    /// AvaloniaUI 版の <c>ListBoxItem</c> のホバー背景と同じ値（明色 <c>#DADADA</c> を実測）。<br/>
    /// 暗色は同じ役割の濃い色を使う。
    /// </remarks>
    public static uint HoverBackground(bool isDark) => isDark ? 0xFF303030 : 0xFFDADADA;
}

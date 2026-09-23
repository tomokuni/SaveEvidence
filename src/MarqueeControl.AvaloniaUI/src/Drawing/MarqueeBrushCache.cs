using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI.Imaging;

namespace Estable.AvaloniaUI.Marquee.Drawing;

/// <summary>Marquee の描画層が共用するブラシキャッシュ。</summary>
/// <remarks>
/// 選択矩形（<see cref="MarqueeDrawer"/>）・外側の装飾（<see cref="MarqueeOuterDrawer"/>）・内側の装飾（<see cref="MarqueeInnerDrawer"/>）が<br/>
/// 同じ色の組を描くため、キャッシュを 1 つに集約する（描画ごとにキャッシュを分けると、同じ色でも重複して保持される）。<br/>
/// 容量管理（超過時の全クリア）は Platform.AvaloniaUI の <see cref="BrushCache"/> が単一所有する。
/// </remarks>
internal static class MarqueeBrushCache
{
    /// <summary>ブラシキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    internal const int Capacity = 128;

    /// <summary>ブラシキャッシュ（色 → ブラシ。容量制限付き）。</summary>
    private static readonly BrushCache s_cache = new(Capacity);

    /// <summary>色に対応するブラシを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    public static SolidColorBrush GetBrush(Color color) => s_cache.GetOrCreate(color);
}

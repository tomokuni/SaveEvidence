using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using DrawingColor = System.Drawing.Color;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペが使うブラシを単一所有する（コアの色型 → 共有ブラシ）。</summary>
/// <remarks>
/// コントロール（座標ラベルの文字色・背景色）と描画層（市松模様・十字線・外枠）が同じ変換を使うため、<br/>
/// 変換と容量制限付きキャッシュを 1 箇所に集約する（コントロールごとにキャッシュを持つと容量管理が二重になる）。<br/>
/// キャッシュは <see cref="BrushCache"/>（Platform.AvaloniaUI）へ委譲し、容量超過時は全クリアして無制限の増加を防ぐ。
/// </remarks>
internal static class LoupeBrushes
{
    /// <summary>色に対応するブラシの容量制限付きキャッシュ。</summary>
    private static readonly BrushCache s_cache = new();

    /// <summary>コアの色に対応するブラシを取得する（未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色（コアの色型）。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    public static SolidColorBrush Get(DrawingColor color) => s_cache.GetOrCreate(color.ToAvaloniaColor());
}

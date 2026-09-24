using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using Estable.Marquee;
using Estable.Marquee.Selection;
using Avalonia;
using Avalonia.Media;

namespace Estable.AvaloniaUI.Marquee.Drawing;

/// <summary>リサイズ可能なドラッグ矩形の描画（選択枠線・ドラッグハンドル・始点マーカー）を提供する静的クラス。</summary>
/// <remarks>
/// 矩形ドメイン（Marquee）の視覚表現を担当する。<br/>
/// ハンドル位置は <see cref="Selection.SelectionGeometry.GetHandlePosition"/>（DRY）から取得し、中心に描画する。<br/>
/// <br/>
/// 選択矩形の外側に描く装飾（暗転マスク・枠線）の描画は <see cref="MarqueeOuterDrawer"/> が担う。<br/>
/// <b>文字（座標ラベル）は本クラスが描かない</b>: テキストの実寸測定が必要なため、描画完了時にホストへ通知し<br/>
/// <see cref="MarqueeLabelRenderer"/> が描く（文字の描画をホストの責務とする）。<br/>
/// <br/>
/// **適用パターン**: Flyweight（色・太さで決まるブラシ / ペンをキャッシュして再利用）。
/// </remarks>
internal static class MarqueeDrawer
{
    /// <summary>ペンキャッシュ（色・太さ → ペン。Flyweight・容量制限付き）。</summary>
    private static readonly CapacityLimitedCache<(Color Color, double Thickness), Pen> s_penCache =
        new(static key => new Pen(MarqueeBrushCache.GetBrush(key.Color), key.Thickness));

    /// <summary>色に対応するブラシを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    /// <remarks>容量管理（容量制限・超過時全クリア）は <see cref="CapacityLimitedCache{TKey, TValue}"/> へ委譲する（単一所有・DRY）。</remarks>
    private static SolidColorBrush GetBrush(Color color)
        => MarqueeBrushCache.GetBrush(color);

    /// <summary>色・太さに対応するペンを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ペンの色。</param>
    /// <param name="thickness">ペンの太さ。</param>
    /// <returns>色・太さに対応する <see cref="Pen"/>。</returns>
    /// <remarks>容量超過時はキャッシュを全クリアしてから登録する（<see cref="CapacityLimitedCache{TKey, TValue}"/> へ委譲）。</remarks>
    private static Pen GetPen(Color color, double thickness) => s_penCache.GetOrCreate((color, thickness));

    /// <summary>描画対象のハンドル種別（8 方向。None / Move を除く）。</summary>
    /// <remarks>一覧はコア（<see cref="SelectionGeometry.DirectionalHandles"/>）が単一所有する。</remarks>
    private static HitTarget[] HandleTypes => SelectionGeometry.DirectionalHandles;

    /// <summary>ドラッグハンドル（色付き矩形＋枠線）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="rect">選択矩形。</param>
    /// <param name="handleSize">ハンドルの一辺サイズ。</param>
    /// <param name="handleColor">ハンドルの塗り色（未指定は白へ解決済み。コアの <c>MarqueeAppearance.ResolveHandleColor</c> が単一所有する）。</param>
    /// <param name="borderColor">ハンドル枠の色（外側の装飾の枠線色を使う）。</param>
    /// <remarks>
    /// 8方向のハンドル（None / Move を除く）を列挙し、<see cref="Selection.SelectionGeometry.GetHandlePosition"/> から<br/>
    /// 中心位置を取得して描画する。新ハンドル追加時は HitTarget の追加のみで描画が追従する（OCP）。
    /// </remarks>
    public static void DrawHandles(DrawingContext context, Rect rect, double handleSize, Color handleColor, Color borderColor)
    {
        var handleFill = GetBrush(handleColor);
        var handlePen = GetPen(borderColor, MarqueeAppearance.HandlePenThickness);
        double hs = handleSize;
        double hh = hs / 2;

        // ドメイン計算 (SelectionGeometry) は UI 非依存のため、コア型へ変換して渡し、結果のみ DIP へ戻す。
        var domainRect = rect.ToEsRect();
        foreach (var handle in HandleTypes)
        {
            var center = SelectionGeometry.GetHandlePosition(domainRect, handle);
            DrawHandle(context, center.X - hh, center.Y - hh, hs, hs, handleFill, handlePen);
        }
    }

    /// <summary>始点マーカー（十字線）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="center">始点の中心位置。</param>
    /// <param name="color">十字線の色。</param>
    /// <param name="thickness">十字線の太さ。</param>
    /// <param name="size">十字線の半幅。</param>
    public static void DrawStartMarker(DrawingContext context, Point center, Color color, double thickness, double size)
    {
        var pen = GetPen(color, thickness);
        context.DrawLine(pen, new Point(center.X - size, center.Y), new Point(center.X + size, center.Y));
        context.DrawLine(pen, new Point(center.X, center.Y - size), new Point(center.X, center.Y + size));
    }

    /// <summary>ドラッグハンドル（矩形＋枠線）を描画する。</summary>
    private static void DrawHandle(DrawingContext context, double x, double y, double w, double h, IBrush fill, Pen pen)
    {
        var r = new Rect(x, y, w, h);
        context.FillRectangle(fill, r);
        context.DrawRectangle(pen, r);
    }
}




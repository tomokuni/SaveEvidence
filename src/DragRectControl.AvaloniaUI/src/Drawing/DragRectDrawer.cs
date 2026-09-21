using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using Estable.DragRect;
using Estable.DragRect.Selection;
using EsUtil.Platform.Core.Geometry;
using Avalonia;
using Avalonia.Media;
using System.Collections.Concurrent;
using System.Globalization;

namespace Estable.AvaloniaUI.DragRect.Drawing;

/// <summary>リサイズ可能なドラッグ矩形の描画（選択枠線・ドラッグハンドル・始点マーカー・カーソル座標ラベル）を提供する静的クラス。</summary>
/// <remarks>
/// 矩形ドメイン（DragRect）の視覚表現を担当する。<br/>
/// ハンドル位置は <see cref="Selection.SelectionGeometry.GetHandlePosition"/>（DRY）から取得し、中心に描画する。<br/>
/// <br/>
/// 暗転マスク・サイズラベル等の選択オーバーレイ描画は Selection 側（FadeMaskDrawer）が担う。<br/>
/// <br/>
/// **適用パターン**: Flyweight（色・太さで決まるブラシ / ペンをキャッシュして再利用）。
/// </remarks>
internal static class DragRectDrawer
{
    /// <summary>ブラシキャッシュ（色 → ブラシ。Flyweight・容量制限付き）。</summary>
    /// <remarks>Platform.AvaloniaUI の <see cref="BrushCache"/> へ委譲する（LoupeControl と共用・重複排除・DRY）。</remarks>
    private static readonly BrushCache s_brushCache = new(BrushCacheCapacity);

    /// <summary>ペンキャッシュ（色・太さ → ペン。Flyweight・容量制限付き）。</summary>
    private static readonly ConcurrentDictionary<(Color Color, double Thickness), Pen> s_penCache = new();

    /// <summary>ブラシキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（DragRectControl.AvaloniaUI.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int BrushCacheCapacity = 128;

    /// <summary>ペンキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（DragRectControl.AvaloniaUI.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int PenCacheCapacity = 128;

    /// <summary>色に対応するブラシを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    /// <remarks>容量管理（容量制限・超過時全クリア）は <see cref="BrushCache"/> へ委譲する（単一所有・DRY）。</remarks>
    private static SolidColorBrush GetBrush(Color color)
        => s_brushCache.GetOrCreate(color);

    /// <summary>色・太さに対応するペンを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ペンの色。</param>
    /// <param name="thickness">ペンの太さ。</param>
    /// <returns>色・太さに対応する <see cref="Pen"/>。</returns>
    /// <remarks>容量超過時はキャッシュを全クリアしてから登録する（<see cref="AddWithCapacityLimit{T}"/> に委譲）。</remarks>
    private static Pen GetPen(Color color, double thickness)
    {
        var key = (color, thickness);
        if (s_penCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var pen = new Pen(GetBrush(color), thickness);
        AddWithCapacityLimit(s_penCache, PenCacheCapacity, key, pen);
        return pen;
    }
    /// <summary>描画対象のハンドル種別（8 方向。None / Move を除く）。</summary>
    /// <remarks>一覧はコア（<see cref="SelectionGeometry.DirectionalHandles"/>）が単一所有する。</remarks>
    private static DragHandle[] HandleTypes => SelectionGeometry.DirectionalHandles;

    /// <summary>容量制限付きキャッシュへ値を登録する（超過時は全クリアしてから登録）。</summary>
    /// <typeparam name="TKey">キャッシュのキー型。</typeparam>
    /// <typeparam name="TValue">キャッシュの値型。</typeparam>
    /// <param name="cache">対象キャッシュ。</param>
    /// <param name="capacity">キャッシュの最大容量。</param>
    /// <param name="key">登録キー。</param>
    /// <param name="value">登録値。</param>
    /// <remarks>
    /// 容量超過時は全クリアして無制限の増加を防ぐ（同一キーは直後に再キャッシュされる）。<br/>
    /// ブラシ・ペンの 2 つのキャッシュで同一の登録ロジックを共有する（DRY・容量管理の単一所有）。
    /// </remarks>
    private static void AddWithCapacityLimit<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> cache, int capacity, TKey key, TValue value)
        where TKey : notnull
    {
        if (cache.Count >= capacity)
        {
            cache.Clear();
        }
        cache[key] = value;
    }

    /// <summary>選択枠線を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="rect">選択矩形。</param>
    /// <param name="borderColor">枠線の色（不透明度は色のアルファに統合）。</param>
    /// <param name="borderThickness">枠線の太さ。</param>
    public static void DrawBorder(DrawingContext context, Rect rect, Color borderColor, double borderThickness)
        => context.DrawRectangle(GetPen(borderColor, borderThickness), rect);

    /// <summary>ドラッグハンドル（白色矩形＋枠線）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="rect">選択矩形。</param>
    /// <param name="handleSize">ハンドルの一辺サイズ。</param>
    /// <param name="handleColor">ハンドルの色。デフォルト（透明）は白。</param>
    /// <param name="borderColor">枠線の色（ハンドル枠に使用）。</param>
    /// <remarks>
    /// 8方向のハンドル（None / Move を除く）を列挙し、<see cref="Selection.SelectionGeometry.GetHandlePosition"/> から<br/>
    /// 中心位置を取得して描画する。新ハンドル追加時は DragHandle の追加のみで描画が追従する（OCP）。
    /// </remarks>
    public static void DrawHandles(DrawingContext context, Rect rect, double handleSize, Color handleColor, Color borderColor)
    {
        var handleFill = GetBrush(handleColor == default ? Colors.White : handleColor);
        var handlePen = GetPen(borderColor, DragRectAppearance.HandlePenThickness);
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

    /// <summary>カーソルの右側にカーソルのドメイン座標ラベル（"(x, y)"）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="cursorDip">カーソルのローカル DIP 座標。</param>
    /// <param name="domainPos">カーソルのドメイン座標（表示する値）。</param>
    /// <param name="clientSize">描画領域のサイズ（はみ出しクランプ用）。</param>
    /// <param name="textColor">ラベルの文字色。</param>
    /// <param name="bgColor">ラベルの背景色（不透明度は色のアルファに統合）。</param>
    /// <param name="fontSize">ラベルのフォントサイズ。</param>
    /// <remarks>
    /// カーソルの右側（+8px オフセット）に座標テキストを背景付きで描画する。<br/>
    /// ラベルが描画領域の右端・下端をはみ出す場合は、領域内へクランプして表示する（ラベル全体が見える）。<br/>
    /// 座標は整数に丸めて "(x, y)" 形式で表示する（ピクセル座標として可読性を優先）。
    /// </remarks>
    public static void DrawCoordLabel(
        DrawingContext context,
        Point cursorDip,
        Point domainPos,
        Size clientSize,
        Color textColor,
        Color bgColor,
        double fontSize)
    {
        var text = CoordinateFormatter.Format(domainPos.ToEsPoint());
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            GetBrush(textColor));

        var pad = DragRectAppearance.LabelPadding;
        var w = ft.Width + pad * 2;
        var h = ft.Height + pad * 2;
        // ラベル配置（カーソル右側 + クランプ）はコアの純粋関数へ委譲する
        var labelPos = DragRectAppearance.ComputeCoordLabelPosition(
            cursorDip.ToEsPoint(), clientSize.ToEsSize(), w, h).ToAvaloniaPoint();

        // 不透明度は bgColor のアルファに統合済み（SolidColorBrush がアルファを反映する）
        context.FillRectangle(GetBrush(bgColor), new Rect(labelPos.X, labelPos.Y, w, h));
        context.DrawText(ft, new Point(labelPos.X + pad, labelPos.Y + pad));
    }
}




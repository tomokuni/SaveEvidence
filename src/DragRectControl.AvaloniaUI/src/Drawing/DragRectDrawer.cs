using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.DragRect;
using Estable.DragRect.Selection;
using Estable.Platform.Geometry;
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
    /// <remarks>PlatformFactory の <see cref="BrushCache"/> へ委譲する（LoupeControl と共用・重複排除・DRY）。</remarks>
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
    /// <summary>描画対象のハンドル種別（8 方向。None / Move を除く）の静的配列。</summary>
    /// <remarks>
    /// <see cref="Enum.GetValues{T}()"/> は呼び出し毎に配列を生成するため、
    /// 描画ホットパスでの毎回の列挙を排除する（<see cref="DrawHandles"/> で使用）。
    /// </remarks>
    private static readonly DragHandle[] s_handleTypes =
        [.. Enum.GetValues<DragHandle>().Where(h => h != DragHandle.None && h != DragHandle.Move)];

    /// <summary>ハンドル枠線のペン太さ（DIP）。</summary>
    private const double HandlePenThickness = 1.5;

    /// <summary>座標ラベルのパディング（DIP）。</summary>
    private const double LabelPadding = 4.0;

    /// <summary>座標ラベルのカーソルからのオフセット（DIP）。</summary>
    private const double LabelOffset = 8.0;
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
        var handlePen = GetPen(borderColor, HandlePenThickness);
        double hs = handleSize;
        double hh = hs / 2;

        // ドメイン計算 (SelectionGeometry) は UI 非依存のため、コア型へ変換して渡し、結果のみ DIP へ戻す。
        var domainRect = rect.ToEsRect();
        foreach (var handle in s_handleTypes)
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

        var pad = LabelPadding;
        var w = ft.Width + pad * 2;
        var h = ft.Height + pad * 2;
        // ラベル配置（カーソル右側 + クランプ）は純粋関数に委譲
        var labelPos = ComputeCoordLabelPosition(cursorDip, clientSize, w, h);

        // 不透明度は bgColor のアルファに統合済み（SolidColorBrush がアルファを反映する）
        context.FillRectangle(GetBrush(bgColor), new Rect(labelPos.X, labelPos.Y, w, h));
        context.DrawText(ft, new Point(labelPos.X + pad, labelPos.Y + pad));
    }

    /// <summary>カーソル座標ラベルの左上位置を計算する（純粋関数）。</summary>
    /// <param name="cursorDip">カーソルのローカル DIP 座標。</param>
    /// <param name="clientSize">描画領域のサイズ（はみ出しクランプ用）。</param>
    /// <param name="labelWidth">ラベルの幅（DIP）。</param>
    /// <param name="labelHeight">ラベルの高さ（DIP）。</param>
    /// <returns>ラベルの左上位置。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// カーソルの右側（+8px オフセット）に配置し、描画領域の右端・下端をはみ出す場合は<br/>
    /// 領域内へクランプする（ラベル全体が見えるよう左寄せ・上寄せ）。
    /// </remarks>
    public static Point ComputeCoordLabelPosition(Point cursorDip, Size clientSize, double labelWidth, double labelHeight)
    {
        double x = cursorDip.X + LabelOffset;
        double y = cursorDip.Y + LabelOffset;
        if (clientSize.Width > 0) x = Math.Min(x, Math.Max(0, clientSize.Width - labelWidth));
        if (clientSize.Height > 0) y = Math.Min(y, Math.Max(0, clientSize.Height - labelHeight));
        return new Point(x, y);
    }
}




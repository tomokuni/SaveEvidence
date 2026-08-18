using Avalonia;
using Avalonia.Media;
using System.Collections.Concurrent;
using System.Globalization;

namespace Estable.AvaloniaUI.FadeMask;

/// <summary>領域選択時の暗転オーバーレイ（穴あけマスク）描画ロジックを提供する静的クラス。</summary>
/// <remarks>
/// 暗転マスク（4辺の穴あけ）とサイズラベルの描画を担当する。<br/>
/// 選択枠線・ドラッグハンドル・始点マーカーは DragRect の責務（<see cref="DragRectDrawer"/>）が担う。<br/>
/// <see cref="FadeMaskControl"/> の <see cref="FadeMaskControl.Render"/> と、<br/>
/// OverlayWindow の <c>Render()</c> オーバーライドの両方から呼び出される。<br/>
/// </remarks>
internal static class FadeMaskDrawer
{
    /// <summary>ブラシキャッシュ（色 → ブラシ。容量制限付き）。</summary>
    private static readonly ConcurrentDictionary<Color, SolidColorBrush> s_brushCache = new();

    /// <summary>ブラシキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（FadeMaskControl.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int BrushCacheCapacity = 128;

    /// <summary>サイズラベルの内側余白（DIP）。</summary>
    private const double LabelPadding = 4.0;

    /// <summary>ラベル背景の既定色（黒 50%。不透明度はアルファに統合）。</summary>
    private static readonly Color DefaultLabelBackground = Color.FromArgb(128, 0, 0, 0);

    /// <summary>色に対応するブラシを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    /// <remarks>容量超過時はキャッシュを全クリアしてから登録する（<see cref="AddWithCapacityLimit{TKey, TValue}"/> に委譲）。</remarks>
    internal static SolidColorBrush GetBrush(Color color)
    {
        if (s_brushCache.TryGetValue(color, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(color);
        AddWithCapacityLimit(s_brushCache, BrushCacheCapacity, color, brush);
        return brush;
    }

    /// <summary>容量制限付きキャッシュへ値を登録する（超過時は全クリアしてから登録）。</summary>
    /// <typeparam name="TKey">キャッシュのキー型。</typeparam>
    /// <typeparam name="TValue">キャッシュの値型。</typeparam>
    /// <param name="cache">対象キャッシュ。</param>
    /// <param name="capacity">キャッシュの最大容量。</param>
    /// <param name="key">登録キー。</param>
    /// <param name="value">登録値。</param>
    /// <remarks>
    /// 容量超過時は全クリアして無制限の増加を防ぐ（同一キーは直後に再キャッシュされる）。
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
    /// <summary>サイズラベルの下に表示するサブライン（2 行目以降）の 1 行分の内容。</summary>
    /// <remarks>
    /// 各行でフォントサイズ・文字色・背景色を個別に指定できる（1 行目 = サイズラベルと同等の設定）。<br/>
    /// 背景色の不透明度は <paramref name="BackgroundColor"/> のアルファに統合する（省略時は黒 50%）。
    /// </remarks>
    public readonly record struct SubLine(
        string Text,
        double FontSize,
        Color Color,
        Color BackgroundColor = default);

    /// <summary>オーバーレイ描画の共通ロジック（暗転マスク＋サイズラベル）。</summary>
    /// <param name="context">描画コンテキスト</param>
    /// <param name="clientRect">描画対象のクライアント矩形（通常は (0,0,Bounds.Width,Bounds.Height)）</param>
    /// <param name="selectionRect">選択矩形。null の場合は全面暗転</param>
    /// <param name="overlayColor">暗転オーバーレイの色</param>
    /// <param name="textColor">ラベルテキストの色</param>
    /// <param name="fontSize">ラベルのフォントサイズ</param>
    /// <param name="showSizeLabel">サイズラベルを表示するか</param>
    /// <param name="sizeLabelOverride">サイズラベルのテキストを明示指定する。
    /// null の場合は <paramref name="selectionRect"/> から自動生成される。
    /// MainWindow の切出しモードでは元画像座標系の値が渡される。</param>
    /// <param name="scaling">DPI スケーリング係数（1.0=100%, 1.25=125%）。
    /// サイズラベル自動生成時に DIP→物理ピクセル変換に使用される。</param>
    /// <param name="subLines">サイズラベルの下に追加表示するサブライン（2 行目以降）。
    /// 例: 操作方法・スクリーン番号・ウィンドウタイトル。各行でフォントサイズ・色・背景色を個別指定できる。</param>
    /// <param name="textBgColor">サイズラベル（1 行目）の背景色（不透明度は色のアルファに統合）。省略時は黒 50%。</param>
    public static void DrawOverlay(
        DrawingContext context,
        Rect clientRect,
        Rect? selectionRect,
        Color overlayColor,
        Color textColor,
        double fontSize,
        bool showSizeLabel,
        string? sizeLabelOverride = null,
        double scaling = 1.0,
        IReadOnlyList<SubLine>? subLines = null,
        Color textBgColor = default)
    {
        if (selectionRect.HasValue)
        {
            var sr = selectionRect.Value;

            // ---- 4辺の穴あけマスク（矩形計算は純粋関数に委譲） ----
            var overlayBrush = GetBrush(overlayColor);
            foreach (var rect in GetMaskRects(clientRect, sr))
            {
                context.FillRectangle(overlayBrush, rect);
            }

            // ---- サイズラベル（1 行目） + サブライン（2 行目以降） ----
            // サイズラベルが非表示の場合は、サブラインを 1 行目から描画する（繰り上げ）。
            double y = sr.Y + LabelPadding; // 描画開始位置（サイズラベル非表示時はサブラインがここから始まる）

            if (showSizeLabel)
            {
                string sizeText;
                if (sizeLabelOverride is not null)
                {
                    sizeText = sizeLabelOverride;
                }
                else
                {
                    sizeText = FormatSizeLabel(sr, scaling);
                }
                y = DrawLabelLine(context, sr.X, y, sizeText, fontSize, textColor, textBgColor);
            }

            // ---- サブライン（2 行目以降。操作方法・タイトル。各行でフォント/色/背景を個別指定） ----
            if (subLines is { Count: > 0 })
            {
                foreach (var sub in subLines)
                {
                    y = DrawLabelLine(context, sr.X, y, sub.Text, sub.FontSize, sub.Color, sub.BackgroundColor);
                }
            }
        }
        else
        {
            // 選択矩形なし → 全面暗転
            context.FillRectangle(GetBrush(overlayColor), clientRect);
        }
    }

    /// <summary>ラベル 1 行（テキスト + 背景）を描画し、次の行の Y 座標を返す。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="x">ラベル領域の左端（選択矩形の X）。</param>
    /// <param name="y">ラベル行の先頭 Y。</param>
    /// <param name="text">ラベルのテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">テキストの色。</param>
    /// <param name="backgroundColor">背景の色（不透明度は色のアルファに統合。省略時 default = 黒 50%）。</param>
    /// <returns>次の行の先頭 Y 座標（= 本行の下端 + 内側余白）。</returns>
    /// <remarks>
    /// サイズラベル（1 行目）とサブライン（2 行目以降）で共用する。<br/>
    /// 背景色は <paramref name="backgroundColor"/> が default の場合に黒 50% へフォールバックする。<br/>
    /// 描画矩形はテキスト幅に内側余白（<see cref="LabelPadding"/> ×2）を加えたサイズとする。
    /// </remarks>
    private static double DrawLabelLine(
        DrawingContext context, double x, double y, string text,
        double fontSize, Color textColor, Color backgroundColor)
    {
        var pad = LabelPadding;
        var pad2 = pad * 2;
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            GetBrush(textColor));
        var bgBrush = GetBrush(backgroundColor == default ? DefaultLabelBackground : backgroundColor);
        var rect = new Rect(x, y, ft.Width + pad2, ft.Height + pad2);
        context.FillRectangle(bgBrush, rect);
        context.DrawText(ft, new Point(x + pad2, y + pad));
        return rect.Bottom + pad;
    }

    /// <summary>選択矩形に対する穴あけマスク（暗転オーバーレイ）の 4 辺矩形を計算する（純粋関数）。</summary>
    /// <param name="clientRect">描画対象のクライアント矩形（通常は (0,0,Bounds.Width,Bounds.Height)）。</param>
    /// <param name="selectionRect">選択矩形（穴あけ領域）。</param>
    /// <returns>描画する矩形のコレクション。上・下・左・右の順。幅・高さが 0 以下の辺は含まない。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 選択矩形がクライアント端に接する場合、その辺の矩形は生成されない（幅・高さ 0 の除外）。
    /// </remarks>
    public static IReadOnlyList<Rect> GetMaskRects(Rect clientRect, Rect selectionRect)
    {
        // 同一スコープで複数回参照される矩形の座標・サイズはローカル変数へ一度だけ格納する
        var cw = clientRect.Width;
        var ch = clientRect.Height;
        var selTop = selectionRect.Top;
        var selBottom = selectionRect.Bottom;
        var selLeft = selectionRect.Left;
        var selRight = selectionRect.Right;
        var selHeight = selectionRect.Height;

        var rects = new List<Rect>(4);

        // 上
        if (selTop > 0)
            rects.Add(new Rect(0, 0, cw, selTop));
        // 下
        if (selBottom < ch)
            rects.Add(new Rect(0, selBottom, cw, ch - selBottom));
        // 左
        if (selLeft > 0)
            rects.Add(new Rect(0, selTop, selLeft, selHeight));
        // 右
        if (selRight < cw)
            rects.Add(new Rect(selRight, selTop, cw - selRight, selHeight));

        return rects;
    }

    /// <summary>サイズラベルのテキストを生成する（純粋関数・DIP → 物理ピクセル変換）。</summary>
    /// <param name="selectionRect">選択矩形（DIP 座標）。</param>
    /// <param name="scaling">DPI スケーリング係数（1.0=100%, 1.25=125%）。0 以下は 1.0 として扱う。</param>
    /// <returns>"(L, T) - (R, B),  (W×H), DisplayScale: (P%)" 形式のテキスト。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 座標・サイズは物理ピクセル（<c>DIP × scaling</c> の整数切り捨て）で表示する。
    /// </remarks>
    public static string FormatSizeLabel(Rect selectionRect, double scaling)
    {
        var s = scaling > 0 ? scaling : 1.0;
        int physL = (int)(selectionRect.X * s);
        int physT = (int)(selectionRect.Y * s);
        int physR = (int)(selectionRect.Right * s);
        int physB = (int)(selectionRect.Bottom * s);
        int physW = (int)(selectionRect.Width * s);
        int physH = (int)(selectionRect.Height * s);
        int pct = (int)(s * 100);
        return $"({physL}, {physT}) - ({physR}, {physB}),  ({physW}×{physH}), DisplayScale: ({pct}%)";
    }
}

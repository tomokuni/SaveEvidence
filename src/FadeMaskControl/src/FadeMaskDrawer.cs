using Avalonia;
using Avalonia.Media;
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
            var overlayBrush = new SolidColorBrush(overlayColor);
            foreach (var rect in GetMaskRects(clientRect, sr))
            {
                context.FillRectangle(overlayBrush, rect);
            }

            // ---- サイズラベル（1 行目） + サブライン（2 行目以降） ----
            // サイズラベルが非表示の場合は、サブラインを 1 行目から描画する（繰り上げ）。
            var pad = 4.0;
            // 1 行目（サイズラベル）の背景（textBgColor は省略時 default = 黒 50%。不透明度は色のアルファに統合）
            var textBgBrush = new SolidColorBrush(textBgColor == default ? Color.FromArgb(128, 0, 0, 0) : textBgColor);
            double y = sr.Y + pad; // 描画開始位置（サイズラベル非表示時はサブラインがここから始まる）

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
                var ft = new FormattedText(
                    sizeText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    fontSize,
                    new SolidColorBrush(textColor));
                var textRect = new Rect(sr.X + pad, y, ft.Width + pad * 2, ft.Height + pad * 2);
                context.FillRectangle(textBgBrush, textRect);
                context.DrawText(ft, new Point(sr.X + pad * 2, y + pad));
                y = textRect.Bottom + pad;
            }

            // ---- サブライン（2 行目以降。操作方法・タイトル。各行でフォント/色/背景を個別指定） ----
            if (subLines is { Count: > 0 })
            {
                foreach (var sub in subLines)
                {
                    var lineFt = new FormattedText(
                        sub.Text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        sub.FontSize,
                        new SolidColorBrush(sub.Color));
                    // サブラインの背景（各行の背景色。不透明度は色のアルファに統合。省略時は黒 50%）
                    var lineBgBrush = new SolidColorBrush(
                        sub.BackgroundColor == default ? Color.FromArgb(128, 0, 0, 0) : sub.BackgroundColor);
                    // サブラインはサイズラベルと同じ幅の背景に描画する（最大幅に合わせて広げる）
                    var lineRect = new Rect(sr.X + pad, y, lineFt.Width + pad * 2, lineFt.Height + pad * 2);
                    context.FillRectangle(lineBgBrush, lineRect);
                    context.DrawText(lineFt, new Point(sr.X + pad * 2, y + pad));
                    y = lineRect.Bottom + pad;
                }
            }
        }
        else
        {
            // 選択矩形なし → 全面暗転
            context.FillRectangle(new SolidColorBrush(overlayColor), clientRect);
        }
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
        var rects = new List<Rect>(4);

        // 上
        if (selectionRect.Top > 0)
            rects.Add(new Rect(0, 0, clientRect.Width, selectionRect.Top));
        // 下
        if (selectionRect.Bottom < clientRect.Height)
            rects.Add(new Rect(0, selectionRect.Bottom, clientRect.Width, clientRect.Height - selectionRect.Bottom));
        // 左
        if (selectionRect.Left > 0)
            rects.Add(new Rect(0, selectionRect.Top, selectionRect.Left, selectionRect.Height));
        // 右
        if (selectionRect.Right < clientRect.Width)
            rects.Add(new Rect(selectionRect.Right, selectionRect.Top, clientRect.Width - selectionRect.Right, selectionRect.Height));

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

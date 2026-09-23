using Avalonia;
using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Geometry;
using Estable.DragRect.Decoration;
using System.Globalization;

namespace Estable.AvaloniaUI.DragRect.Drawing;

/// <summary>選択矩形の外側に描く装飾（暗転マスク・穴あけ枠・ラベル）を描画する静的クラス。</summary>
/// <remarks>
/// MewUI 版の <c>DragRectOuterSkiaRenderer.Draw</c> と同じ引数の形（<see cref="OuterDecoFrame"/> を描くだけ）とする。<br/>
/// 「どの矩形を暗転するか」「枠線を描くか」「ラベルをどう組むか」の判断は装飾コアの <see cref="OuterDecoFrame"/> が持つため、<br/>
/// 本クラスはフレームの各項目を Avalonia の描画 API へ写すだけとする（外観値・判断を持たない）。<br/>
/// テキストの実寸測定のみ UI 固有（<see cref="FormattedText"/>）であり、行の配置はコアが単一所有する。<br/>
/// 選択矩形そのもの（枠線・ドラッグハンドル・始点マーカー・座標ラベル）は <see cref="DragRectDrawer"/> が担う。
/// </remarks>
internal static class DragRectOuterDrawer
{
    /// <summary>ブラシキャッシュ（色 → ブラシ。容量制限付き）。</summary>
    /// <remarks>Platform.AvaloniaUI の <see cref="BrushCache"/> へ委譲する（LoupeControl / DragRectControl と共用・重複排除・DRY）。</remarks>
    private static readonly BrushCache s_brushCache = new(BrushCacheCapacity);

    /// <summary>ブラシキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（DragRectControl.AvaloniaUI.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int BrushCacheCapacity = 128;

    /// <summary>色に対応するブラシを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    private static SolidColorBrush GetBrush(Color color)
        => s_brushCache.GetOrCreate(color);

    /// <summary>暗転マスク・枠線・ラベルを描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>
    /// <remarks>
    /// 1. 暗転領域（<see cref="OuterDecoFrame.OverlayRegions"/>）を暗転色で塗る（空なら何もしない）。<br/>
    /// 2. 枠線（<see cref="OuterDecoFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// 3. ラベル（サイズラベル → サブラインの順）を基準矩形の左上へ縦に積む（基準が無ければ何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。
    /// </remarks>
    public static void Draw(DrawingContext context, in OuterDecoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(context);

        // ---- 暗転マスク（4辺の穴あけ。矩形計算はコアの純粋関数に委譲） ----
        if (frame.OverlayRegions.Count > 0)
        {
            var overlayBrush = GetBrush(frame.OverlayColor.ToAvaloniaColor());
            foreach (var region in frame.OverlayRegions)
            {
                context.FillRectangle(overlayBrush, region.ToAvaloniaRect());
            }
        }

        // ---- 選択枠線（四辺・矩形の内側） ----
        if (frame.BorderRects.Count > 0)
        {
            var borderBrush = GetBrush(frame.BorderColor.ToAvaloniaColor());
            foreach (var rect in frame.BorderRects)
            {
                context.DrawRectangle(borderBrush, null, rect.ToAvaloniaRect());
            }
        }

        // ---- ラベル（1 行目 = サイズラベル、2 行目以降 = サブライン） ----
        // サイズラベルが無い場合はサブラインを 1 行目から描画する（繰り上げ）
        if (frame.LabelAnchor is not { } anchor)
        {
            return;
        }

        var x = anchor.X;
        var y = anchor.Y + OuterDecoLabelLayout.Padding;

        if (frame.SizeLabelText is { } sizeLabelText)
        {
            y = DrawLabelLine(
                context, x, y, sizeLabelText, frame.SizeLabelStyle.FontSize,
                frame.SizeLabelStyle.Color.ToAvaloniaColor(), frame.SizeLabelStyle.BackgroundColor.ToAvaloniaColor());
        }

        foreach (var line in frame.SubLines)
        {
            y = DrawLabelLine(
                context, x, y, line.Text, line.FontSize, line.Color.ToAvaloniaColor(), line.BackgroundColor.ToAvaloniaColor());
        }
    }

    /// <summary>ラベル 1 行（テキスト + 背景）を描画し、次の行の Y 座標を返す。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="x">ラベル領域の左端（穴あけ矩形の X）。</param>
    /// <param name="y">ラベル行の先頭 Y。</param>
    /// <param name="text">ラベルのテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">テキストの色。</param>
    /// <param name="backgroundColor">背景の色（実効色へ解決済み。不透明度は色のアルファに統合）。</param>
    /// <returns>次の行の先頭 Y 座標（= 本行の下端 + 内側余白）。</returns>
    /// <remarks>
    /// サイズラベル（1 行目）とサブライン（2 行目以降）で共用する。<br/>
    /// 背景矩形・文字の描画原点・次の行の位置はコアの <see cref="OuterDecoLabelLayout"/> が単一所有する（MewUI 版と同一の見た目）。
    /// </remarks>
    private static double DrawLabelLine(
        DrawingContext context, double x, double y, string text,
        double fontSize, Color textColor, Color backgroundColor)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            GetBrush(textColor));
        var bgBrush = GetBrush(backgroundColor);

        // 背景・文字原点・次の行は UI 非依存の計算（コア）へ委譲する
        var origin = new EsPoint(x, y);
        var background = OuterDecoLabelLayout.ComputeBackground(origin, new EsSize(ft.Width, ft.Height));
        context.FillRectangle(bgBrush, background.ToAvaloniaRect());

        var textOrigin = OuterDecoLabelLayout.ComputeTextOrigin(origin);
        context.DrawText(ft, new Point(textOrigin.X, textOrigin.Y));

        return OuterDecoLabelLayout.NextLineY(background);
    }
}

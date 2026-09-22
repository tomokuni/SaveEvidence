using Estable.FadeMask;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.FadeMask;

/// <summary>暗転マスク・枠線・ラベルを Skia で描画するレンダラー。</summary>
/// <remarks>
/// Avalonia 版の <c>FadeMaskDrawer.Draw</c> と同じ引数の形（<see cref="FadeMaskFrame"/> を描くだけ）とする。<br/>
/// 「どの矩形を暗転するか」「枠線を描くか」「ラベルをどう組むか」の判断はコアの <see cref="FadeMaskFrame"/> が持つため、<br/>
/// 本クラスはフレームの各項目を <see cref="SKCanvas"/> の描画 API へ写すだけとする（外観値・判断を持たない）。<br/>
/// テキストの実寸測定とフォント解決のみ UI 固有であり、行の配置はコア（<see cref="FadeMaskLabelLayout"/>）が単一所有する。<br/>
/// 座標はすべて DIP で扱い、呼び出し側が DPI スケーリングを適用済みであることを前提とする。
/// </remarks>
internal static class FadeMaskSkiaRenderer
{
    /// <summary>非 ASCII（日本語等）を含むテキスト用の予備フォント（初回のみ解決する）。</summary>
    private static SKTypeface? s_fallbackTypeface;

    /// <summary>暗転マスク・枠線・ラベルを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>
    /// <remarks>
    /// 1. 暗転領域（<see cref="FadeMaskFrame.OverlayRegions"/>）を暗転色で塗る（空なら何もしない）。<br/>
    /// 2. 枠線（<see cref="FadeMaskFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// 3. ラベル（サイズラベル → サブラインの順）を基準矩形の左上へ縦に積む（基準が無ければ何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。
    /// </remarks>
    public static void Draw(SKCanvas canvas, in FadeMaskFrame frame)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // ---- 暗転マスク（4辺の穴あけ。矩形計算はコアの純粋関数に委譲） ----
        if (frame.OverlayRegions.Count > 0)
        {
            using var overlayPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = ToSKColor(frame.OverlayColor),
            };

            foreach (var region in frame.OverlayRegions)
            {
                canvas.DrawRect(ToSKRect(region), overlayPaint);
            }
        }

        // ---- 選択枠線（四辺・矩形の内側） ----
        if (frame.BorderRects.Count > 0)
        {
            using var borderPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = ToSKColor(frame.BorderColor),
            };

            foreach (var rect in frame.BorderRects)
            {
                canvas.DrawRect(ToSKRect(rect), borderPaint);
            }
        }

        // ---- ラベル（1 行目 = サイズラベル、2 行目以降 = サブライン） ----
        // サイズラベルが無い場合はサブラインを 1 行目から描画する（繰り上げ）
        if (frame.LabelAnchor is not { } anchor)
        {
            return;
        }

        var origin = new EsPoint(anchor.X, anchor.Y + FadeMaskLabelLayout.Padding);

        if (frame.SizeLabelText is { } sizeLabelText)
        {
            origin = DrawLabelLine(
                canvas, origin, sizeLabelText, frame.SizeLabelStyle.FontSize,
                frame.SizeLabelStyle.Color, frame.SizeLabelStyle.BackgroundColor);
        }

        foreach (var line in frame.SubLines)
        {
            origin = DrawLabelLine(canvas, origin, line.Text, line.FontSize, line.Color, line.BackgroundColor);
        }
    }

    /// <summary>ラベル 1 行（背景 + テキスト）を描画し、次の行の原点を返す。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="origin">ラベル行の原点（DIP）。</param>
    /// <param name="text">描画するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="backgroundColor">背景色（実効色へ解決済み）。</param>
    /// <returns>次の行の原点（DIP）。</returns>
    /// <remarks>
    /// サイズラベル（1 行目）とサブライン（2 行目以降）で共用する。<br/>
    /// 背景・文字位置・次の行の位置はコアの <see cref="FadeMaskLabelLayout"/> が単一所有する（Avalonia 版と同一の見た目）。
    /// </remarks>
    private static EsPoint DrawLabelLine(
        SKCanvas canvas, EsPoint origin, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor)
    {
        using var font = new SKFont(ResolveTypeface(text), (float)fontSize);
        using var textPaint = new SKPaint { IsAntialias = true, Color = ToSKColor(textColor) };
        using var backgroundPaint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = ToSKColor(backgroundColor),
        };

        var textSize = new EsSize(font.MeasureText(text, textPaint), font.Metrics.Descent - font.Metrics.Ascent);
        var background = FadeMaskLabelLayout.ComputeBackground(origin, textSize);
        canvas.DrawRect(ToSKRect(background), backgroundPaint);

        var textOrigin = FadeMaskLabelLayout.ComputeTextOrigin(origin);
        canvas.DrawText(text, (float)textOrigin.X, (float)(textOrigin.Y - font.Metrics.Ascent), font, textPaint);

        return new EsPoint(origin.X, FadeMaskLabelLayout.NextLineY(background));
    }

    /// <summary>コア幾何型の矩形を Skia の矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>Skia の矩形。</returns>
    private static SKRect ToSKRect(EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

    /// <summary>テキストの描画に使うフォントを解決する。</summary>
    /// <param name="text">描画するテキスト。</param>
    /// <returns>フォント。</returns>
    /// <remarks>
    /// <see cref="SKTypeface.Default"/> は日本語等のグリフを持たない環境があり、その場合は豆腐（□）になる（実測）。<br/>
    /// ASCII のみのテキストは既定のフォントを使い、非 ASCII を含む場合は対応するフォントを一度だけ解決して再利用する。<br/>
    /// ラベルはドラッグ中に毎フレーム描画されるため、解決はキャッシュする。
    /// </remarks>
    internal static SKTypeface ResolveTypeface(string text)
    {
        if (IsAsciiOnly(text))
        {
            return SKTypeface.Default;
        }

        s_fallbackTypeface ??= ResolveFallbackTypeface();
        return s_fallbackTypeface ?? SKTypeface.Default;
    }

    /// <summary>非 ASCII（日本語等）を含む文字列用の予備フォントを解決する。</summary>
    /// <returns>解決したフォント。見つからない場合は null。</returns>
    /// <remarks>代表的な日本語文字で字形を持つフォントを探す（見つからなければ既定のフォントで描画する）。</remarks>
    private static SKTypeface? ResolveFallbackTypeface()
    {
        foreach (var character in "あ漢")
        {
            if (SKFontManager.Default.MatchCharacter(character) is { } typeface)
            {
                return typeface;
            }
        }

        return null;
    }

    /// <summary>文字列が ASCII のみで構成されるかどうかを判定する。</summary>
    /// <param name="text">判定する文字列。</param>
    /// <returns>ASCII のみの場合は true。</returns>
    internal static bool IsAsciiOnly(string text)
    {
        foreach (var character in text)
        {
            if (character > '\u007F')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>コアの色を Skia の色へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>Skia の色。</returns>
    private static SKColor ToSKColor(DrawingColor color) => new(color.R, color.G, color.B, color.A);
}

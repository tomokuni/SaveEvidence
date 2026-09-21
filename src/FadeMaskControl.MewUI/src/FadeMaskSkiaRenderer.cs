using Estable.FadeMask;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.FadeMask;

/// <summary>暗転オーバーレイ（穴あけ + 枠線）を Skia で描画するレンダラー。</summary>
/// <remarks>
/// UI 非依存コア（<see cref="FadeMaskOverlayGeometry"/>）の計算結果を <see cref="SKCanvas"/> へ描画するだけの層。<br/>
/// 穴あけ矩形がある場合は「クライアント矩形 − 穴あけ矩形」の帯状の領域のみを塗る（穴あけ部分を描画しない）。<br/>
/// 座標はすべて DIP で扱い、呼び出し側が DPI スケーリングを適用済みであることを前提とする。
/// </remarks>
internal static class FadeMaskSkiaRenderer
{
    /// <summary>ラベルの背景色の既定値（黒 50%）。背景色が未指定（アルファ 0）の場合に使用する。</summary>
    private static readonly DrawingColor s_defaultLabelBackground = DrawingColor.FromArgb(128, 0, 0, 0);

    /// <summary>非 ASCII（日本語等）を含むテキスト用の予備フォント（初回のみ解決する）。</summary>
    private static SKTypeface? s_fallbackTypeface;

    /// <summary>暗転オーバーレイと枠線を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="client">クライアント矩形（DIP）。</param>
    /// <param name="holeRect">穴あけ矩形（DIP）。null の場合は全面を暗転する。</param>
    /// <param name="overlayColor">暗転の色（アルファを含む）。</param>
    /// <param name="borderColor">枠線の色（アルファを含む）。</param>
    /// <param name="borderThickness">枠線の太さ（DIP）。</param>
    /// <param name="showFullOverlay">穴あけ矩形が無くても全面を暗転するか。</param>
    /// <remarks>
    /// 1. 暗転の要否をコアの判定で決める。<br/>
    /// 2. 暗転は穴あけ矩形を除いた領域のみを塗る（アルファが 0 の場合は塗らない）。<br/>
    /// 3. 枠線は四辺を矩形の内側に描く。
    /// </remarks>
    public static void Render(
        SKCanvas canvas,
        EsRect client,
        EsRect? holeRect,
        DrawingColor overlayColor,
        DrawingColor borderColor,
        double borderThickness,
        bool showFullOverlay)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (!FadeMaskOverlayGeometry.ShouldDrawOverlay(holeRect, showFullOverlay))
        {
            return;
        }

        if (overlayColor.A > 0)
        {
            using var overlayPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = ToSKColor(overlayColor),
            };

            foreach (var region in FadeMaskOverlayGeometry.ComputeOverlayRegions(client, holeRect))
            {
                canvas.DrawRect(ToSKRect(region), overlayPaint);
            }
        }

        if (holeRect is not { } hole || borderColor.A == 0)
        {
            return;
        }

        using var borderPaint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = ToSKColor(borderColor),
        };

        foreach (var rect in FadeMaskOverlayGeometry.GetBorderRects(hole, borderThickness))
        {
            canvas.DrawRect(ToSKRect(rect), borderPaint);
        }
    }

    /// <summary>サイズラベルとサブライン（操作ラベル・情報ラベル）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="anchor">ラベルの基準矩形（穴あけ矩形・DIP）。行列は左上から下向きに積む。</param>
    /// <param name="showSizeLabel">サイズラベル（1 行目）を表示するか。</param>
    /// <param name="sizeLabelText">サイズラベルのテキスト。null の場合はサイズラベルを描画しない。</param>
    /// <param name="sizeLabelFontSize">サイズラベルのフォントサイズ（DIP）。</param>
    /// <param name="sizeLabelColor">サイズラベルの文字色。</param>
    /// <param name="sizeLabelBackgroundColor">サイズラベルの背景色（アルファ 0 の場合は黒 50% へフォールバック）。</param>
    /// <param name="subLines">2 行目以降の行（未指定・空の場合は描画しない）。</param>
    /// <remarks>
    /// 1. サイズラベルが非表示またはテキスト未指定の場合は、サブラインを 1 行目から描画する（繰り上げ）。<br/>
    /// 2. 各行は「テキスト + 内側余白の背景」を基準矩形の左端から縦に積む（配置はコアの計算へ委譲する）。<br/>
    /// 3. テキストの実寸は Skia のフォントで測定する（余白・次の行の位置はコアが単一所有する）。
    /// </remarks>
    public static void DrawLabels(
        SKCanvas canvas,
        EsRect anchor,
        bool showSizeLabel,
        string? sizeLabelText,
        double sizeLabelFontSize,
        DrawingColor sizeLabelColor,
        DrawingColor sizeLabelBackgroundColor,
        IReadOnlyList<FadeMaskSubLine>? subLines)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // 行列の原点（1 行目）。Avalonia 版と同一の見た目になるよう、余白はコアの計算を使う
        var origin = new EsPoint(anchor.X, anchor.Y + FadeMaskLabelLayout.Padding);

        if (showSizeLabel && !string.IsNullOrEmpty(sizeLabelText))
        {
            origin = DrawLabelLine(
                canvas, origin, sizeLabelText, sizeLabelFontSize, sizeLabelColor, sizeLabelBackgroundColor);
        }

        if (subLines is not { Count: > 0 })
        {
            return;
        }

        foreach (var line in subLines)
        {
            origin = DrawLabelLine(
                canvas, origin, line.Text, line.FontSize, line.Color, line.BackgroundColor);
        }
    }

    /// <summary>ラベル 1 行（背景 + テキスト）を描画し、次の行の原点を返す。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="origin">ラベル行の原点（DIP）。</param>
    /// <param name="text">描画するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="backgroundColor">背景色（アルファ 0 の場合は黒 50% へフォールバック）。</param>
    /// <returns>次の行の原点（DIP）。</returns>
    /// <remarks>背景・文字位置・次の行の位置は <see cref="FadeMaskLabelLayout"/> が単一所有する。</remarks>
    private static EsPoint DrawLabelLine(
        SKCanvas canvas, EsPoint origin, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor)
    {
        using var font = new SKFont(ResolveTypeface(text), (float)fontSize);
        using var textPaint = new SKPaint { IsAntialias = true, Color = ToSKColor(textColor) };
        using var backgroundPaint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = ToSKColor(backgroundColor.A == 0 ? s_defaultLabelBackground : backgroundColor),
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

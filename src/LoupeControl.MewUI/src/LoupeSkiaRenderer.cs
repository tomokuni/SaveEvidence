using Estable.Loupe;
using Estable.Loupe.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Loupe;

/// <summary>ルーペの描画（市松模様背景 + 拡大画像 + 十字線 + 外枠 + 座標ラベル）を Skia で行うレンダラー。</summary>
/// <remarks>
/// 描画の順序と、描画に必要な値の単位はコアの契約（<see cref="LoupeRenderRequest"/>）が単一所有する。<br/>
/// UI 非依存コア（<see cref="LoupeAppearance"/> / <see cref="LoupeImageExtractor"/> / <see cref="LoupeDecorationGeometry"/> /<br/>
/// <see cref="LoupeCheckerboard"/> / <see cref="LoupeLabelLayout"/>）の値と計算結果を <see cref="SKCanvas"/> へ描画するだけの層。<br/>
/// 外観値（色・太さ・タイルサイズ・ラベル）は <see cref="LoupeAppearance"/> が単一所有し、本クラスは保持しない。<br/>
/// Avalonia 版は中間ビットマップ（<c>RenderTargetBitmap</c>）を経由するが、MewUI 版は画面のキャンバスへ<br/>
/// 直接描画できるため、中間バッファの生成とコピーを行わない。<br/>
/// 矩形計算はコアが単一所有するため、両 UI の見た目は一致する（Avalonia 版のレンダラーと等価）。
/// </remarks>
internal static class LoupeSkiaRenderer
{
    /// <summary>ルーペを描画する（市松模様背景 + 拡大画像 + 十字線 + 外枠 + 座標ラベル）。</summary>
    /// <param name="canvas">描画先のキャンバス（左上を原点とする）。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は市松模様のみ。</param>
    /// <param name="request">描画入力（コア⇔UI の契約。UI が公開プロパティから組み立てる）。</param>
    /// <remarks>
    /// 0. 下地（ベース色。透明なら描かない）を描画する。<br/>
    /// 1. 市松模様の背景を描画する。<br/>
    /// 2. ソース画像があれば切り出して拡大描画する（切り出しはコアへ委譲。範囲外は透明パディング）。<br/>
    /// 3. 十字線を描画する。<br/>
    /// 4. 外枠を描画する。<br/>
    /// 5. 座標ラベルを描画する（拡大画像の上へ重ねる）。<br/>
    /// 外観の DIP 値は描画時に物理ピクセルへ換算する（DPI 差で見た目を変えない）。
    /// </remarks>
    public static void Render(SKCanvas canvas, EsBitmap? source, LoupeRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(request.Appearance);

        // 下地（ベース色）→ 市松模様の背景（タイルサイズの補正・DIP → 物理ピクセルの換算はコアが行う）
        DrawBackground(canvas, request);
        DrawCheckerboard(canvas, request);
        DrawMagnified(canvas, source, request);
        DrawCrosshair(canvas, request);
        DrawFrame(canvas, request);
        DrawCoordLabel(canvas, request);
    }

    /// <summary>下地（ベース色）を表面全体へ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（外観）。</param>
    /// <remarks>
    /// 市松模様の色は半透明のため、背後の要素（プレビューの画像・画面の平均色等）を透かしたくない場合はホストが不透明色を指定する。<br/>
    /// 表面全体を塗り替える（透明の場合も前フレームの画素を残さない）ため、キャンバスのクリアを使う。
    /// </remarks>
    private static void DrawBackground(SKCanvas canvas, LoupeRenderRequest request)
        => canvas.Clear(request.Appearance.BackgroundColor.ToSKColor());

    /// <summary>中心座標ラベルを上端中央へ描画する（拡大画像の上へ重ねる）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大中心・外観・スケーリング）。</param>
    /// <remarks>
    /// 描画の可否（<see cref="LoupeAppearance.ShowCoordLabel"/> と中心の有無の組合せ）は
    /// <see cref="LoupeAppearance.ShouldShowCoordLabel"/>（コア）が単一所有し、本メソッドは結果で描画するだけにする。<br/>
    /// 余白・背景矩形・文字原点は <see cref="LoupeLabelLayout"/>（コア）が、文字サイズと角丸の物理ピクセル換算は
    /// <see cref="LoupeAppearanceScaler"/>（コア）が単一所有する。<br/>
    /// 座標の文字列化は <see cref="CoordinateFormatter"/>（コア）へ委譲する（Avalonia 版と同一の表記）。
    /// </remarks>
    private static void DrawCoordLabel(SKCanvas canvas, LoupeRenderRequest request)
    {
        var appearance = request.Appearance;
        if (!appearance.ShouldShowCoordLabel(request.Center is not null) || request.Center is not { } center)
        {
            return;
        }

        var text = CoordinateFormatter.Format(center);
        var fontSize = (float)LoupeAppearanceScaler.ScaleCoordLabelFontSize(appearance, request.Scaling);

        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var textPaint = new SKPaint { IsAntialias = true, Color = appearance.CoordLabelColor.ToSKColor() };
        using var backgroundPaint = new SKPaint { IsAntialias = false, Color = appearance.CoordLabelBackgroundColor.ToSKColor() };

        var width = font.MeasureText(text, textPaint);
        var background = LoupeLabelLayout.ComputeBackground(request.PixelSize, width, fontSize, request.Scaling);
        var rect = ToSKRect(background);

        // 背景の角丸はコアの外観値を使う（半径 0 は四角形として描く）
        var cornerRadius = LoupeAppearanceScaler.ScaleCoordLabelCornerRadius(appearance, request.Scaling);
        if (cornerRadius > 0)
        {
            canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, backgroundPaint);
        }
        else
        {
            canvas.DrawRect(rect, backgroundPaint);
        }

        var textOrigin = LoupeLabelLayout.ComputeTextOrigin(request.PixelSize, width, request.Scaling);
        // 文字は左上原点からフォントサイズ分下がった位置がベースラインになる（コアは左上のみを所有する）
        canvas.DrawText(text, (float)textOrigin.X, (float)textOrigin.Y + fontSize, SKTextAlign.Left, font, textPaint);
    }

    /// <summary>コアの矩形を Skia の矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（コア座標）。</param>
    /// <returns>Skia の矩形。</returns>
    /// <remarks>本レンダラー内で唯一の <see cref="EsRect"/> → <see cref="SKRect"/> 変換点とする（変換式の重複を防ぐ）。</remarks>
    private static SKRect ToSKRect(EsRect rect) => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

    /// <summary>市松模様の背景を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// 明色で全面を塗った後、暗色タイルのみを上書きする（描画回数を半減）。<br/>
    /// 描画の可否（アルファ 0 は描画しない）はコアの <see cref="LoupeAppearance.HasCheckerColorA"/> /
    /// <see cref="LoupeAppearance.HasCheckerColorB"/> の判定を呼び出し側が null で表現し、本メソッドは描画だけを行う。<br/>
    /// 暗色タイルの位置（タイル数・座標）とタイルサイズの物理ピクセル換算はコアが単一所有する。
    /// </remarks>
    private static void DrawCheckerboard(SKCanvas canvas, LoupeRenderRequest request)
    {
        var appearance = request.Appearance;
        var colorA = appearance.HasCheckerColorA ? appearance.CheckerColorA : (DrawingColor?)null;
        var colorB = appearance.HasCheckerColorB ? appearance.CheckerColorB : (DrawingColor?)null;

        using var paint = new SKPaint { IsAntialias = false };

        // 色A（明色）で全面を塗る（暗色タイルの下地）。透明の場合は上書きされないため省略する
        if (colorA is { } light)
        {
            paint.Color = light.ToSKColor();
            canvas.DrawRect(new SKRect(0, 0, request.PixelSize, request.PixelSize), paint);
        }

        if (colorB is not { } dark)
        {
            return;
        }

        paint.Color = dark.ToSKColor();

        // タイルサイズの換算と暗色タイルの位置（タイル数・座標）はコアが単一所有する（本クラスは返された矩形を塗るだけ）
        foreach (var tile in LoupeCheckerboard.ComputeDarkTileRects(
            request.PixelSize, LoupeAppearanceScaler.ScaleCheckerSize(appearance, request.Scaling)))
        {
            canvas.DrawRect(ToSKRect(tile), paint);
        }
    }

    /// <summary>ソース画像を切り出して拡大描画する（ソース／中心が未設定なら何もしない）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・拡大中心）。</param>
    /// <remarks>
    /// 切り出しの可否（ソースと中心が揃っているか）と切り出し手順は <see cref="LoupeImageExtractor.TryExtractRegionAt"/>（コア）へ委譲する<br/>
    /// （破棄済みソースの扱いもコアが単一所有し、本メソッドは null なら市松模様のみを残す）。<br/>
    /// 切り出し元・描画先の矩形はコア（<see cref="LoupeImageExtractor.ComputeMagnifiedRects"/>）が単一所有する（Avalonia 版と同一の倍率）。<br/>
    /// <c>ToSKBitmap()</c> は内部インスタンスの共有参照を返すため破棄しない。
    /// </remarks>
    private static void DrawMagnified(SKCanvas canvas, EsBitmap? source, LoupeRenderRequest request)
    {
        using var region = LoupeImageExtractor.TryExtractRegionAt(source, request);
        if (region is null)
        {
            return;
        }

        var (src, dst) = LoupeImageExtractor.ComputeMagnifiedRects(region.Size, request.ZoomLevel);

        // 拡大は最近傍（既定）で行い、画素をそのまま拡大表示する
        canvas.DrawBitmap(region.ToSKBitmap(), ToSKRect(src), ToSKRect(dst));
    }

    /// <summary>十字線を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・外観）。</param>
    /// <remarks>
    /// 矩形の計算はコアが単一所有し、描画の可否（アルファ 0 は描画しない）は
    /// <see cref="LoupeAppearance.HasCrosshair"/> の判定を呼び出し側が null で表現する。
    /// </remarks>
    private static void DrawCrosshair(SKCanvas canvas, LoupeRenderRequest request)
    {
        if (!request.Appearance.HasCrosshair)
        {
            return;
        }

        FillRects(
            canvas,
            LoupeDecorationGeometry.ComputeCrosshairRects(request.PixelSize, request.ZoomLevel),
            request.Appearance.CrossColor.ToSKColor());
    }

    /// <summary>外枠を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// 太さの補正と DIP → 物理ピクセルの換算は <see cref="LoupeAppearanceScaler"/>（コア）が単一所有し、<br/>
    /// 描画の可否（アルファ 0 は描画しない）は <see cref="LoupeAppearance.HasFrame"/> の判定を呼び出し側が表す。
    /// </remarks>
    private static void DrawFrame(SKCanvas canvas, LoupeRenderRequest request)
    {
        if (!request.Appearance.HasFrame)
        {
            return;
        }

        FillRects(
            canvas,
            LoupeDecorationGeometry.ComputeFrameRects(
                request.PixelSize, LoupeAppearanceScaler.ScaleFrameWidth(request.Appearance, request.Scaling)),
            request.Appearance.FrameColor.ToSKColor());
    }

    /// <summary>矩形の一覧を単色で塗りつぶす。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="rects">塗りつぶす矩形（コア座標）。</param>
    /// <param name="color">塗りつぶしの色。</param>
    private static void FillRects(SKCanvas canvas, IReadOnlyList<EsRect> rects, SKColor color)
    {
        using var paint = new SKPaint { IsAntialias = false, Color = color, Style = SKPaintStyle.Fill };

        foreach (var rect in rects)
        {
            canvas.DrawRect(ToSKRect(rect), paint);
        }
    }
}

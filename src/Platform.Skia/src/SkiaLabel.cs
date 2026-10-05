using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Platform.Skia;

/// <summary>Skia のラベル 1 件（背景 + 文字）の描画順を単一所有する。</summary>
/// <remarks>
/// 「背景を塗ってから文字を描く」という順序は、<see cref="SkiaFill"/> と <see cref="SkiaText"/> を<br/>
/// 呼ぶ順序そのものであり、各 UI・各ホストが書くと順序（重なり）がずれうるため本クラスへ集約する。<br/>
/// SkiaSharp は UI フレームワークではなくピクセル・描画の基盤であるため、<see cref="EsBitmap"/> と同じ層（本プロジェクト）が担う。<br/>
/// <b>テキストの実寸測定と配置の計算は呼び出し側が行う</b>（余白・行送り・クランプの規則は各コントロールのコアが単一所有する）。<br/>
/// 背景の角丸にも対応する（角丸半径 0 以下は四角形として塗る）。
/// </remarks>
public static class SkiaLabel
{
    /// <summary>ラベル 1 件（背景 + 文字）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="background">背景の矩形（コア座標）。</param>
    /// <param name="backgroundColor">背景色（不透明度はアルファ）。</param>
    /// <param name="text">描画する文字列。</param>
    /// <param name="textOrigin">文字の左上原点（背景の内側）。</param>
    /// <param name="fontSize">フォントサイズ（物理ピクセル）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="cornerRadius">背景の角丸半径（物理ピクセル）。 0 以下は四角形（既定）。</param>
    /// <remarks>
    /// 1. 背景（<see cref="SkiaFill.RoundRect"/>。 角丸 0 は四角形）を塗る。<br/>
    /// 2. 文字（<see cref="SkiaText.Draw"/>）を左上原点で描く。<br/>
    /// <br/>
    /// 文字は背景の後に描く（先に描くと背景に隠れる）。 本メソッドが順序を保証する。
    /// </remarks>
    public static void Draw(
        SKCanvas canvas,
        EsRect background,
        DrawingColor backgroundColor,
        string text,
        EsPoint textOrigin,
        double fontSize,
        DrawingColor textColor,
        double cornerRadius = 0)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        SkiaFill.RoundRect(canvas, background, cornerRadius, backgroundColor);
        SkiaText.Draw(canvas, text, textOrigin.X, textOrigin.Y, fontSize, textColor);
    }
}

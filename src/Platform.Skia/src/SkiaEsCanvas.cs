using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;

namespace EsUtil.Platform.Skia;

/// <summary><see cref="SKCanvas"/> を <see cref="IEsCanvas"/> へ適合させるアダプタ。</summary>
/// <remarks>
/// SkiaSharp は UI フレームワークではなくピクセル・描画の基盤であるため、本層（<see cref="EsBitmap"/> と同じ層）が<br/>
/// 適合を単一所有する。Skia の <c>SKCanvas</c> を取得できる UI（MewUI 等）は本クラスをそのまま使える。<br/>
/// 描画の実行は既存の <see cref="SkiaFill"/> / <see cref="SkiaText"/> / <see cref="SkiaLabel"/> へ委譲する<br/>
/// （「背景を塗ってから文字を描く」順序の単一所有を保つ）。<br/>
/// <b>座標系</b>: 呼び出し側が定める単位でそのまま描く（スケーリングは呼び出し側がキャンバスへ適用する）。<br/>
/// <b>破棄</b>: 本クラスは <see cref="SKCanvas"/> を所有しない（呼び出し側が破棄する）。
/// </remarks>
public sealed class SkiaEsCanvas : IEsCanvas
{
    /// <summary>描画先のキャンバス（本クラスは所有しない）。</summary>
    private readonly SKCanvas _canvas;

    /// <summary>アダプタを初期化する。</summary>
    /// <param name="canvas">描画先のキャンバス。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    public SkiaEsCanvas(SKCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        _canvas = canvas;
    }

    /// <summary>描画面の全体を指定色で置き換える（前フレームの残像を消す）。</summary>
    /// <param name="surface">描画面の矩形（本実装では使わない。描画面全体を消去する）。</param>
    /// <param name="color">置き換える色（不透明度はアルファ）。</param>
    /// <remarks>
    /// <see cref="SKCanvas.Clear(SKColor)"/> は現在のセーブレイヤーの描画面全体を塗り替える（矩形を指定しない）。<br/>
    /// 呼び出し側が渡す <paramref name="surface"/> は「描画面全体」の宣言であり、本実装はそれより広い範囲を消去しうる<br/>
    /// （キャンバスが要素より大きい場合に、要素外の残像も消すため）。
    /// </remarks>
    public void Clear(EsRect surface, DrawingColor color) => _canvas.Clear(color.ToSKColor());

    /// <summary>矩形を塗る。</summary>
    /// <param name="rect">塗る矩形。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public void FillRect(EsRect rect, DrawingColor color) => SkiaFill.Rect(_canvas, rect, color);

    /// <summary>複数の矩形を同じ色で塗る。</summary>
    /// <param name="rects">塗る矩形の一覧。空の場合は何もしない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public void FillRects(IReadOnlyList<EsRect> rects, DrawingColor color) => SkiaFill.Rects(_canvas, rects, color);

    /// <summary>複数の矩形を同じ色で塗る（矩形のリストを確保しない）。</summary>
    /// <typeparam name="TWalker">実装する列挙子自身の型（構造体）。</typeparam>
    /// <param name="rects">塗る矩形を列挙する列挙子。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public void FillRects<TWalker>(TWalker rects, DrawingColor color)
        where TWalker : struct, IEsRectWalker<TWalker>
        => SkiaFill.Rects(_canvas, rects, color);

    /// <summary>角丸矩形を塗る。</summary>
    /// <param name="rect">塗る矩形。</param>
    /// <param name="cornerRadius">角の半径。0 の場合は四角形として塗る。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public void FillRoundRect(EsRect rect, double cornerRadius, DrawingColor color)
        => SkiaFill.RoundRect(_canvas, rect, cornerRadius, color);

    /// <summary>矩形の枠線を描く（線は矩形の辺の上に中心を置く）。</summary>
    /// <param name="rect">枠の矩形。</param>
    /// <param name="color">線の色（不透明度はアルファ）。</param>
    /// <param name="thickness">線の太さ。</param>
    public void StrokeRect(EsRect rect, DrawingColor color, double thickness)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)thickness,
            Color = color.ToSKColor(),
        };

        _canvas.DrawRect(rect.ToSKRect(), paint);
    }

    /// <summary>線分を描く。</summary>
    /// <param name="from">始点。</param>
    /// <param name="to">終点。</param>
    /// <param name="color">線の色（不透明度はアルファ）。</param>
    /// <param name="thickness">線の太さ。</param>
    public void DrawLine(EsPoint from, EsPoint to, DrawingColor color, double thickness)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)thickness,
            Color = color.ToSKColor(),
        };

        _canvas.DrawLine((float)from.X, (float)from.Y, (float)to.X, (float)to.Y, paint);
    }

    /// <summary>ラベル 1 件（背景 + 文字）を描く。</summary>
    /// <param name="background">背景の矩形。</param>
    /// <param name="backgroundColor">背景色（不透明度はアルファ）。</param>
    /// <param name="text">描画する文字列。</param>
    /// <param name="textOrigin">文字の左上原点（背景の内側）。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="cornerRadius">背景の角丸半径。0 の場合は四角形。</param>
    public void DrawLabel(
        EsRect background,
        DrawingColor backgroundColor,
        string text,
        EsPoint textOrigin,
        double fontSize,
        DrawingColor textColor,
        double cornerRadius)
        => SkiaLabel.Draw(_canvas, background, backgroundColor, text, textOrigin, fontSize, textColor, cornerRadius);

    /// <summary>画像の一部を描画先の矩形へ描く（拡大は最近傍＝画素をそのまま拡大する）。</summary>
    /// <param name="image">描く画像。</param>
    /// <param name="source">画像側の切り出し矩形（画像ピクセル座標）。</param>
    /// <param name="destination">描画先の矩形。</param>
    /// <remarks><c>ToSKBitmap()</c> は内部インスタンスの共有参照を返すため破棄しない（所有権は呼び出し側に残る）。</remarks>
    public void DrawImage(EsBitmap image, EsRect source, EsRect destination)
    {
        ArgumentNullException.ThrowIfNull(image);
        _canvas.DrawBitmap(image.ToSKBitmap(), source.ToSKRect(), destination.ToSKRect());
    }

    /// <summary>テキストの実寸を測定する。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>テキストの実寸。</returns>
    public EsSize MeasureText(string text, double fontSize) => SkiaText.Measure(text, fontSize);
}

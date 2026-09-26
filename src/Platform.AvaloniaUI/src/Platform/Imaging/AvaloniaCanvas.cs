using System.Globalization;
using Avalonia;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Platform.AvaloniaUI.Interop;


namespace EsUtil.Platform.AvaloniaUI.Imaging;

/// <summary>Avalonia のベクター描画（<see cref="DrawingContext"/>）を <see cref="IEsCanvas"/> へ適合させるアダプタ。</summary>
/// <remarks>
/// <b>位置付け</b>: 描画の手順（「何をどの順で描くか」）は UI 非依存のレンダラ（<c>MarqueeControl.UIBase</c> /<br/>
/// <c>LoupeControl.UIBase</c>）が単一所有する。本クラスはその手順を Avalonia の描画 API へ写すだけである。<br/>
/// <br/>
/// <b>座標系</b>: 呼び出し側が定める単位をそのまま使う（<see cref="DrawingContext"/> の単位と同じ）。<br/>
/// 高速化のためブラシとペンを容量制限付きで共有する（<see cref="CapacityLimitedCache{TKey, TValue}"/> が容量管理を単一所有する）。<br/>
/// <b>描画面の消去</b>: <see cref="DrawingContext"/> には全面消去の API が無いため、<see cref="Clear"/> は渡された矩形を塗って代用する<br/>
/// （フレームワークが描画面を消去する UI では <see cref="Clear"/> を呼ぶ必要はない）。
/// </remarks>
public sealed class AvaloniaCanvas : IEsCanvas
{
    /// <summary>ブラシのキャッシュ（Avalonia の色 → ブラシ。容量制限付き）。</summary>
    private static readonly CapacityLimitedCache<Color, SolidColorBrush> s_brushes =
        new(static color => new SolidColorBrush(color));

    /// <summary>ペンのキャッシュ（色と太さ → ペン。容量制限付き）。</summary>
    private static readonly CapacityLimitedCache<(Color Color, double Thickness), Pen> s_pens =
        new(static key => new Pen(s_brushes.GetOrCreate(key.Color), key.Thickness));

    /// <summary>テキストの実寸測定に使うブラシ（<see cref="FormattedText"/> の生成に必要。実寸は色に依存しない）。</summary>
    private static readonly SolidColorBrush s_measureBrush = new(Colors.Transparent);

    /// <summary>描画先の描画コンテキスト（本クラスは所有しない）。</summary>
    private readonly DrawingContext _context;

    /// <summary>アダプタを初期化する。</summary>
    /// <param name="context">描画先の描画コンテキスト。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    public AvaloniaCanvas(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>描画面の全体を指定色で置き換える（前フレームの残像を消す）。</summary>
    /// <param name="surface">描画面の矩形（描画単位）。</param>
    /// <param name="color">置き換える色（不透明度はアルファ）。</param>
    /// <remarks><see cref="DrawingContext"/> に全面消去の API が無いため、渡された矩形を塗って代用する。</remarks>
    public void Clear(EsRect surface, DrawingColor color) => FillRect(surface, color);

    /// <summary>矩形を塗る。</summary>
    /// <param name="rect">塗る矩形。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public void FillRect(EsRect rect, DrawingColor color)
        => _context.DrawRectangle(s_brushes.GetOrCreate(color.ToAvaloniaColor()), null, rect.ToAvaloniaRect());

    /// <summary>複数の矩形を同じ色で塗る。</summary>
    /// <param name="rects">塗る矩形の一覧。空の場合は何もしない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public void FillRects(IReadOnlyList<EsRect> rects, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(rects);

        if (rects.Count == 0)
        {
            return;
        }

        var brush = s_brushes.GetOrCreate(color.ToAvaloniaColor());
        foreach (var rect in rects)
        {
            _context.DrawRectangle(brush, null, rect.ToAvaloniaRect());
        }
    }

    /// <summary>複数の矩形を同じ色で塗る（矩形のリストを確保しない）。</summary>
    /// <typeparam name="TWalker">実装する列挙子自身の型（構造体）。</typeparam>
    /// <param name="rects">塗る矩形を列挙する列挙子。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public void FillRects<TWalker>(TWalker rects, DrawingColor color)
        where TWalker : struct, IEsRectWalker<TWalker>
    {
        var brush = s_brushes.GetOrCreate(color.ToAvaloniaColor());

        while (rects.MoveNext())
        {
            _context.DrawRectangle(brush, null, rects.Current.ToAvaloniaRect());
        }
    }

    /// <summary>角丸矩形を塗る。</summary>
    /// <param name="rect">塗る矩形。</param>
    /// <param name="cornerRadius">角の半径。0 の場合は四角形として塗る。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>角丸 0 は <see cref="RoundedRect"/> の半径 0 として四角形になる（呼び出し側の分岐を不要にする）。</remarks>
    public void FillRoundRect(EsRect rect, double cornerRadius, DrawingColor color)
        => _context.DrawRectangle(
            s_brushes.GetOrCreate(color.ToAvaloniaColor()), null, new RoundedRect(rect.ToAvaloniaRect(), cornerRadius));

    /// <summary>矩形の枠線を描く（線は矩形の辺の上に中心を置く）。</summary>
    /// <param name="rect">枠の矩形。</param>
    /// <param name="color">線の色（不透明度はアルファ）。</param>
    /// <param name="thickness">線の太さ。</param>
    public void StrokeRect(EsRect rect, DrawingColor color, double thickness)
        => _context.DrawRectangle(null, GetPen(color, thickness), rect.ToAvaloniaRect());

    /// <summary>線分を描く。</summary>
    /// <param name="from">始点。</param>
    /// <param name="to">終点。</param>
    /// <param name="color">線の色（不透明度はアルファ）。</param>
    /// <param name="thickness">線の太さ。</param>
    public void DrawLine(EsPoint from, EsPoint to, DrawingColor color, double thickness)
        => _context.DrawLine(GetPen(color, thickness), from.ToAvaloniaPoint(), to.ToAvaloniaPoint());

    /// <summary>ラベル 1 件（背景 + 文字）を描く。</summary>
    /// <param name="background">背景の矩形。</param>
    /// <param name="backgroundColor">背景色（不透明度はアルファ）。</param>
    /// <param name="text">描画する文字列。</param>
    /// <param name="textOrigin">文字の左上原点（背景の内側）。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="cornerRadius">背景の角丸半径。0 の場合は四角形。</param>
    /// <remarks>「背景を塗ってから文字を描く」順序は本メソッドが保証する（呼び出し側は 1 回呼ぶだけ）。</remarks>
    public void DrawLabel(
        EsRect background,
        DrawingColor backgroundColor,
        string text,
        EsPoint textOrigin,
        double fontSize,
        DrawingColor textColor,
        double cornerRadius)
    {
        FillRoundRect(background, cornerRadius, backgroundColor);
        _context.DrawText(CreateFormattedText(text, fontSize, textColor), textOrigin.ToAvaloniaPoint());
    }

    /// <summary>画像の一部を描画先の矩形へ描く（補間は Avalonia の既定に従う）。</summary>
    /// <param name="image">描く画像。</param>
    /// <param name="source">画像側の切り出し矩形（画像ピクセル座標）。</param>
    /// <param name="destination">描画先の矩形。</param>
    /// <remarks>
    /// <see cref="DrawingContext.DrawImage(IImage, Rect, Rect)"/> は <c>IImage</c> を要求するため、<br/>
    /// 表示境界で Avalonia の <c>Bitmap</c> へ変換する（標準 <c>Bitmap</c> は <c>Lock</c> が無く高速に描画できる）。<br/>
    /// 変換した複製は本メソッドで破棄する（<see cref="EsBitmap"/> の所有権は呼び出し側に残る）。
    /// </remarks>
    public void DrawImage(EsBitmap image, EsRect source, EsRect destination)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var avalonia = image.ToAvaloniaBitmap();
        _context.DrawImage(avalonia, source.ToAvaloniaRect(), destination.ToAvaloniaRect());
    }

    /// <summary>テキストの実寸を測定する。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>テキストの実寸。</returns>
    public EsSize MeasureText(string text, double fontSize)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, fontSize, s_measureBrush);

        return new EsSize(formatted.Width, formatted.Height);
    }

    /// <summary>テキスト描画用の <see cref="FormattedText"/> を生成する。</summary>
    /// <param name="text">描画する文字列。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <param name="color">文字色。</param>
    /// <returns>整形済みテキスト。</returns>
    private static FormattedText CreateFormattedText(string text, double fontSize, DrawingColor color)
        => new(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, fontSize,
            s_brushes.GetOrCreate(color.ToAvaloniaColor()));

    /// <summary>色と太さに対応するペンを取得する（キャッシュ優先）。</summary>
    /// <param name="color">線の色。</param>
    /// <param name="thickness">線の太さ。</param>
    /// <returns>色と太さに対応するペン。</returns>
    private static Pen GetPen(DrawingColor color, double thickness)
        => s_pens.GetOrCreate((color.ToAvaloniaColor(), thickness));
}

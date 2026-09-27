using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.Feature.Marquee.Presenter;

/// <summary>ホストが描く付加情報ラベル（操作ラベル・情報ラベル）を描画ポートへ写すレンダラ。</summary>
/// <remarks>
/// <b>位置付け</b>: 付加情報ラベルは選択矩形コントロールの概念ではない（アプリ固有の付加情報）ため、<br/>
/// 文言の生成と表示可否の判断はホストが担う。ただし<b>体裁（背景 = 文字の実寸 + 内側余白 × 2）は<br/>
/// サイズラベルと同じ規則</b>であり、体裁の値は描画の設定（<see cref="MarqueeAppearance"/>）が単一所有する。<br/>
/// 両 UI のホストが同じ組み立てを書かないよう、本クラスが体裁の適用を単一所有する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> でピクセル検証できる。
/// </remarks>
public static class MarqueeHostLabelRenderer
{
    /// <summary>付加情報ラベル 1 行（背景 + 文字）を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="anchor">ラベルの基準点（背景の左上に置く座標）。</param>
    /// <param name="text">文言（ホストが生成する）。null 不可。</param>
    /// <param name="fontSize">フォントサイズ（描画単位）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="backgroundColor">背景色（不透明度はアルファ）。</param>
    /// <param name="padding">内側余白（描画単位）。サイズラベルと同じ値（<see cref="MarqueeAppearance.LabelPadding"/>）を渡す。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> または <paramref name="text"/> が null の場合。</exception>
    /// <remarks>
    /// 基準点が背景の左上であり、背景は文字の実寸へ内側余白を四方へ加えた大きさになり、<br/>
    /// 文字は同じ余白だけ内側へ入る。縦積みの行送りは「前の行の高さ + 内側余白 × 2」である。
    /// </remarks>
    public static void Draw(
        IEsCanvas canvas,
        EsPoint anchor,
        string text,
        double fontSize,
        DrawingColor textColor,
        DrawingColor backgroundColor,
        double padding)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(text);

        var textSize = canvas.MeasureText(text, fontSize);
        var background = new EsRect(anchor.X, anchor.Y, textSize.Width + padding * 2, textSize.Height + padding * 2);
        var textOrigin = new EsPoint(anchor.X + padding, anchor.Y + padding);

        canvas.DrawLabel(background, backgroundColor, text, textOrigin, fontSize, textColor, cornerRadius: 0);
    }
}

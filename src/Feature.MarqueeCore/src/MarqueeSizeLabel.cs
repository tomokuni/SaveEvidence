using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee;

/// <summary>サイズラベル（穴あけ矩形の位置と寸法）の描画契約。</summary>
/// <param name="Text">表示する文言（<see cref="Format"/> が生成する。ホストが上書きした場合はその文字列）。</param>
/// <param name="Anchor">ラベルを積む基準点（穴あけ矩形の左上・DIP）。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="TextColor">文字色。</param>
/// <param name="BackgroundColor">背景色（不透明度は色のアルファに統合）。</param>
/// <remarks>
/// <b>文言はコアが単一所有する</b>: 既定は <see cref="Format"/> の書式（<c>(L, T) - (R, B),  (W×H), DisplayScale: (P%)</c>）。<br/>
/// ホスト固有の文言（元画像座標等）が必要な場合は <see cref="Marquee.SizeLabelOverrideProvider"/> で与える。<br/>
/// <br/>
/// <b>文字の描画は UI 層が担う</b>: テキストの実寸は描画 API でしか測れないため、<br/>
/// UI 層が実寸を <see cref="ComputeFrame"/> へ渡し、返された配置へテキスト要素を置く<br/>
/// （要素への写像は <see cref="EsLabelExtensions.ToElement"/> が単一所有する）。<br/>
/// <b>単位は DIP</b>である（描画領域・選択矩形の座標系と同じ）。
/// </remarks>
public readonly record struct MarqueeSizeLabel(
    string Text,
    EsPoint Anchor,
    double FontSize,
    DrawingColor TextColor,
    DrawingColor BackgroundColor) : IEsLabel
{
    /// <summary>テキストの実寸からラベルの配置（背景矩形・文字の描画原点）を求める。</summary>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>ラベルの配置（基準点の左上に置き、内側余白を四方へ空ける）。</returns>
    /// <remarks>
    /// 背景の大きさ・文字の原点は <see cref="EsLabelLayout"/> が単一所有する。<br/>
    /// 内側余白は付加情報ラベルと共通の <see cref="EsLabelLayout.Padding"/> を使う（同じ体裁で縦に積める）。
    /// </remarks>
    public EsLabelFrame ComputeFrame(EsSize textSize)
    {
        var box = EsLabelLayout.ComputeBoxAtAnchor(
            Anchor, textSize, EsLabelLayout.Padding, EsLabelLayout.Padding);
        return new EsLabelFrame(
            box, EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, EsLabelLayout.Padding));
    }

    /// <summary>サイズラベルの文言を生成する。</summary>
    /// <param name="dipRect">対象の矩形（DIP）。</param>
    /// <param name="scaling">座標・サイズを物理ピクセルへ換算する係数（1.0=100%, 1.25=125%）。0 以下は 1.0 として扱う。</param>
    /// <param name="displayScaling">DisplayScale 表示用の係数（画面の実表示スケール）。0 以下は <paramref name="scaling"/> を使用する。</param>
    /// <returns><c>"(L, T) - (R, B),  (W×H), DisplayScale: (P%)"</c> 形式の文言。</returns>
    /// <remarks>
    /// 座標・サイズは物理ピクセル（<c>DIP × scaling</c> の整数切り捨て）で表示する。<br/>
    /// 値がドメイン座標（画像ピクセル）の場合は <paramref name="scaling"/> に 1.0 を渡す。<br/>
    /// このとき <paramref name="displayScaling"/> へ画面の実表示スケールを渡すと、座標は画像ピクセルのまま<br/>
    /// DisplayScale の百分率だけを実表示スケールで表示できる（画像ピクセル = 1:1 の表示でも画面が 125% なら 125% と表示する）。<br/>
    /// <br/>
    /// <b>既定値を持たない</b>: 引数を省略すると 1 つの値が 2 つの役割（座標の換算と表示倍率）を兼ねてしまい、<br/>
    /// 同じ呼び出しが書式によって違う意味になる。呼び出し側はどちらの係数かを明示する。
    /// </remarks>
    public static string Format(EsRect dipRect, double scaling, double displayScaling)
    {
        var s = DpiScaler.NormalizeScaling(scaling);
        var d = ResolveDisplayScaling(displayScaling, s);
        var left = (int)(dipRect.X * s);
        var top = (int)(dipRect.Y * s);
        var right = (int)(dipRect.Right * s);
        var bottom = (int)(dipRect.Bottom * s);
        var width = (int)(dipRect.Width * s);
        var height = (int)(dipRect.Height * s);
        var percent = (int)(d * 100);
        return $"({left}, {top}) - ({right}, {bottom}),  ({width}×{height}), DisplayScale: ({percent}%)";
    }

    /// <summary>表示スケールの係数を解決する（0 以下の場合は座標換算の係数を使う）。</summary>
    /// <param name="displayScaling">指定された表示スケール。0 以下は未指定として扱う。</param>
    /// <param name="scaling">座標換算の係数（1.0 未満は 1.0 として扱う）。</param>
    /// <returns>解決した表示スケール。</returns>
    /// <remarks>
    /// 「0 以下は未指定」という規則を単一所有する（UI 層が同じ三項演算を書くと、同じ設定で表示が食い違う）。
    /// </remarks>
    public static double ResolveDisplayScaling(double displayScaling, double scaling)
        => displayScaling > 0 ? displayScaling : DpiScaler.NormalizeScaling(scaling);
}

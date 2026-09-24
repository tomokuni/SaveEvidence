using EsUtil.Platform.Core.Geometry;
using Estable.Loupe;
using DrawingColor = System.Drawing.Color;

namespace Estable.Loupe.CoreForControl;

/// <summary>ルーペのラベル 1 件を UI 要素（テキスト要素）として配置するための指示。</summary>
/// <param name="Bounds">要素を置く矩形（DIP。ルーペの表示領域の左上を原点とする）。</param>
/// <param name="TextOrigin">文字の描画原点（左上・DIP。要素の内側余白の算出に使う）。</param>
/// <param name="Text">表示する文言。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="TextColor">文字色。</param>
/// <param name="BackgroundColor">背景色（アルファ 0 は透明）。</param>
/// <param name="CornerRadius">背景の角丸半径（DIP）。0 は四角形。</param>
/// <remarks>
/// <b>位置付け</b>: コントロール化のための配置指示である。コアが決めた文言・外観・レイアウトを、<br/>
/// UI 層が「テキスト要素を 1 つ置く」だけで反映できる形へ写した値である。<br/>
/// 要素の内側余白は <see cref="PaddingX"/> / <see cref="PaddingY"/> が <see cref="TextOrigin"/> から求める。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> であり、UI 境界で自 UI の色型へ変換する。
/// </remarks>
public readonly record struct LoupeLabelElement(
    EsRect Bounds,
    EsPoint TextOrigin,
    string Text,
    double FontSize,
    DrawingColor TextColor,
    DrawingColor BackgroundColor,
    double CornerRadius)
{
    /// <summary>要素の左内側余白（DIP）を取得する。</summary>
    /// <value>背景矩形の左端から文字の描画原点までの距離。</value>
    public double PaddingX => TextOrigin.X - Bounds.X;

    /// <summary>要素の上内側余白（DIP）を取得する。</summary>
    /// <value>背景矩形の上端から文字の描画原点までの距離。</value>
    public double PaddingY => TextOrigin.Y - Bounds.Y;
}

/// <summary>ルーペの中心座標ラベルの配置指示を解決する（描画順の解釈の単一所有）。</summary>
/// <remarks>
/// <b>位置付け</b>: コントロール化のための変換である。コア（<see cref="Loupe.ResolveNotification"/>）は<br/>
/// 「どのラベルを・どの文言で・どの順に描くか」を決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI 層から受け取って要素の矩形へ変換する。<br/>
/// <br/>
/// <b>なぜ CoreForControl 層にあるか</b>: 「実寸から要素の矩形を求める」ことは、文字を UI 要素として<br/>
/// 配置するコントロールでだけ必要になる手順である。<br/>
/// UI フレームワークには依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。<br/>
/// <br/>
/// <b>順序はコアが所有する</b>: 本クラスは <see cref="LoupeRenderNotification.Layers"/> の最後に来る<br/>
/// ラベル層に対応する要素を返すだけであり、順序を判断しない（ラベルは常に最上位に置かれる）。
/// </remarks>
public static class LoupeLabelLayoutPlan
{
    /// <summary>中心座標ラベルの配置指示を解決する。</summary>
    /// <param name="notification">コアが解決した通知（文言・外観・層の順序を持つ）。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素（1 要素）。非表示の設定または中心未設定の場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 外観（色・フォントサイズ・角丸・余白）と文言はコアが単一所有する（本クラスは実寸の反映のみ）。
    /// </remarks>
    public static IReadOnlyList<LoupeLabelElement> Resolve(
        in LoupeRenderNotification notification, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        if (notification.CoordLabel is not { } label)
        {
            return [];
        }

        var frame = label.ComputeFrame(measureText(label.Text, label.FontSize));

        return
        [
            new LoupeLabelElement(
                frame.Background, frame.TextOrigin, label.Text, label.FontSize,
                label.TextColor, label.BackgroundColor, label.CornerRadius),
        ];
    }
}

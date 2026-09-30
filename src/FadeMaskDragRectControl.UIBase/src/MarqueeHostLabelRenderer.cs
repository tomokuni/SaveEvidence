using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.Presenter.Setting;
using EsUtil.Feature.Marquee.Presenter.Label;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.UIBase;

/// <summary>複合コントロールが描く付加情報ラベル（操作ラベル・情報ラベル）の配置指示を解決する。</summary>
/// <remarks>
/// <b>位置付け</b>: 付加情報ラベルは選択矩形コントロール（<c>MarqueeControl</c>）の概念ではない<br/>
/// （本コントロール固有の付加情報）ため、文言の生成と表示可否の判断はホスト（本層を利用する複合コントロール）が担う。<br/>
/// ただし<b>体裁（背景 = 文字の実寸 + 内側余白 × 2）はサイズラベルと同じ規則</b>であり、<br/>
/// 体裁の値は提示層の外観（<see cref="MarqueeAppearance"/>）が単一所有する。<br/>
/// 両 UI のホストが同じ組み立てを書かないよう、本クラスが体裁の適用を単一所有する。<br/>
/// <br/>
/// <b>本クラスは描画しません</b>: UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれないため、<br/>
/// 描画は各 UI が自 UI の API で行います（本クラスは配置指示までを返す）。<br/>
/// UI フレームワークに依存しない（実寸測定をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できます。
/// </remarks>
public static class MarqueeHostLabelRenderer
{
    /// <summary>付加情報ラベル 1 行（背景 + 文字）の配置指示を解決する。</summary>
    /// <param name="anchor">ラベルの基準点（背景の左上に置く座標）。</param>
    /// <param name="text">文言（ホストが生成する）。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="backgroundColor">背景色（不透明度はアルファ）。</param>
    /// <param name="padding">内側余白（DIP）。 サイズラベルと同じ値（<see cref="MarqueeAppearance.LabelPadding"/>）を渡す。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。 null 不可。</param>
    /// <returns>ラベル要素の配置指示（要素へ写すか canvas へ描く）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 基準点が背景の左上であり、背景は文字の実寸へ内側余白を四方へ加えた大きさになり、<br/>
    /// 文字は同じ余白だけ内側へ入る。 縦積みの行送りは「前の行の高さ + 内側余白 × 2」である。<br/>
    /// 角丸は持たない（選択矩形のラベルは矩形である。 要素へ写す UI は <c>CornerRadius = 0</c> にする）。
    /// </remarks>
    public static MarqueeLabelPlacement Resolve(
        EsPoint anchor,
        string text,
        double fontSize,
        DrawingColor textColor,
        DrawingColor backgroundColor,
        double padding,
        Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measureText);

        var textSize = measureText(text, fontSize);
        var dipRect = new EsRect(anchor.X, anchor.Y, textSize.Width + padding * 2, textSize.Height + padding * 2);

        return new MarqueeLabelPlacement(dipRect, padding, padding, text, fontSize, textColor, backgroundColor);
    }
}

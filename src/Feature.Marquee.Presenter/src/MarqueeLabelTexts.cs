using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Marquee.Core.Drawing;
using static EsUtil.Feature.Marquee.Core.Marquee.Limits;

namespace EsUtil.Feature.Marquee.Presenter;

/// <summary>選択矩形コントロールのラベルの文言と基準点を生成する（描画層が単一所有する）。</summary>
/// <remarks>
/// <b>文言の書式は描画層が単一所有する</b>: コアは幾何（穴あけ矩形・カーソル位置）だけを返し、文言と基準点は描画の都合として本クラスが作る。<br/>
/// サイズラベルは「ホストのプロバイダ優先 → 未指定・空文字なら自動生成」であり、表示スケールは矩形ごとに解決できる<br/>
/// （マルチモニタでモニタごとに表示スケールが異なる場合、ホストが矩形からモニタのスケールを解決する）。<br/>
/// フォントサイズ・色は <see cref="MarqueeAppearance"/> が単一所有する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class MarqueeLabelTexts
{
    /// <summary>カーソル座標ラベルをカーソルから離すオフセット（DIP）。</summary>
    /// <remarks>ラベルがカーソルに重ならないための余白である。</remarks>
    private const double CoordLabelOffset = 8.0;

    /// <summary>ラベルを矩形の枠線から離す余白（DIP）。</summary>
    /// <remarks>
    /// 基準点は穴あけ矩形の左上であり、そのまま置くとラベルの背景が矩形の枠線に重なって文字が読みにくくなる。<br/>
    /// 枠線は矩形の内側へ <see cref="MarqueeAppearance.OuterBorderThickness"/> の幅で描かれるため、その幅に本値を足した分だけ内側へ寄せる<br/>
    /// （線幅ちょうどでは背景が線に接する。本値は背景と線の間に残す見た目の余白である）。
    /// </remarks>
    private const double LabelInsetMargin = 1.0;

    /// <summary>穴あけ矩形からラベルの基準点（矩形の枠線を避けた左上）を求める。</summary>
    /// <param name="holeRect">穴あけ矩形（選択またはハイライトの領域）。</param>
    /// <param name="appearance">外観（枠線の太さと表示可否を持つ）。null 不可。</param>
    /// <returns>ラベルの基準点（DIP）。</returns>
    /// <remarks>
    /// 内側への寄せ幅は「矩形の線幅 + <see cref="LabelInsetMargin"/>」であり、本メソッドが単一所有する<br/>
    /// （サイズラベルとホストの付加情報ラベルで寄せ幅が異なると縦積みがずれる）。<br/>
    /// 付加情報ラベルは描画層を経由しないため、基準点は <see cref="MarqueeDrawPlan.LabelAnchor"/> としても公開する。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> が null の場合。</exception>
    internal static EsPoint ResolveLabelAnchor(EsRect holeRect, MarqueeAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        var inset = ResolveLabelInset(appearance);
        return new EsPoint(holeRect.X + inset, holeRect.Y + inset);
    }

    /// <summary>ラベルを矩形の線から離す距離（DIP）を求める。</summary>
    /// <param name="appearance">外観（枠線の太さと表示可否を持つ）。</param>
    /// <returns>矩形の左上からラベルまでの距離（DIP）。</returns>
    /// <remarks>
    /// 線を描く設定では「線幅 + <see cref="LabelInsetMargin"/>」、描かない設定では <see cref="LabelInsetMargin"/> だけである<br/>
    /// （線が無いのに線幅ぶんを足すと、ラベルが理由なく矩形の内側へずれる）。
    /// </remarks>
    private static double ResolveLabelInset(MarqueeAppearance appearance)
        => (appearance.ShowOuterBorder ? appearance.OuterBorderThickness : 0.0) + LabelInsetMargin;

    /// <summary>サイズラベルの文言と基準点を解決する。</summary>
    /// <param name="geometry">描画の幾何（穴あけ矩形を持つ）。</param>
    /// <param name="appearance">外観（表示可否と枠線の太さを持つ）。</param>
    /// <param name="displayScaleScaling">座標・サイズを物理ピクセルへ換算する係数（0 以下は倍率として扱う）。</param>
    /// <param name="overrideProvider">文言プロバイダ。null または空文字を返す場合は自動生成する。</param>
    /// <param name="displayScalingProvider">表示スケールを矩形ごとに解決するプロバイダ。null の場合は <paramref name="displayScaleScaling"/> を使う。</param>
    /// <returns>サイズラベルのテキスト配置。表示しない場合は null。</returns>
    /// <remarks>
    /// 基準点は穴あけ矩形の左上から枠線の幅（+ 余白）だけ内側へ寄せた点であり、アラインは左端・上端である。<br/>
    /// 座標の換算は <paramref name="displayScaleScaling"/> を使い、DisplayScale の百分率だけを<br/>
    /// <paramref name="displayScalingProvider"/> で矩形ごとに差し替えられる。
    /// </remarks>
    internal static MarqueeTextPlacement? ResolveSizeLabelText(
        in MarqueeGeometry geometry,
        MarqueeAppearance appearance,
        double displayScaleScaling,
        Func<string?>? overrideProvider,
        Func<EsRect, double>? displayScalingProvider)
    {
        if (!appearance.ShouldShowSizeLabel(geometry.HoleRect is not null) || geometry.HoleRect is not { } holeRect)
        {
            return null;
        }

        var rectDisplayScale = displayScalingProvider is { } scalingProvider && geometry.HoleDomainRect is { } domainRect
            ? scalingProvider(domainRect)
            : displayScaleScaling;

        var overrideText = overrideProvider?.Invoke();
        var text = string.IsNullOrEmpty(overrideText)
            ? FormatSizeLabel(holeRect, displayScaleScaling, rectDisplayScale)
            : overrideText;

        return new MarqueeTextPlacement(
            text, ResolveLabelAnchor(holeRect, appearance), EsHorizontalAlign.Left, EsVerticalAlign.Top, default);
    }

    /// <summary>カーソル座標ラベルの文言と基準点を解決する。</summary>
    /// <param name="geometry">描画の幾何（カーソル位置とクライアントサイズを持つ）。</param>
    /// <param name="appearance">外観（表示可否を持つ）。</param>
    /// <param name="port">解決済みの座標変換ポート（ドメイン座標 → DIP）。</param>
    /// <returns>座標ラベルのテキスト配置。表示しない場合は null。</returns>
    /// <remarks>
    /// 文言は <see cref="EsPoint.Format(int)"/> が文字列化する。<br/>
    /// 基準点は「カーソル + <see cref="CoordLabelOffset"/>」であり、アラインは左端・上端である。
    /// </remarks>
    internal static MarqueeTextPlacement? ResolveCoordLabelText(
        in MarqueeGeometry geometry, MarqueeAppearance appearance, ICoordTransform port)
    {
        if (!appearance.ShouldShowCoordLabel())
        {
            return null;
        }

        var cursor = port.Inverse(geometry.MousePosition);

        return new MarqueeTextPlacement(
            geometry.MousePosition.Format(),
            new EsPoint(cursor.X + CoordLabelOffset, cursor.Y + CoordLabelOffset),
            EsHorizontalAlign.Left,
            EsVerticalAlign.Top,
            geometry.ClientSize);
    }

    /// <summary>サイズラベルの書式（文言）を生成する。</summary>
    /// <param name="dipRect">対象の矩形（DIP）。</param>
    /// <param name="displayScale">座標・サイズを物理ピクセルへ換算する係数（0 以下は 100% として扱う）。</param>
    /// <param name="rectDisplayScale">DisplayScale の百分率に使う係数（0 以下は <paramref name="displayScale"/> を使う）。</param>
    /// <returns><c>"(L, T) - (R, B),  (W×H), DisplayScale: (P%)"</c> 形式の文言。</returns>
    /// <remarks>
    /// 書式は描画層の内部に閉じる（ホストは文言を作らない）。<br/>
    /// 座標・サイズは物理ピクセル（<c>DIP × displayScale</c> の整数切り捨て）で表示する。
    /// </remarks>
    private static string FormatSizeLabel(EsRect dipRect, double displayScale, double rectDisplayScale)
    {
        var s = DpiScaler.NormalizeScaling(displayScale);
        var d = ResolveDisplayScale(rectDisplayScale, s);
        var left = (int)(dipRect.X * s);
        var top = (int)(dipRect.Y * s);
        var right = (int)(dipRect.Right * s);
        var bottom = (int)(dipRect.Bottom * s);
        var width = (int)(dipRect.Width * s);
        var height = (int)(dipRect.Height * s);
        var percent = (int)(d * 100);

        return $"({left}, {top}) - ({right}, {bottom}),  ({width}×{height}), DisplayScale: ({percent}%)";
    }
}

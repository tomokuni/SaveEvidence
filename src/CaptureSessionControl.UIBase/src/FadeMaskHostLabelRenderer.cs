using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Marquee.Presenter.Label;
using EsUtil.Feature.Marquee.Presenter.Setting;
using EsUtil.Feature.Marquee.Presenter.Drawing;

namespace EsUtil.SaveEvidence.CaptureSession.UIBase;

/// <summary>ホストが描く付加情報ラベル（操作ラベル・情報ラベル）の配置指示を解決する。</summary>
/// <remarks>
/// <b>位置付け</b>: 付加情報ラベルは選択矩形コントロールの概念ではない（アプリ固有の付加情報）ため、<br/>
/// <b>文言の生成はホストが担う</b>。 ただし<b>描画位置（注目矩形の左上から縦に積む）と表示可否<br/>
/// （設定スイッチと文言の有無の両方が成立したときだけ描く）は両 UI で同じ</b>であるため、本クラスが単一所有する<br/>
/// （片方の UI だけ積み方が変わる余地をなくす）。<br/>
/// <br/>
/// 体裁（背景 = 文字の実寸 + 内側余白）は <see cref="MarqueeHostLabelRenderer"/> が、体裁の値（内側余白）は<br/>
/// <see cref="MarqueeAppearance"/>（提示層が単一所有する外観）が持つ。 本クラスは「どの文言をどの位置へ積むか」だけを決める。<br/>
/// <br/>
/// <b>本クラスは描画しません</b>: 描画は各 UI が自 UI の API で行います（本クラスは配置指示までを返す）。<br/>
/// UI フレームワークに依存しない（実寸測定をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できます。
/// </remarks>
public static class FadeMaskHostLabelRenderer
{
    /// <summary>付加情報ラベル（操作ラベル・情報ラベル）の配置指示を解決する（描画順に並ぶ）。</summary>
    /// <param name="plan">選択矩形コントロールが解決した描画計画（注目矩形とサイズラベルの表示可否を参照する）。</param>
    /// <param name="labels">付加情報ラベルの外観（表示可否・寸法・色）。</param>
    /// <param name="appearance">選択矩形コントロールの外観（サイズラベルのフォントサイズを行送りに使う）。 null 不可。</param>
    /// <param name="operationText">操作ラベルの文言（ホストが生成する。 null または空文字の場合は描かない）。</param>
    /// <param name="infoText">情報ラベルの文言（ホストが生成する。 null または空文字の場合は描かない）。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。 null 不可。</param>
    /// <returns>配置するラベル要素の一覧（操作ラベル → 情報ラベルの順）。 どちらも無い場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 1. 注目矩形（選択またはハイライトの領域）が無い場合は空を返す（積み上げる基準が無い）。<br/>
    /// 2. 積み上げの開始位置は注目矩形の内側へ寄せた左上（<see cref="MarqueeDrawPlan.LabelAnchor"/>）であり、<br/>
    ///    サイズラベルが描かれている場合はその下から始める（重なりを避ける。 行送りの行の高さは外観のフォントサイズを使う）。<br/>
    /// 3. 操作ラベル → 情報ラベルの順に、表示可否を満たす行だけを返す（左端は基準点の左端に揃える）。<br/>
    /// 4. 描画のたびに評価される（ホストの文言プロバイダは最新の状態から文言を生成できる）。
    /// </remarks>
    public static IReadOnlyList<MarqueeLabelPlacement> Resolve(
        in MarqueeDrawPlan plan,
        FadeMaskDragRectLabelOptions labels,
        MarqueeAppearance appearance,
        string? operationText,
        string? infoText,
        Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(measureText);

        // 付加情報を重ねる基準は注目矩形の内側へ寄せた左上である（背景が枠線に重ならない位置）
        if (plan.LabelAnchor is not { } anchor)
        {
            return [];
        }

        var y = anchor.Y;
        var padding = appearance.LabelPadding;

        // サイズラベルが描かれている場合はその下へ積む（重なりを避ける）
        if (appearance.ShouldShowSizeLabel(hasFocusRect: true))
        {
            y = NextTop(y, appearance.SizeLabelFontSize, padding);
        }

        var placements = new List<MarqueeLabelPlacement>(2);

        // 表示可否はホストが判定する（設定スイッチと文言の有無の両方が成立したときだけ描く）
        if (labels.ShowOperationLabel && operationText is { Length: > 0 })
        {
            placements.Add(MarqueeHostLabelRenderer.Resolve(
                new EsPoint(anchor.X, y),
                operationText,
                labels.OperationFontSize,
                labels.OperationColor,
                labels.OperationBackgroundColor,
                padding,
                measureText));
            y = NextTop(y, labels.OperationFontSize, padding);
        }

        if (labels.ShowInfoLabel && infoText is { Length: > 0 })
        {
            placements.Add(MarqueeHostLabelRenderer.Resolve(
                new EsPoint(anchor.X, y),
                infoText,
                labels.InfoFontSize,
                labels.InfoColor,
                labels.InfoBackgroundColor,
                padding,
                measureText));
        }

        return placements;
    }

    /// <summary>縦積みしたラベルの次の行の上端を求める。</summary>
    /// <param name="top">現在の行の上端（DIP）。</param>
    /// <param name="lineHeight">現在の行の高さ（DIP。 フォントサイズ等）。</param>
    /// <param name="padding">上下の内側余白（DIP）。 0 以上。</param>
    /// <returns>次の行の上端（DIP。 現在の行の高さ + 内側余白 × 2 だけ下）。</returns>
    /// <remarks>
    /// ラベルを同じ間隔で縦に積む規則を本クラスが単一所有する（行送りを各 UI へ書かせない）。<br/>
    /// <b>行送り ≠ 背景の下端</b>: 背景は基準点（アンカー）から求め、行送りは基準点の間隔である。
    /// </remarks>
    private static double NextTop(double top, double lineHeight, double padding)
        => top + lineHeight + padding * 2;
}


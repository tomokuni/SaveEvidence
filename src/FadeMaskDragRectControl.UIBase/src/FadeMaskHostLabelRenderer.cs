using EsUtil.Feature.MarqueeCore;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Marquee.UIBase;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.FadeMaskDragRect.UIBase;

/// <summary>ホストが描く付加情報ラベル（操作ラベル・情報ラベル）を描画ポートへ写すレンダラ。</summary>
/// <remarks>
/// <b>位置付け</b>: 付加情報ラベルは選択矩形コントロールの概念ではない（アプリ固有の付加情報）ため、<br/>
/// <b>文言の生成はホストが担う</b>。ただし<b>描画位置（穴あけ矩形の左上から縦に積む）と表示可否<br/>
/// （設定スイッチと文言の有無の両方が成立したときだけ描く）は両 UI で同じ</b>であるため、本クラスが単一所有する<br/>
/// （片方の UI だけ積み方が変わる余地をなくす）。<br/>
/// <br/>
/// 体裁（背景 = 文字の実寸 + 内側余白）は <see cref="MarqueeHostLabelRenderer"/> が、行送りは<br/>
/// <see cref="EsLabelLayout"/> が単一所有する。本クラスは「どの文言をどの位置へ積むか」だけを決める。<br/>
/// <br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> でピクセル検証できる。
/// </remarks>
public static class FadeMaskHostLabelRenderer
{
    /// <summary>付加情報ラベル（操作ラベル・情報ラベル）を描画ポートへ描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="content">選択矩形コントロールが解決した描画内容（穴あけ矩形とサイズラベルの有無を参照する）。</param>
    /// <param name="labels">付加情報ラベルの外観（表示可否・寸法・色）。</param>
    /// <param name="operationText">操作ラベルの文言（ホストが生成する。null または空文字の場合は描かない）。</param>
    /// <param name="infoText">情報ラベルの文言（ホストが生成する。null または空文字の場合は描かない）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// 1. 穴あけ矩形（選択またはハイライトの領域）が無い場合は何も描かない（積み上げる基準が無い）。<br/>
    /// 2. 積み上げの開始位置は穴あけ矩形の左上であり、サイズラベルが描かれている場合はその下から始める<br/>
    ///    （重なりを避ける。行送りはコアが単一所有する）。<br/>
    /// 3. 操作ラベル → 情報ラベルの順に、表示可否を満たす行だけを描く（左端は穴あけ矩形の左端に揃える）。<br/>
    /// 4. 描画のたびに評価される（ホストの文言プロバイダは最新の状態から文言を生成できる）。
    /// </remarks>
    public static void Draw(
        IEsCanvas canvas,
        in MarqueeDrawContent content,
        FadeMaskDragRectLabelOptions labels,
        string? operationText,
        string? infoText)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // 付加情報を重ねる基準は穴あけ矩形（選択またはハイライトの領域）の左上である
        if (content.HoleRect is not { } hole)
        {
            return;
        }

        var y = hole.Y;

        // サイズラベルが描かれている場合はその下へ積む（重なりを避ける）
        if (content.SizeLabel is { } sizeLabel)
        {
            y = EsLabelLayout.NextTop(y, sizeLabel.FontSize, EsLabelLayout.Padding);
        }

        // 表示可否はホストが判定する（設定スイッチと文言の有無の両方が成立したときだけ描く）
        if (labels.ShowOperationLabel && operationText is { Length: > 0 })
        {
            MarqueeHostLabelRenderer.Draw(
                canvas,
                new EsPoint(hole.X, y),
                operationText,
                labels.OperationFontSize,
                labels.OperationColor,
                labels.OperationBackgroundColor,
                EsLabelLayout.Padding);
            y = EsLabelLayout.NextTop(y, labels.OperationFontSize, EsLabelLayout.Padding);
        }

        if (labels.ShowInfoLabel && infoText is { Length: > 0 })
        {
            MarqueeHostLabelRenderer.Draw(
                canvas,
                new EsPoint(hole.X, y),
                infoText,
                labels.InfoFontSize,
                labels.InfoColor,
                labels.InfoBackgroundColor,
                EsLabelLayout.Padding);
        }
    }
}

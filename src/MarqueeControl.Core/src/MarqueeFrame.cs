using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Estable.Marquee.Decoration;

namespace Estable.Marquee;

/// <summary>選択矩形の 1 フレーム分の描画内容を解決する静的クラス。</summary>
/// <remarks>
/// 本コントロールは「選択矩形そのもの（枠線・ハンドル・マーカー・座標ラベル）」と、<br/>
/// 「選択矩形の外側に描く装飾（暗転マスク・穴あけ枠・ラベル）」の 2 つを描く。後者の描画内容は<br/>
/// 装飾コア（<see cref="OuterDecoFrame"/>）が単一所有するため、本クラスは両者を橋渡しする。<br/>
/// <br/>
/// <b>選択矩形はセッションが単一所有する</b>: 装飾の穴あけ矩形と選択の有無は、設定値ではなくセッションの状態から導出する。<br/>
/// このため装飾コアの入力値（<see cref="OuterDecoOptions"/>）を本クラスで組み立て、<br/>
/// 選択に由来する項目（<c>LogicalSelectionRect</c> / <c>HasSelection</c>）だけをセッションの状態で埋める<br/>
/// （外側の装飾の設定値には選択の状態を持たせない）。<br/>
/// <br/>
/// <b>描画内容の判断は装飾コアが持つ</b>: どの矩形を暗転するか・枠線を描くか・ラベルをどう組むかは<br/>
/// <see cref="OuterDecoFrame.Resolve(in OuterDecoRequest)"/> が判断する（本クラスは判断を持たない）。<br/>
/// UI 実装は本メソッドが返すフレームを自 UI の描画 API へ写すだけでよい。
/// </remarks>
public static class MarqueeFrame
{
    /// <summary>選択矩形の 1 フレーム分の描画内容（外側の装飾）を解決する。</summary>
    /// <param name="session">選択の状態を単一所有するセッション（選択矩形・選択の有無・設定を参照する）。</param>
    /// <param name="client">クライアント矩形（DIP。UI 実装の描画領域）。</param>
    /// <param name="port">論理座標 → DIP の座標変換ポート（null 可。null の場合は <paramref name="scaling"/> を等倍スケールとして適用する）。</param>
    /// <param name="scaling">ポート未指定の場合に適用する倍率（0 以下・等倍は等価変換）。</param>
    /// <param name="sizeLabelOverrideProvider">サイズラベルの文言プロバイダ（null 可。未指定・空文字の場合は自動生成する）。</param>
    /// <param name="displayScaleScaling">サイズラベルの自動生成に使うスケーリング（0 以下は等倍）。</param>
    /// <param name="operationTextProvider">操作ラベル（2 行目）の文言プロバイダ（null 可。未指定・空文字の場合は描かない）。</param>
    /// <param name="infoTextProvider">情報ラベル（3 行目）の文言プロバイダ（null 可。未指定・空文字の場合は描かない）。</param>
    /// <returns>外側の装飾の描画内容（描くものが無い場合は空のフレーム）。</returns>
    /// <remarks>
    /// 1. セッションの設定（外側の装飾の外観・ハイライト）を取り出す。<br/>
    /// 2. 選択に由来する項目（論理選択矩形・選択の有無）をセッションの状態から導出する。<br/>
    /// 3. 装飾コアの入力値を組み立て、フレーム解決（<see cref="OuterDecoFrame.Resolve(in OuterDecoRequest)"/>）へ委譲する。<br/>
    /// <br/>
    /// 文言（操作ラベル・情報ラベル）はホストがプロバイダで与える（本コアは特定の操作文言を持たない）。<br/>
    /// 本メソッドは引数を検証しない（<paramref name="client"/> の寸法が 0 の場合は空のフレームが返る）。
    /// </remarks>
    public static OuterDecoFrame Resolve(
        MarqueeSession session,
        EsRect client,
        ICoordTransform? port,
        double scaling,
        Func<string?>? sizeLabelOverrideProvider,
        double displayScaleScaling,
        Func<string?>? operationTextProvider = null,
        Func<string?>? infoTextProvider = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var options = session.Options;
        var hasSelection = session.HasSelection;

        var request = new OuterDecoRequest(
            new OuterDecoOptions
            {
                Appearance = options.OuterAppearance,
                ShowFullOverlay = options.OuterShowFullOverlay,
                LogicalSelectionRect = hasSelection ? session.SelectionRect : null,
                HasSelection = hasSelection,
                LogicalHighlightRect = options.OuterHighlightRect,
                HasHighlight = options.OuterHasHighlight,
            },
            client,
            port,
            scaling,
            sizeLabelOverrideProvider,
            operationTextProvider,
            infoTextProvider,
            displayScaleScaling);

        return OuterDecoFrame.Resolve(request);
    }
}

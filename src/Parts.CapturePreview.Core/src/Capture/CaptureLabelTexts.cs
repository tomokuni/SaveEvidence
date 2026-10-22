using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

/// <summary>キャプチャの選択 UI が表示するラベル文言（操作ラベル・情報ラベル）と言葉の選定を単一所有する。</summary>
/// <remarks>
/// 選択矩形の複合コントロール（<c>SelectionOverlayControl</c>）は汎用化により固有の文言を持たないため、<br/>
/// キャプチャ固有の文言（確定・キャンセル方法、ウィンドウタイトル）は本クラスが与える。<br/>
/// 両 UI のホストが同じ文言・同じ表示可否を使う（UI ごとに文言がずれる余地がない）。<br/>
/// UI 層は文言プロバイダ（<c>OperationTextProvider</c> / <c>InfoTextProvider</c>）から本クラスを呼ぶだけにする。
/// </remarks>
public static class CaptureLabelTexts
{
    /// <summary>確定（Enter 系）の操作ラベル。</summary>
    private const string EnterOperationText = "Enter / クリック: 確定  Esc: キャンセル";

    /// <summary>確定（ダブルクリック系）の操作ラベル。</summary>
    private const string DoubleClickOperationText = "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル";

    /// <summary>キャプチャ種別に対応する複合コントロールの動作モードを取得する。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <returns>動作モード。</returns>
    /// <remarks>
    /// 切出し（<see cref="SelectionOverlayMode.Crop"/>）は本写像の対象外である（プレビューの切り出しが明示的に指定する）。<br/>
    /// 未定義の種別は範囲選択として扱う。
    /// </remarks>
    public static SelectionOverlayMode ToSelectionMode(CaptureType captureType) => captureType switch
    {
        CaptureType.ScreenSelect => SelectionOverlayMode.ScreenSelect,
        CaptureType.WindowSelect => SelectionOverlayMode.WindowSelect,
        _ => SelectionOverlayMode.AreaSelect,
    };

    /// <summary>操作ラベル（確定・キャンセル方法）の文言を取得する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>モードに応じた操作ラベルの文言（常に非空）。</returns>
    /// <remarks>
    /// 確定操作の文言は 2 系統（Enter 系 / ダブルクリック系）に集約する。<br/>
    /// スクリーン選択・ウィンドウ選択は Enter 系、範囲選択・切出しはダブルクリック系とする。<br/>
    /// 表示の可否（<see cref="SelectionOverlayLabelOptions.ShowOperationLabel"/>）は描画側（ホスト）が判断する。
    /// </remarks>
    public static string GetOperationText(SelectionOverlayMode mode)
        => mode is SelectionOverlayMode.ScreenSelect or SelectionOverlayMode.WindowSelect
            ? EnterOperationText
            : DoubleClickOperationText;

    /// <summary>情報ラベル（ウィンドウタイトル）に表示する文言を取得する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="windowTitle">検出したウィンドウタイトル。 null 可。</param>
    /// <returns>ウィンドウ選択の場合はウィンドウタイトル、それ以外は null（情報ラベルを描かない）。</returns>
    /// <remarks>
    /// 情報ラベルはウィンドウ選択でのみ意味を持つため、他モードでは null を返して描画させない。<br/>
    /// 表示の可否（<see cref="SelectionOverlayLabelOptions.ShowInfoLabel"/>）と空文字の扱いは描画側（ホスト）が判断する。
    /// </remarks>
    public static string? GetInfoText(SelectionOverlayMode mode, string? windowTitle)
        => mode == SelectionOverlayMode.WindowSelect ? windowTitle : null;
}

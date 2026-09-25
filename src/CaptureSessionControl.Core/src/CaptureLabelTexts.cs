using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.FadeMaskDragRect;
using EsUtil.Feature.MarqueeCore;

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>キャプチャの選択 UI が表示するラベル文言（操作ラベル・情報ラベル）と言葉の選定を単一所有する。</summary>
/// <remarks>
/// 選択矩形の複合コントロール（<c>FadeMaskDragRectControl</c>）は汎用化により固有の文言を持たないため、<br/>
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
    /// 切出し（<see cref="FadeMaskDragRectMode.Crop"/>）は本写像の対象外である（プレビューの切り出しが明示的に指定する）。<br/>
    /// 未定義の種別は範囲選択として扱う。
    /// </remarks>
    public static FadeMaskDragRectMode ToSelectionMode(CaptureType captureType) => captureType switch
    {
        CaptureType.ScreenSelect => FadeMaskDragRectMode.ScreenSelect,
        CaptureType.WindowSelect => FadeMaskDragRectMode.WindowSelect,
        _ => FadeMaskDragRectMode.AreaSelect,
    };

    /// <summary>操作ラベル（確定・キャンセル方法）の文言を取得する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <returns>モードに応じた操作ラベルの文言（常に非空）。</returns>
    /// <remarks>
    /// 確定操作の文言は 2 系統（Enter 系 / ダブルクリック系）に集約する。<br/>
    /// スクリーン選択・ウィンドウ選択は Enter 系、範囲選択・切出しはダブルクリック系とする。<br/>
    /// 表示の可否（<see cref="FadeMaskDragRectLabelOptions.ShowOperationLabel"/>）は描画側（ホスト）が判断する。
    /// </remarks>
    public static string GetOperationText(FadeMaskDragRectMode mode)
        => mode is FadeMaskDragRectMode.ScreenSelect or FadeMaskDragRectMode.WindowSelect
            ? EnterOperationText
            : DoubleClickOperationText;

    /// <summary>情報ラベル（ウィンドウタイトル）に表示する文言を取得する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="windowTitle">検出したウィンドウタイトル。null 可。</param>
    /// <returns>ウィンドウ選択の場合はウィンドウタイトル、それ以外は null（情報ラベルを描かない）。</returns>
    /// <remarks>
    /// 情報ラベルはウィンドウ選択でのみ意味を持つため、他モードでは null を返して描画させない。<br/>
    /// 表示の可否（<see cref="FadeMaskDragRectLabelOptions.ShowInfoLabel"/>）と空文字の扱いは描画側（ホスト）が判断する。
    /// </remarks>
    public static string? GetInfoText(FadeMaskDragRectMode mode, string? windowTitle)
        => mode == FadeMaskDragRectMode.WindowSelect ? windowTitle : null;

    /// <summary>サイズラベルの文言を解決する（確定対象が無い場合はホスト指定の文言を使う）。</summary>
    /// <param name="target">確定対象の矩形（ドメイン座標）。対象が無い場合は null。</param>
    /// <param name="overrideText">確定対象が無い場合に使う文言（設定の明示指定）。null 可（自動生成に委ねる）。</param>
    /// <param name="displayScaling">対象矩形の実表示倍率を解決するデリゲート（描画時のみ評価する）。</param>
    /// <returns>サイズラベルの文言（常に描画側へ渡せる形）。</returns>
    /// <remarks>
    /// 「確定対象があれば矩形から生成し、無ければホスト指定の文言を使う」という規則を単一所有する<br/>
    /// （UI が同じ三項演算を書くと、同じ状態で表示が食い違う）。<br/>
    /// 矩形は既にドメイン座標（画面物理ピクセル）のため、表示倍率は基準値（DIP と 1:1）で整形する。
    /// </remarks>
    public static string? ResolveSizeLabel(EsRect? target, string? overrideText, Func<EsRect, double> displayScaling)
    {
        ArgumentNullException.ThrowIfNull(displayScaling);
        return target is { } rect
            ? MarqueeSizeLabel.Format(rect, DpiScaler.StandardScaling, displayScaling(rect))
            : overrideText;
    }
}
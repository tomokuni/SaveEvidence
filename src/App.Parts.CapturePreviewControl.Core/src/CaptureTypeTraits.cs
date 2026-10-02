using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>キャプチャ種別（<see cref="CaptureType"/>）の性質を単一所有する。</summary>
/// <remarks>
/// 「どの種別がハイライト型（クリック確定）か」「どの種別が閉区間（終点のピクセルを含む）か」「どの検出関数を使うか」は<br/>
/// UI 実装に依存しない規則であるため本クラスへ集約する（両 UI が同じ判定を別表現で持たない）。<br/>
/// 種別は <c>Platform.Core</c> の <see cref="CaptureType"/> が所有し、本クラスはその性質を導出するだけである。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class CaptureTypeTraits
{
    /// <summary>ハイライト（検出矩形の強調）で選択する種別かどうかを判定する。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <returns>スクリーン選択・ウィンドウ選択の場合は true（ドラッグ選択の種別は false）。</returns>
    /// <remarks>
    /// ハイライト型は「矩形を描いて確定する」のではなく「カーソル下の要素を強調してクリックで確定する」操作になる。<br/>
    /// 範囲選択（<see cref="CaptureType.AreaSelect"/>）と切り出しはドラッグで矩形を作る。<br/>
    /// 本メソッドは「選択スタイル（ハイライト型 / ドラッグ型）」の分区を単一所有するため、<br/>
    /// UI 層は十字カーソルの要否・ルーペの利用・確定操作の配線を本メソッドの結果から導く<br/>
    /// （UI ごとに種別を直接比較すると、同じ規則が別表現で散る）。
    /// </remarks>
    public static bool IsHighlighted(CaptureType captureType)
        => captureType is CaptureType.ScreenSelect or CaptureType.WindowSelect;

    /// <summary>切り出し範囲の区間の流儀（終点のピクセルを含むか）を取得する。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <returns>範囲選択の場合は <see cref="EsRectInterval.Closed"/>。 ハイライト型は <see cref="EsRectInterval.HalfOpen"/>。</returns>
    /// <remarks>
    /// ドラッグで作る矩形は「始点から終点まで」を意図するため終点のピクセルを含める（<c>+1</c>）が、<br/>
    /// ハイライト矩形は検出結果が全ピクセルを含むため加算しない（1 ピクセル大きく切り出されるのを防ぐ）。<br/>
    /// 種別は矩形の出自（ドラッグ / 検出）を決めるため、そのまま区間の流儀を決める<br/>
    /// （切り出し側は返された値で矩形を組み、ピクセル数の判断は <see cref="EsRectInterval"/> を読む側へ閉じる）。
    /// </remarks>
    public static EsRectInterval ResolveInterval(CaptureType captureType)
        => captureType == CaptureType.AreaSelect ? EsRectInterval.Closed : EsRectInterval.HalfOpen;

    /// <summary>カーソル下の要素を検出する対象を取得する。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <returns>検出対象（スクリーン / ウィンドウ）。 ハイライト型以外は <see cref="CaptureElementTarget.Window"/> を返す。</returns>
    /// <remarks>
    /// 検出対象の判定を本メソッドへ集約し、UI 層は返された対象に対応する検出関数を呼ぶだけにする<br/>
    /// （「スクリーン選択なら画面検出、それ以外はウィンドウ検出」という規則を各 UI に書かない）。
    /// </remarks>
    public static CaptureElementTarget GetElementTarget(CaptureType captureType)
        => captureType == CaptureType.ScreenSelect ? CaptureElementTarget.Screen : CaptureElementTarget.Window;

    /// <summary>カーソル下の要素（スクリーン / ウィンドウ）を検出する。</summary>
    /// <param name="finder">要素検出サービス。 null の場合は検出しない。</param>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="screenPoint">カーソル位置（画面物理ピクセル）。</param>
    /// <returns>検出結果。<paramref name="finder"/> が null の場合は未検出の結果。</returns>
    /// <remarks>
    /// 「どの種別がどちらの検出関数を使うか」という分岐を単一所有する（UI が <see cref="GetElementTarget"/> の結果で<br/>
    /// 呼び分けると、同じ分岐がホバー・押下などの入力経路ごとに複製される）。
    /// </remarks>
    public static ElementFinderResults FindUnderCursor(
        IElementFinder? finder, CaptureType captureType, EsPointInt screenPoint)
        => finder is not { } target ? default
            : GetElementTarget(captureType) == CaptureElementTarget.Screen
                ? target.FindScreenUnderCursor(screenPoint)
                : target.FindWindowUnderCursor(screenPoint);

    /// <summary>確定操作の対象となる選択矩形を解決する。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="highlightRect">ハイライト矩形（未検出の場合は null）。</param>
    /// <param name="selectionRect">ドラッグ選択の矩形（未選択の場合は null）。</param>
    /// <returns>確定対象の矩形。 対象が無い場合は null。</returns>
    /// <remarks>
    /// ハイライト型（スクリーン選択 / ウィンドウ選択）はハイライト矩形を、それ以外（範囲選択）はドラッグ選択の矩形を使う。<br/>
    /// 「どちらを確定対象にするか」という判断を単一所有する（UI ごとの優先順位の書き方の違いで挙動が食い違うのを防ぐ）。
    /// </remarks>
    public static EsRect? ResolveRect(CaptureType captureType, EsRect? highlightRect, EsRect? selectionRect)
        => IsHighlighted(captureType) ? highlightRect : selectionRect;
}

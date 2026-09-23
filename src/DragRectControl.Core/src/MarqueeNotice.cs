using EsUtil.Platform.Core.Geometry;

namespace Estable.Marquee;

/// <summary>コア（状態層）から UI 層へ伝える通知の種別。</summary>
/// <remarks>
/// UI 層は本種別を自 UI の公開イベントへ転送するだけである（判断はコアが済ませている）。<br/>
/// 種別と公開イベントの対応は各 UI 実装の転送処理が持つ。<br/>
/// </remarks>
internal enum MarqueeNoticeKind
{
    /// <summary>新規選択のドラッグ開始（始点の決定）。</summary>
    SelectionStarted,

    /// <summary>選択矩形の変更。</summary>
    SelectionChanged,

    /// <summary>選択矩形の確定。</summary>
    SelectionCompleted,

    /// <summary>領域のキャンセル（小さすぎる矩形・枠外ダブルクリック等）。</summary>
    SelectionCancelled,

    /// <summary>ポインター押下（左ボタン）。</summary>
    MouseDown,

    /// <summary>ポインター解放（左ボタン）。</summary>
    MouseUp,

    /// <summary>クリック（ドラッグ移動なしの押下→解放）。</summary>
    MouseClicked,

    /// <summary>ポインターのダブルクリック。</summary>
    MouseDoubleClicked,

    /// <summary>ポインターの移動。</summary>
    MouseMoved,

    /// <summary>ルーペが表示する中心座標の変更。</summary>
    LoupePositionMoved,

    /// <summary>ビューポート（ズーム・スクロール）の変化。</summary>
    ViewportChanged,

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）。</summary>
    ConfirmRequested,

    /// <summary>キャンセル要求（Escape・枠外ダブルクリック・API 呼び出し）。</summary>
    CancelRequested,
}

/// <summary>コア（状態層）から UI 層への通知（種別・通知座標・スナップショット）。</summary>
/// <remarks>
/// UI 層は本通知を自 UI の公開イベント引数へ変換して発火するだけである（判断・状態更新はコアが実施済み）。<br/>
/// <see cref="Position"/> は点座標を表す通知（MouseMoved / LoupePositionMoved 等）で使用する。<br/>
/// 矩形・無引数の通知（SelectionChanged / CancelRequested 等）では参照しない（<see cref="Snapshot"/> の選択矩形を使用する）。<br/>
/// </remarks>
/// <param name="Kind">通知の種別。</param>
/// <param name="Position">通知時点の座標（ドメイン座標）。点座標を持たない通知では既定値。</param>
/// <param name="Snapshot">通知時点の状態スナップショット。</param>
internal sealed record MarqueeNotice(MarqueeNoticeKind Kind, EsPoint Position, MarqueeSnapshot Snapshot);

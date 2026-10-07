using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズームセッションから UI 層へ送る通知の種別。</summary>
/// <remarks>UI 層は自 UI の公開イベントへ転送するだけにする（判断を持たない）。</remarks>
public enum ZoomNoticeKind
{
    /// <summary>ズーム状態（実効ズーム率・Fit 状態・可否）が変化した。</summary>
    ZoomChanged,

    /// <summary>ビューポート（パン・ズーム・表示条件）が変化した。</summary>
    ViewportChanged,
}

/// <summary>ズームセッションから UI 層へ送る通知（種別と通知時点の値）。</summary>
/// <remarks>
/// <see cref="ZoomNoticeKind.ZoomChanged"/> は <see cref="State"/> に確定した 4 値を運ぶ。<br/>
/// <see cref="ZoomNoticeKind.ViewportChanged"/> は画像の表示矩形（<see cref="ImageRect"/>）を運び、<br/>
/// オーバーレイ（切り出しの暗転マスク・選択枠・ルーペ等）が同じ矩形を参照できるようにする。<br/>
/// <b>record である理由</b>: 本層（UI 非依存のコア）の通知は素のデリゲートで公開するため、
/// <see cref="EventArgs"/> 派生にする必要がない（値の等価を持つ record のままとし、公開型を増やさない）。
/// </remarks>
/// <param name="Kind">通知の種別。</param>
/// <param name="State">通知時点のズーム状態（<see cref="ZoomNoticeKind.ZoomChanged"/> で意味を持つ）。</param>
/// <param name="ImageRect">通知時点の画像表示矩形（ビューポート座標。 画像未設定時は空）。</param>
public sealed record ZoomNotice(ZoomNoticeKind Kind, ZoomState State, EsRect ImageRect);

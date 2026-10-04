using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

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
/// <b>クラスである理由</b>: 公開イベントの引数は <see cref="EventArgs"/> 派生にする（CA1003・CA1711）。 <c>record</c> は
/// <see cref="EventArgs"/> を継承できないため、やむを得ずクラスとする。
/// </remarks>
/// <param name="kind">通知の種別。</param>
/// <param name="state">通知時点のズーム状態（<see cref="ZoomNoticeKind.ZoomChanged"/> で意味を持つ）。</param>
/// <param name="imageRect">通知時点の画像表示矩形（ビューポート座標。 画像未設定時は空）。</param>
public sealed class ZoomNotice(ZoomNoticeKind kind, ZoomState state, EsRect imageRect) : EventArgs
{
    /// <summary>通知の種別を取得する。</summary>
    /// <value>ズーム状態の変化か、ビューポートの変化か。</value>
    public ZoomNoticeKind Kind { get; } = kind;

    /// <summary>通知時点のズーム状態を取得する。</summary>
    /// <value>実効ズーム率・Fit 状態・可否を含む値オブジェクト。</value>
    public ZoomState State { get; } = state;

    /// <summary>通知時点の画像表示矩形を取得する。</summary>
    /// <value>ビューポート座標の矩形。 画像未設定時は空。</value>
    public EsRect ImageRect { get; } = imageRect;
}

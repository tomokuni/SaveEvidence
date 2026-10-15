using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.ZoomView;

/// <summary>ズーム率変更イベントの引数（WinForms 版）。</summary>
/// <remarks>
/// 通知する値をコアの <see cref="ZoomState"/>（4 値の値オブジェクト）として保持する。<br/>
/// 4 値はコアのセッションが同時に確定するため、UI 層が個別に組み立てない（判断を持たない）。<br/>
/// 利用側の利便性のため、各値は個別プロパティでも参照できる（他の UI 版と同じ情報を通知し、ホストの結線を揃える）。<br/>
/// <see cref="ZoomPercent"/> は実効値のため Fit 中でも 0 にならない。 Fit 判定は <see cref="IsFit"/> を使用する。
/// </remarks>
/// <param name="state">通知時点のズーム状態（実効ズーム率・Fit 状態・可否）。</param>
/// <remarks>
/// <b>record ではなく class</b>: イベント引数は <see cref="EventArgs"/> 派生にする（CA1711・CA1003。<br/>
/// C# の <c>record</c> は <see cref="EventArgs"/> を継承できないため class とする）。
/// </remarks>
public sealed class ZoomChangedEventArgs(ZoomState state) : EventArgs
{
    /// <summary>通知時点のズーム状態を取得する。</summary>
    /// <value>実効ズーム率・Fit 状態・可否を含む値オブジェクト。</value>
    public ZoomState State { get; } = state;

    /// <summary>現在の実効ズーム率（%）を取得する。</summary>
    /// <value>Fit 中は実効倍率、固定倍率中はズーム率。 0 にはならない。</value>
    public double ZoomPercent => State.ZoomPercent;

    /// <summary>現在 Fit 表示中かどうかを取得する。</summary>
    /// <value>Fit 表示中（内部ズーム率が 0）の場合は true。</value>
    public bool IsFit => State.IsFit;

    /// <summary>ズームイン可能かどうかを取得する。</summary>
    /// <value>可能な場合は true。 画像未設定時は false。</value>
    public bool CanZoomIn => State.CanZoomIn;

    /// <summary>ズームアウト可能かどうかを取得する。</summary>
    /// <value>可能な場合は true。 画像未設定時は false。</value>
    public bool CanZoomOut => State.CanZoomOut;
}

using EsUtil.SaveEvidence.ZoomView;

namespace EsUtil.SaveEvidence.ZoomView.MewUI;

/// <summary>ズーム率変更イベントの引数（MewUI 版）。</summary>
/// <remarks>
/// 通知する値をコアの <see cref="ZoomState"/>（4 値の値オブジェクト）として保持する。<br/>
/// 4 値はコアのセッションが同時に確定するため、UI 層が個別に組み立てない（判断を持たない）。<br/>
/// 利用側の利便性のため、各値は個別プロパティでも参照できる（Avalonia 版と同じ情報を通知し、UI 間でホストの結線を揃える）。<br/>
/// <see cref="ZoomPercent"/> は実効値のため Fit 中でも 0 にならない。Fit 判定は <see cref="IsFit"/> を使用する。
/// </remarks>
/// <param name="State">通知時点のズーム状態（実効ズーム率・Fit 状態・可否）。</param>
public sealed record ZoomChangedEventArgs(ZoomState State)
{
    /// <summary>現在の実効ズーム率（%）を取得する。</summary>
    /// <value>Fit 中は実効倍率、固定倍率中はズーム率。0 にはならない。</value>
    public double ZoomPercent => State.ZoomPercent;

    /// <summary>現在 Fit 表示中かどうかを取得する。</summary>
    /// <value>Fit 表示中（内部ズーム率が 0）の場合は true。</value>
    public bool IsFit => State.IsFit;

    /// <summary>ズームイン可能かどうかを取得する。</summary>
    /// <value>可能な場合は true。画像未設定時は false。</value>
    public bool CanZoomIn => State.CanZoomIn;

    /// <summary>ズームアウト可能かどうかを取得する。</summary>
    /// <value>可能な場合は true。画像未設定時は false。</value>
    public bool CanZoomOut => State.CanZoomOut;
}

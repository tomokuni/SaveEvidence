using EsUtil.SaveEvidence.ZoomView;

namespace EsUtil.SaveEvidence.ZoomView.AvaloniaUI;

/// <summary>ズーム率変更イベントの引数。</summary>
/// <remarks>
/// 通知する値をコアの <see cref="ZoomState"/>（4 値の値オブジェクト）として保持する。<br/>
/// 4 値（実効ズーム率・Fit 状態・⊖⊕ ボタンの Enable/Disable 制御に必要な可否）はコアのセッションが同時に確定するため、<br/>
/// UI 層が個別に組み立てない（判断を持たない）。利用側の利便性のため、各値は個別プロパティでも参照できる。<br/>
/// <para/>
/// <b>Fit 判定</b>: <see cref="ZoomPercent"/> が 0 にならないため、<see cref="IsFit"/> で Fit 状態を判定する。<br/>
/// <b>表示文字列の生成</b>: 表示形式（"%" 表記等）はホストの都合で決まるため、ライブラリは <see cref="ZoomPercent"/>（数値）のみを通知し、文字列化はホストが行う。
/// </remarks>
/// <param name="State">通知時点のズーム状態（実効ズーム率・Fit 状態・可否）。</param>
public sealed record ZoomChangedEventArgs(ZoomState State)
{
    /// <summary>現在の実効ズーム率（%）を取得する。</summary>
    /// <value>Fit 中は実効倍率、固定倍率中はズーム率。0 にはならない（Fit 中でも実効倍率を返す）。</value>
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

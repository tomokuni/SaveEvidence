using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI;

/// <summary>選択矩形イベントの引数（イベント引数、対象座標の選択矩形）。</summary>
/// <remarks>
/// 確定要求（<see cref="FadeMaskDragRectControl.ConfirmRequested"/>）のイベント引数。<br/>
/// 内包コントロールの型に依存しない独自型とし、ホストは本複合コントロールの公開 API のみで利用できる（公開型のカプセル化）。<br/>
/// 座標は UI 非依存のコア型で公開する（境界変換が不要になる）。
/// </remarks>
/// <param name="selectionSourceRect">選択矩形（対象座標）。</param>
/// <remarks>
/// <b>record ではなく class</b>: イベント引数は <see cref="EventArgs"/> 派生にする（CA1711・CA1003。<br/>
/// C# の <c>record</c> は <see cref="EventArgs"/> を継承できないため class とする）。
/// </remarks>
public sealed class FadeMaskDragRectSelectionEventArgs(EsRect selectionSourceRect) : EventArgs
{
    /// <summary>選択矩形（対象座標）を取得する。</summary>
    /// <value>ドラッグ選択中の矩形。</value>
    public EsRect SelectionSourceRect { get; } = selectionSourceRect;
}

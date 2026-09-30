using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.MewUI;

/// <summary>選択矩形イベントの引数（イベント引数、ドメイン座標の選択矩形）。</summary>
/// <remarks>
/// 確定要求（<see cref="FadeMaskDragRectControl.ConfirmRequested"/>）のイベント引数。<br/>
/// 内包コントロールの型に依存しない独自型とし、ホストは本複合コントロールの公開 API のみで利用できる（公開型のカプセル化）。<br/>
/// 座標は UI 非依存のコア型で公開する（境界変換が不要になる）。
/// </remarks>
/// <param name="SelectionDomainRect">選択矩形（ドメイン座標）。</param>
public sealed record FadeMaskDragRectSelectionEventArgs(EsRect SelectionDomainRect);

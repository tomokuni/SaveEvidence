using Estable.Platform.Geometry;

namespace Estable.MewUI.FadeMaskDragRect;

/// <summary>ポインター位置イベントの引数（ドメイン座標の位置）。</summary>
/// <remarks>
/// ルーペ中心の更新通知（<see cref="FadeMaskDragRectControl.LoupePositionMoved"/>）のイベント引数。<br/>
/// 内包コントロールの型に依存しない独自型とし、ホストは本複合コントロールの公開 API のみで利用できる（公開型のカプセル化）。<br/>
/// 座標は UI 非依存のコア型で公開する（境界変換が不要になる）。
/// </remarks>
/// <param name="Position">ルーペ中心（ドメイン座標）。</param>
public sealed record FadeMaskDragRectPointEventArgs(EsPoint Position);

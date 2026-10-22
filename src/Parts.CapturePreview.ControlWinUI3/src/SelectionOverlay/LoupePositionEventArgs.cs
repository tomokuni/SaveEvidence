
namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.SelectionOverlay;

/// <summary>ポインター位置イベントの引数（対象座標の位置）。</summary>
/// <remarks>
/// ルーペ中心の更新通知（<see cref="SelectionOverlayControl.LoupePositionMoved"/>）のイベント引数。<br/>
/// 内包コントロールの型に依存しない独自型とし、ホストは本複合コントロールの公開 API のみで利用できる（公開型のカプセル化）。<br/>
/// 座標は UI 非依存のコア型で公開する（境界変換が不要になる）。
/// </remarks>
/// <param name="position">ルーペ中心（対象座標）。</param>
/// <remarks>
/// <b>record ではなく class</b>: イベント引数は <see cref="EventArgs"/> 派生にする（CA1711・CA1003。<br/>
/// C# の <c>record</c> は <see cref="EventArgs"/> を継承できないため class とする）。
/// </remarks>
public sealed class LoupePositionEventArgs(EsPoint position) : EventArgs
{
    /// <summary>ルーペ中心（対象座標）を取得する。</summary>
    /// <value>ドラッグ中のポインター位置。</value>
    public EsPoint Position { get; } = position;
}

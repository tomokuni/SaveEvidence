using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;

/// <summary>キャプチャ完了イベントの引数（キャプチャ結果）。</summary>
/// <remarks>
/// イベントの型は <see cref="EventHandler{T}"/>（引数は <see cref="EventArgs"/> 派生）に統一する（CA1003）。<br/>
/// 結果は <see cref="CaptureResult"/>（キャンセル時は 0x0 の空結果）をそのまま運ぶ。<br/>
/// UI 型（Avalonia の型）は含めない（コア型だけを公開する）。
/// </remarks>
/// <param name="result">キャプチャ結果。 キャンセル・エラー時は 0x0 の空結果。</param>
public sealed class CaptureCompletedEventArgs(CaptureResult? result) : EventArgs
{
    /// <summary>キャプチャ結果を取得する。</summary>
    /// <value>画像と撮影領域。 キャンセル・エラー時は 0x0 の空結果。</value>
    public CaptureResult? Result { get; } = result;
}

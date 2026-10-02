namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>カーソル下の要素を検出する対象。</summary>
/// <remarks>
/// <see cref="CaptureTypeTraits.GetElementTarget"/> が返し、UI 層は対象に対応する検出関数（画面 / ウィンドウ）を呼ぶ。<br/>
/// UI フレームワークに依存しない列挙型である。
/// </remarks>
public enum CaptureElementTarget
{
    /// <summary>カーソル下のウィンドウを検出する（ウィンドウ選択・範囲選択で使用）。</summary>
    Window,

    /// <summary>カーソル下の画面（モニター）を検出する（スクリーン選択で使用）。</summary>
    Screen,
}

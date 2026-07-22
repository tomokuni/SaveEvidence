namespace app.Enum;

/// <summary>ウィンドウキャプチャ時の画像取得方式を指定する列挙型。</summary>
/// <remarks>
/// <see cref="PrintWindow"/>: DWM から直接描画内容を取得するため1px枠線の写り込みが発生しない。ただし枠の色がクラシック風になり、DirectX/GPU 描画コンテンツでは正しく取得できない場合がある。<br/>
/// <see cref="CopyFromScreen"/>: 従来の画面矩形コピー方式。1px枠線部分に背景が映り込む可能性がある。ただし他の Window が前面に重なっている場合は、見た目通りの重なった状態で取得される。
/// </remarks>
public enum WindowCaptureMode
{
    /// <summary>PrintWindow API を使用（DWM から直接取得、1px枠線防止）。</summary>
    PrintWindow = 0,

    /// <summary>CopyFromScreen を使用（従来方式、1px枠線写り込みあり）。</summary>
    CopyFromScreen = 1,
}

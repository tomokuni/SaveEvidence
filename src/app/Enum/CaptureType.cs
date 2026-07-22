namespace app.Enum;

/// <summary>画面キャプチャの取得方法を指定する列挙型。</summary>
/// <remarks>
/// スクリーン全体、特定ウィンドウ、またはユーザーがマウスドラッグで指定した矩形領域の3種類のキャプチャ方法を提供する。<br/>
/// 各モードは <see cref="Views.SelectionForm"/> で対応するUIを表示する。<br/>
/// </remarks>
public enum CaptureType
{
    /// <summary>スクリーン選択</summary>
    SelectScreen,

    /// <summary>ウィンドウ選択</summary>
    WindowSelect,

    /// <summary>範囲選択</summary>
    AreaSelect
}

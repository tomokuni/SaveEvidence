namespace Estable.AvaloniaUI.Platform;

/// <summary>ウィンドウキャプチャ時の画像取得方式を指定する列挙型。</summary>
/// <remarks>
/// プラットフォーム非依存の概念で定義する（Win32 API 名に依存しない）。<br/>
/// Linux / macOS の実装を差し替えても成立する。<br/>
/// </remarks>
public enum WindowCaptureMode
{
    /// <summary>ウィンドウの再描画結果を直接取得する方式（Windows: PrintWindow / DWM、X11: Composite、macOS: ScreenCaptureKit 相当）。</summary>
    /// <remarks>
    /// ウィンドウが隠れていても DWM 等の合成レイヤーから取得できるため、1px 枠線等のずれを防ぐ。<br/>
    /// プラットフォームごとの実装が「直接取得」を提供する。<br/>
    /// </remarks>
    RedrawDirect = 0,

    /// <summary>画面上の表示をそのままコピーする方式（従来方式）。</summary>
    /// <remarks>
    /// 画面全体のキャプチャから対象矩形を切り出す。<br/>
    /// Windows: CopyFromScreen / X11: 画面キャプチャ / macOS: ScreenCaptureKit のディスプレイキャプチャ相当。<br/>
    /// </remarks>
    ScreenCopy = 1,
}

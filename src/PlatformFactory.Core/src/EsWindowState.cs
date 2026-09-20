namespace Estable.Platform;

/// <summary>ウィンドウの表示状態（UI フレームワーク非依存）。</summary>
/// <remarks>
/// 各 UI フレームワークのウィンドウ状態（例: Avalonia の <c>WindowState</c>）と同名のメンバーを持ち、<br/>
/// 設定ファイルの文字列表現（<c>JsonStringEnumConverter</c>）の互換性を保つ。<br/>
/// 相互変換は各 UI プロジェクトの Interop 層（Avalonia 版は <c>EsWindowStateAvaloniaInterop</c>）が提供する。<br/>
/// </remarks>
public enum EsWindowState
{
    /// <summary>通常表示。</summary>
    Normal,

    /// <summary>最小化。</summary>
    Minimized,

    /// <summary>最大化。</summary>
    Maximized,

    /// <summary>全画面表示。</summary>
    FullScreen,
}

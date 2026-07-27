using Avalonia;

namespace Estable.AvaloniaUI.Platform;

/// <summary>OS依存のカーソル操作を抽象化するインターフェイス。</summary>
/// <remarks>
/// AvaloniaUI 標準で代替可能な機能（カーソル形状変更は <c>Cursor</c> プロパティ、<br/>
/// イベント内のポインター位置取得は <c>PointerEventArgs.GetPosition</c>）は本インターフェイスに含めない。<br/>
/// 本インターフェイスには、AvaloniaUI 標準に存在しない<b>OSカーソルの位置取得</b>・<b>位置移動</b>・<br/>
/// <b>表示/非表示</b>のみを残す。<br/>
/// デフォルト実装は <see cref="WinCursorService"/>（Win32 API ベース）。<br/>
/// <br/>
/// 本インターフェイスの API はプラットフォーム独立であり、Win32 ハンドル（<c>IntPtr</c>）等の<br/>
/// プラットフォーム固有型を公開しない。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、以下を達成する（DIP）:<br/>
/// - デフォルト（<see cref="WinCursorService"/>）は Windows で動作<br/>
/// - 他プラットフォームでは実装を注入して差し替え可能<br/>
/// - テスト時のモックが可能<br/>
/// </remarks>
public interface ICursorService
{
    /// <summary>現在のカーソル位置を画面物理座標で取得する。</summary>
    /// <remarks>WM_SETCURSOR 等の OS イベント外でカーソルの絶対位置が必要な場合に使用する。</remarks>
    PixelPoint GetCursorScreenPoint();

    /// <summary>カーソルを画面物理座標に移動する。</summary>
    /// <param name="point">移動先の画面物理座標。</param>
    void SetCursorScreenPoint(PixelPoint point);

    /// <summary>マウスポインターの表示/非表示を切り替える。</summary>
    /// <param name="show">表示する場合は <see langword="true"/>、非表示にする場合は <see langword="false"/>。</param>
    void ShowCursor(bool show);
}

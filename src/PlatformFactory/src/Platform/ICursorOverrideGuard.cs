using Avalonia.Controls;

namespace Estable.AvaloniaUI.Platform;

/// <summary>ホストウィンドウの「カーソル上書き抑止」を抽象化するインターフェイス。</summary>
/// <remarks>
/// **意図（希望する機能）**: 選択操作（Crop モード等）中に OS がカーソル形状を上書きするのを防ぎ、<br/>
/// 選択操作のカーソル形状（十字・リサイズ等）を維持する。<br/>
/// <br/>
/// これはプラットフォーム非依存の抽象操作である:<br/>
/// - Windows: WndProc サブクラス化で WM_SETCURSOR を抑止する（<see cref="WinCursorOverrideGuard"/>）。<br/>
/// - X11: カーソル上書きはウィンドウマネージャーに依存するため、カーソル形状の再適用等で対応する。<br/>
/// - macOS: NSCursor はビュー階層で制御され、WM_SETCURSOR 相当の上書きは発生しないため<br/>
///   実質 no-op でよい（IF のコントラクトは成立する）。<br/>
/// <br/>
/// ウィンドウの特定は <see cref="TopLevel"/>（Avalonia 抽象）で行い、<br/>
/// プラットフォーム固有のメッセージ番号・ハンドルは公開しない。<br/>
/// </remarks>
public interface ICursorOverrideGuard : IDisposable
{
    /// <summary>カーソル上書き抑止の判定コールバック。</summary>
    /// <remarks>
    /// OS がカーソルを上書きしようとしたときに評価される。<br/>
    /// true を返すと上書きを抑止し、その副作用として現在のカーソル形状を再適用する。<br/>
    /// false を返すと OS のデフォルト動作に委譲する。<br/>
    /// </remarks>
    Func<bool>? Guard { get; set; }

    /// <summary>抑止がアタッチ済みかどうか。</summary>
    bool IsAttached { get; }

    /// <summary>指定したウィンドウにカーソル上書き抑止をアタッチする。</summary>
    /// <param name="window">対象ウィンドウ。</param>
    /// <remarks>
    /// 抑止を実現できない環境（デザインモード等）では何もせず、<see cref="IsAttached"/> は false のままとなる。<br/>
    /// </remarks>
    void Attach(TopLevel window);
}

using Avalonia.Controls;

namespace Estable.AvaloniaUI.Platform;

/// <summary>ホストウィンドウの「カーソル上書き抑止」を抽象化するインターフェイス。</summary>
/// <remarks>
/// 選択操作（Crop モード等）中に OS がカーソル形状を上書きするのを防ぎ、選択操作のカーソル形状を維持する。<br/>
/// ウィンドウの特定は <see cref="TopLevel"/>（Avalonia 抽象）で行い、プラットフォーム固有のメッセージ番号・ハンドルは公開しない。
/// </remarks>
public interface ICursorOverrideGuard : IDisposable
{
    /// <summary>抑止がアタッチ済みかどうか。</summary>
    bool IsAttached { get; }

    /// <summary>指定したウィンドウにカーソル上書き抑止をアタッチする。</summary>
    /// <param name="window">対象ウィンドウ。</param>
    /// <remarks>
    /// 抑止を実現できない環境（デザインモード等）では何もせず、<see cref="IsAttached"/> は false のままとなる。<br/>
    /// アタッチ中は、OS によるカーソル形状の上書き（WM_SETCURSOR）を抑止し、現在のカーソル形状を維持する。
    /// </remarks>
    void Attach(TopLevel window);
}

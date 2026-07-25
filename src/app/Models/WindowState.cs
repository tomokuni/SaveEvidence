namespace app.Models;

/// <summary>ウィンドウの表示状態を表す列挙型（JSONシリアル化対応）。</summary>
/// <remarks>
/// <see cref="System.Windows.Forms.FormWindowState"/> の代替として使用する。<br/>
/// </remarks>
public enum WindowStateValue
{
    /// <summary>通常状態</summary>
    Normal = 0,

    /// <summary>最小化状態</summary>
    Minimized = 1,

    /// <summary>最大化状態</summary>
    Maximized = 2,
}

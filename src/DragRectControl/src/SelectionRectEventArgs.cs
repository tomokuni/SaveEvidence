using Avalonia;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>選択矩形に関する通知のイベント引数。</summary>
/// <remarks>
/// 矩形（<see cref="Rect"/>）に加え、通知時点のスナップショット（<see cref="Snapshot"/>）を含む。<br/>
/// スクロール補正はイベント引数では行わず、座標変換ポート（<see cref="Estable.AvaloniaUI.Platform.ICoordTransform"/>）方式が担う
/// （<c>Compose(ImageLayout, ScrollOffset)</c> で変換に組み込む）。
/// </remarks>
/// <param name="rect">通知対象の矩形（ドメイン座標）。</param>
/// <param name="snapshot">通知時点のスナップショット。</param>
public sealed class SelectionRectEventArgs(Rect rect, DragRectSnapshot snapshot) : EventArgs
{
    /// <summary>通知対象の矩形（ドメイン座標）。</summary>
    public Rect Rect { get; } = rect;

    /// <summary>通知時点のスナップショット（状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。</summary>
    public DragRectSnapshot Snapshot { get; } = snapshot;
}

using Aprillz.MewUI;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.ZoomView;

/// <summary>ズームビューの押下を通知するイベントの引数。</summary>
/// <param name="position">押下位置（ズームビューの座標）。</param>
/// <param name="isRightButton">右ボタンの押下かどうか。</param>
public sealed class MousePressedEventArgs(Point position, bool isRightButton) : EventArgs
{
    /// <summary>押下位置（ズームビューの座標）を取得する。</summary>
    public Point Position { get; } = position;

    /// <summary>右ボタンの押下かどうかを取得する。</summary>
    public bool IsRightButton { get; } = isRightButton;
}

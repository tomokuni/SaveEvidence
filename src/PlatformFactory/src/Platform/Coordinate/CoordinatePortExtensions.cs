using Avalonia;

namespace Estable.AvaloniaUI.Platform;

/// <summary><see cref="ICoordinatePort"/> の矩形変換拡張メソッド。</summary>
/// <remarks>
/// ポート（Point 変換）を矩形（<see cref="Rect"/>）に適用する汎用ヘルパーを提供する。<br/>
/// 対角 2 点（TopLeft / BottomRight）を変換して矩形を再構築する（アフィン変換が一様なため等価）。<br/>
/// 座標変換の矩形適用は各プロジェクト（DragRectControl / SnipCaptureUI 等）で共通のため、ここに集約する（DRY 原則）。
/// </remarks>
public static class CoordinatePortExtensions
{
    /// <summary>ポートの順変換（座標系 A → B）で矩形を変換する。</summary>
    /// <param name="port">変換ポート。</param>
    /// <param name="rect">座標系 A の矩形。</param>
    /// <returns>座標系 B の矩形。</returns>
    /// <remarks>点変換の <see cref="ICoordinatePort.Forward"/> を矩形の対角 2 点に適用する（命名規則: Forward → ForwardRect）。</remarks>
    public static Rect ForwardRect(this ICoordinatePort port, Rect rect)
    {
        var tl = port.Forward(rect.TopLeft);
        var br = port.Forward(rect.BottomRight);
        return new Rect(tl, br);
    }

    /// <summary>ポートの逆変換（座標系 B → A）で矩形を変換する。</summary>
    /// <param name="port">変換ポート。</param>
    /// <param name="rect">座標系 B の矩形。</param>
    /// <returns>座標系 A の矩形。</returns>
    /// <remarks>点変換の <see cref="ICoordinatePort.Inverse"/> を矩形の対角 2 点に適用する（命名規則: Inverse → InverseRect）。</remarks>
    public static Rect InverseRect(this ICoordinatePort port, Rect rect)
    {
        var tl = port.Inverse(rect.TopLeft);
        var br = port.Inverse(rect.BottomRight);
        return new Rect(tl, br);
    }
}

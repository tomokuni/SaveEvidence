using Avalonia;

namespace Estable.AvaloniaUI.LineDetection;

/// <summary>線分（辺）の向き。</summary>
public enum LineOrientation
{
    /// <summary>水平（左右方向）の辺。</summary>
    Horizontal,

    /// <summary>垂直（上下方向）の辺。</summary>
    Vertical,
}

/// <summary>検出された辺（線分）の結果。</summary>
/// <param name="Start">始点（画像座標・ピクセル）。</param>
/// <param name="End">終点（画像座標・ピクセル）。</param>
/// <param name="Orientation">線分の向き（水平 / 垂直）。</param>
/// <param name="Score">確からしさ（長さ・信頼度等）。大きいほど有力。</param>
/// <remarks>
/// 座標は UI フレームワーク非依存の <see cref="Point"/>（double の XY 値型）で保持する。
/// これにより検出ロジック（<see cref="LogicLineDetector"/>）と上位の表示境界（app / sample）を分離し、<br/>
/// 単体テストをヘッドレス不要（<c>[Fact]</c> 並列）で実行できる（移植性・テスト容易性向上）。
/// </remarks>
public readonly record struct LineDetectionResult(Point Start, Point End, LineOrientation Orientation, double Score);


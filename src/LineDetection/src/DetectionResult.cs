using Estable.AvaloniaUI.Platform.Imaging;
using Estable;

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
/// 座標は UI フレームワーク非依存の <see cref="PointD"/>（double の XY 値型）で保持する。
/// これにより検出ロジック（<see cref="LogicLineDetector"/>）と上位の表示境界（app / sample）を分離し、<br/>
/// 単体テストをヘッドレス不要（<c>[Fact]</c> 並列）で実行できる（移植性・テスト容易性向上）。
/// </remarks>
public readonly record struct LineDetectionResult(PointD Start, PointD End, LineOrientation Orientation, double Score);

/// <summary>辺（線分）検出器の共通インターフェース。</summary>
public interface ILineDetector
{
    /// <summary>検出方式の表示名。</summary>
    string Name { get; }

    /// <summary>モデルサイズの表示（例: "48 MB"）。モデルを使わない方式は空文字。</summary>
    string ModelInfo { get; }

    /// <summary>モデル読込・実行エラーのメッセージ（モデル未配置等）。正常時は null。</summary>
    string? ErrorMessage { get; }

    /// <summary>画像から水平・垂直の辺（線分）を検出する。</summary>
    /// <param name="image">解析対象の画像（PbBitmap・BGRA8888）。</param>
    /// <returns>検出した線分のリスト（スコア降順）。</returns>
    IReadOnlyList<LineDetectionResult> Detect(PbBitmap image);
}

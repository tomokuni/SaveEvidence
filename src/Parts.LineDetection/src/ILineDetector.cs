using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.Parts.LineDetection;

/// <summary>辺（線分）検出器の共通インターフェース。</summary>
public interface ILineDetector
{
    /// <summary>検出方式の表示名。</summary>
    string Name { get; }

    /// <summary>モデルサイズの表示（例: "48 MB"）。 モデルを使わない方式は空文字。</summary>
    string ModelInfo { get; }

    /// <summary>モデル読込・実行エラーのメッセージ（モデル未配置等）。 正常時は null。</summary>
    string? ErrorMessage { get; }

    /// <summary>画像から水平・垂直の辺（線分）を検出する。</summary>
    /// <param name="image">解析対象の画像（EsReadOnlyBitmap・BGRA8888）。</param>
    /// <returns>検出した線分のリスト（スコア降順）。</returns>
    IReadOnlyList<LineDetectionResult> Detect(EsReadOnlyBitmap image);
}

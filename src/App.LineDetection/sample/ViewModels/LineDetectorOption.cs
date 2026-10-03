
namespace EsUtil.SaveEvidence.LineDetection.Sample.ViewModels;

/// <summary>検出方式の選択肢（表示名 + 検出器インスタンス）。</summary>
public sealed class LineDetectorOption
{
    /// <summary>検出器を初期化する。</summary>
    /// <param name="detector">検出器インスタンス。</param>
    public LineDetectorOption(ILineDetector detector) => Detector = detector;

    /// <summary>検出器インスタンス。</summary>
    public ILineDetector Detector { get; }

    /// <summary>検出方式の表示名。</summary>
    public string Name => Detector.Name;

    /// <summary>表示名（モデルサイズ付き）。 例: "ロジック（Canny + Hough）"。</summary>
    public string DisplayName => string.IsNullOrEmpty(Detector.ModelInfo)
        ? Detector.Name
        : $"{Detector.Name}（{Detector.ModelInfo}）";

    /// <summary>表示名を返す（ComboBox 表示用）。</summary>
    public override string ToString() => DisplayName;
}

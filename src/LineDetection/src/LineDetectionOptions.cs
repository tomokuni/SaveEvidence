namespace Estable.AvaloniaUI.LineDetection;

/// <summary>ロジック方式の線分検出パラメータ（不変値・検証付き）。</summary>
/// <param name="CannyThreshold1">Canny の低閾値。正の値。</param>
/// <param name="CannyThreshold2">Canny の高閾値。<paramref name="CannyThreshold1"/> 以上。</param>
/// <param name="HoughThreshold">HoughLinesP の投票閾値。正の値。</param>
/// <param name="MinLineLength">検出する線分の最小長さ（ピクセル）。正の値。</param>
/// <param name="MaxLineGap">同一線分とみなす最大ギャップ（ピクセル）。0 以上。</param>
/// <param name="AngleTolerance">水平 / 垂直とみなす角度許容差（度）。0 以上 90 未満。</param>
/// <param name="ClusterTolerance">辺のクラスタリング許容差（ピクセル）。0 以上。</param>
/// <param name="MinEdgeSupportRatio">線分に沿ったエッジピクセルの最小割合（0.0〜1.0）。</param>
/// <param name="ParallelMergeTolerance">矩形の辺に統合する平行線の最大距離（ピクセル）。0 以上。</param>
/// <param name="CornerToleranceRatio">矩形のコーナー判定の許容差（辺の長さに対する比率）。0 以上。</param>
/// <param name="OppositeSpanRatio">矩形の対辺の長さの差の許容比率。0 以上。</param>
/// <remarks>
/// ロジック方式（Canny + Hough）の検出パラメータを単一の不変値として集約する。<br/>
/// <see cref="LogicLineDetector"/> が既定値（<see cref="Default"/>）を所有し、不変条件を満たさない値は生成時に例外を投げる。<br/>
/// パラメータの個別変更を許さず、設定の明示性と誤設定防止を図る（カプセル化・SOLID）。
/// </remarks>
public sealed record LineDetectionOptions
{
    /// <summary>Canny の低閾値。</summary>
    public int CannyThreshold1 { get; init; }

    /// <summary>Canny の高閾値。</summary>
    public int CannyThreshold2 { get; init; }

    /// <summary>HoughLinesP の投票閾値。</summary>
    public int HoughThreshold { get; init; }

    /// <summary>検出する線分の最小長さ（ピクセル）。</summary>
    public int MinLineLength { get; init; }

    /// <summary>同一線分とみなす最大ギャップ（ピクセル）。</summary>
    public int MaxLineGap { get; init; }

    /// <summary>水平 / 垂直とみなす角度許容差（度）。</summary>
    public double AngleTolerance { get; init; }

    /// <summary>辺のクラスタリング許容差（ピクセル）。</summary>
    public int ClusterTolerance { get; init; }

    /// <summary>線分に沿ったエッジピクセルの最小割合（0.0〜1.0）。</summary>
    public double MinEdgeSupportRatio { get; init; }

    /// <summary>矩形の辺に統合する平行線の最大距離（ピクセル）。</summary>
    public double ParallelMergeTolerance { get; init; }

    /// <summary>矩形のコーナー判定の許容差（辺の長さに対する比率）。</summary>
    public double CornerToleranceRatio { get; init; }

    /// <summary>矩形の対辺の長さの差の許容比率。</summary>
    public double OppositeSpanRatio { get; init; }

    /// <summary>従来の定数値に基づく既定の設定。</summary>
    public static LineDetectionOptions Default { get; } = new()
    {
        CannyThreshold1 = 20,
        CannyThreshold2 = 60,
        HoughThreshold = 40,
        MinLineLength = 50,
        MaxLineGap = 10,
        AngleTolerance = 5.0,
        ClusterTolerance = 1,
        MinEdgeSupportRatio = 0.6,
        ParallelMergeTolerance = 8.0,
        CornerToleranceRatio = 0.2,
        OppositeSpanRatio = 0.13,
    };

    /// <summary>不変条件（各パラメータの値・範囲）を検証する。</summary>
    /// <exception cref="ArgumentException">不変条件を満たさない場合。</exception>
    /// <remarks>
    /// 値の範囲・関係（CannyThreshold2 ≥ CannyThreshold1、比率 0〜1 等）を強制し、<br/>
    /// 不正な設定での検出を防ぐ（堅牢化）。
    /// </remarks>
    public void Validate()
    {
        if (CannyThreshold1 <= 0) throw new ArgumentException("CannyThreshold1 は正の値が必要です。", nameof(CannyThreshold1));
        if (CannyThreshold2 < CannyThreshold1) throw new ArgumentException("CannyThreshold2 は CannyThreshold1 以上が必要です。", nameof(CannyThreshold2));
        if (HoughThreshold <= 0) throw new ArgumentException("HoughThreshold は正の値が必要です。", nameof(HoughThreshold));
        if (MinLineLength <= 0) throw new ArgumentException("MinLineLength は正の値が必要です。", nameof(MinLineLength));
        if (MaxLineGap < 0) throw new ArgumentException("MaxLineGap は 0 以上が必要です。", nameof(MaxLineGap));
        if (AngleTolerance < 0 || AngleTolerance >= 90) throw new ArgumentException("AngleTolerance は 0 以上 90 未満が必要です。", nameof(AngleTolerance));
        if (ClusterTolerance < 0) throw new ArgumentException("ClusterTolerance は 0 以上が必要です。", nameof(ClusterTolerance));
        if (MinEdgeSupportRatio is < 0 or > 1) throw new ArgumentException("MinEdgeSupportRatio は 0.0〜1.0 が必要です。", nameof(MinEdgeSupportRatio));
        if (ParallelMergeTolerance < 0) throw new ArgumentException("ParallelMergeTolerance は 0 以上が必要です。", nameof(ParallelMergeTolerance));
        if (CornerToleranceRatio < 0) throw new ArgumentException("CornerToleranceRatio は 0 以上が必要です。", nameof(CornerToleranceRatio));
        if (OppositeSpanRatio < 0) throw new ArgumentException("OppositeSpanRatio は 0 以上が必要です。", nameof(OppositeSpanRatio));
    }
}

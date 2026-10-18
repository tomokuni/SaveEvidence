namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>画像の 1 辺分の影（幅・濃さの変化・向き）を表す描画計画。</summary>
/// <remarks>
/// <b>この型は値だけを持ち、描画 API には依存しない</b>（4 つの UI 版が自分の描画 API へ写す）。<br/>
/// 辺の幅は <see cref="Extent"/>、濃さは <see cref="Stops"/>（外側 → 画像の端の順）で表す。
/// </remarks>
public sealed class ShadowEdgePlan
{
    /// <summary>辺の計画を生成する（<see cref="ShadowPlan"/> だけが生成する）。</summary>
    /// <param name="side">辺。</param>
    /// <param name="extent">辺の幅（DIP）。</param>
    /// <param name="outerAtStart">グラデーションの始点が外側かどうか。</param>
    /// <param name="stops">停止点（グラデーションの始点順）。</param>
    internal ShadowEdgePlan(ShadowSide side, double extent, bool outerAtStart, IReadOnlyList<ShadowStop> stops)
    {
        Side = side;
        Extent = extent;
        IsOuterAtStart = outerAtStart;
        Stops = stops;
    }

    /// <summary>辺を取得する。</summary>
    public ShadowSide Side { get; }

    /// <summary>辺の幅（DIP）を取得する。</summary>
    public double Extent { get; }

    /// <summary>グラデーションの始点が外側かどうかを取得する。</summary>
    /// <remarks>true は上辺・左辺（外側が始点）、false は下辺・右辺（画像の端が始点）。</remarks>
    public bool IsOuterAtStart { get; }

    /// <summary>停止点（グラデーションの始点順）を取得する。</summary>
    public IReadOnlyList<ShadowStop> Stops { get; }

    /// <summary>上下の辺かどうかを取得する。</summary>
    public bool IsVertical => Side is ShadowSide.Top or ShadowSide.Bottom;
}
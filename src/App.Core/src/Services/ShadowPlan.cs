namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>画像の 4 辺へ落とす影の描画計画（4 つの UI 版で共通の唯一の定義）。</summary>
/// <remarks>
/// <b>基準</b>: AvaloniaUI 版のフォルダビューの <c>BoxShadow</c>（<c>0 3 10 0 #8A000000</c>）を実測して<br/>
/// 色・幅・グラデーション・透過度を写した値である。 キャプチャページの画像・フォルダビュー・Undo 一覧の<br/>
/// サムネイルは、すべてこの計画から描く（UI 版ごとに影の濃さ・広がりがずれないようにする）。<br/>
/// <br/>
/// <b>描き方</b>: <see cref="BuildEdgeRects"/> が返す 4 辺の矩形を、<see cref="ShadowEdgePlan.Stops"/> の<br/>
/// グラデーションで塗る（GDI+・Skia・XAML・Avalonia のいずれでも同じ手順になる）。<br/>
/// <br/>
/// <b>角の扱い</b>: 上辺・下辺は画像の左右 + <see cref="BlurRadius"/> まで広げて角を埋め、左右の辺は画像の<br/>
/// 高さいっぱいにする（重ねないため、角が二重に濃くならない）。
/// </remarks>
public static class ShadowPlan
{
    /// <summary>影のぼかしの広がり（DIP）。</summary>
    public const double BlurRadius = 10;

    /// <summary>影の下方向のオフセット（DIP）。</summary>
    public const double OffsetY = 3;

    /// <summary>影の色（黒 54%）。</summary>
    /// <remarks>
    /// グラデーションの各停止点は、この色の透過度を<b>実測した濃さ</b>で置き換えて使う<br/>
    /// （<see cref="AlphaAtDistance"/> が返す値がそのまま透過度になる）。
    /// </remarks>
    public const uint ColorArgb = 0x8A000000;

    /// <summary>グラデーションの段数（1 DIP 刻み）。</summary>
    /// <remarks>段数を増やすほど階段が目立たなくなる。 幅 10〜13 DIP に対して 12 段で十分滑らかに見える。</remarks>
    public const int StepCount = 12;

    /// <summary>上辺の幅（DIP）。 影が下へ落ちる分だけ狭い。</summary>
    public const double TopExtent = BlurRadius - OffsetY;

    /// <summary>下辺の幅（DIP）。 影が下へ落ちる分だけ広い。</summary>
    public const double BottomExtent = BlurRadius + OffsetY;

    /// <summary>影の濃さの実測プロファイル（画像の端からの距離 → 不透明度）。</summary>
    /// <remarks>
    /// AvaloniaUI 版の <c>BoxShadow</c>（<c>0 3 10 0 #8A000000</c>）の明色の一覧での実測値。<br/>
    /// 添字 0 は距離 1 DIP、以降 1 DIP 刻みで距離 10 DIP まで（それ以降は 0）。
    /// </remarks>
    private static readonly double[] s_alphaProfile = [0.245, 0.195, 0.150, 0.112, 0.079, 0.050, 0.033, 0.021, 0.012, 0.005];

    /// <summary>4 辺の計画（描画順は上辺・下辺・左辺・右辺）。</summary>
    public static IReadOnlyList<ShadowEdgePlan> Edges { get; } =
    [
        CreateEdge(ShadowSide.Top, TopExtent, OffsetY, outerAtStart: true),
        CreateEdge(ShadowSide.Bottom, BottomExtent, 0, outerAtStart: false),
        CreateEdge(ShadowSide.Left, BlurRadius, 0, outerAtStart: true),
        CreateEdge(ShadowSide.Right, BlurRadius, 0, outerAtStart: false),
    ];

    /// <summary>画像の周囲へ確保する影の余白を取得する。</summary>
    /// <remarks>アイテムの寸法を決めるときに使う（画像の外側にこの分の余地を確保する）。</remarks>
    public static ShadowInsets Insets => new(BlurRadius, TopExtent, BlurRadius, BottomExtent);

    /// <summary>指定の辺の計画を取得する。</summary>
    /// <param name="side">辺。</param>
    /// <returns>辺の計画。</returns>
    public static ShadowEdgePlan Edge(ShadowSide side)
    {
        foreach (var edge in Edges)
        {
            if (edge.Side == side)
            {
                return edge;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(side), side, "未定義の辺です。");
    }

    /// <summary>画像の端から <paramref name="distance"/> DIP 外側の影の不透明度を取得する。</summary>
    /// <param name="distance">画像の端からの距離（DIP）。</param>
    /// <returns>不透明度（0〜1）。</returns>
    /// <remarks>距離 1 DIP 未満は端の値（最も濃い）、<see cref="BlurRadius"/> 以上は 0 として扱う。</remarks>
    public static double AlphaAtDistance(double distance)
    {
        if (distance <= 1)
        {
            return s_alphaProfile[0];
        }

        if (distance >= BlurRadius)
        {
            return distance > BlurRadius ? 0 : s_alphaProfile[^1];
        }

        var index = (int)Math.Floor(distance) - 1;
        var fraction = distance - Math.Floor(distance);
        var near = s_alphaProfile[index];
        var far = s_alphaProfile[index + 1];
        return near + ((far - near) * fraction);
    }

    /// <summary>画像の端から <paramref name="distance"/> DIP 外側の影の色を取得する。</summary>
    /// <param name="distance">画像の端からの距離（DIP）。</param>
    /// <returns>色（<c>0xAARRGGBB</c>。 透過度が影の濃さ）。</returns>
    public static uint ColorAtDistance(double distance)
        => ((uint)Math.Round(Math.Clamp(AlphaAtDistance(distance), 0, 1) * 255) << 24) | (ColorArgb & 0x00FFFFFF);

    /// <summary>画像の矩形から、影を塗る 4 辺の矩形を作る（グラデーションを持つ描画 API 用）。</summary>
    /// <param name="x">画像の左端。</param>
    /// <param name="y">画像の上端。</param>
    /// <param name="width">画像の幅。</param>
    /// <param name="height">画像の高さ。</param>
    /// <returns>4 辺の矩形（画像には掛からない）。</returns>
    /// <remarks>
    /// 上辺・下辺は画像の左右 + <see cref="BlurRadius"/> まで広げて角を埋め、左右の辺は画像の高さいっぱいにする<br/>
    /// （辺どうしを重ねないため、角が二重に濃くならない）。 上辺は影が下へ落ちる分だけ画像へ重ねるが、<br/>
    /// 画像を後から描くため見えない。
    /// </remarks>
    public static IReadOnlyList<ShadowEdgeFill> BuildEdgeRects(double x, double y, double width, double height)
    {
        var fills = new List<ShadowEdgeFill>(4);

        if (width <= 0 || height <= 0)
        {
            return fills;
        }

        var horizontalWidth = width + Insets.Left + Insets.Right;
        foreach (var edge in Edges)
        {
            fills.Add(edge.Side switch
            {
                ShadowSide.Top => new ShadowEdgeFill(
                    x - Insets.Left, y - edge.Extent, horizontalWidth, edge.Extent, edge),
                ShadowSide.Bottom => new ShadowEdgeFill(
                    x - Insets.Left, y + height, horizontalWidth, edge.Extent, edge),
                ShadowSide.Left => new ShadowEdgeFill(
                    x - edge.Extent, y, edge.Extent, height, edge),
                _ => new ShadowEdgeFill(
                    x + width, y, edge.Extent, height, edge),
            });
        }

        return fills;
    }
    /// <summary>1 辺分の計画を生成する。</summary>
    /// <param name="side">辺。</param>
    /// <param name="extent">辺の幅（DIP）。</param>
    /// <param name="extraDistance">濃さを測る距離へ足す値（DIP）。</param>
    /// <param name="outerAtStart">グラデーションの始点が外側かどうか。</param>
    /// <returns>辺の計画。</returns>
    private static ShadowEdgePlan CreateEdge(ShadowSide side, double extent, double extraDistance, bool outerAtStart)
    {
        var stops = new List<ShadowStop>((int)Math.Ceiling(extent) + 1);

        // 外側（距離 = extent）から画像の端（距離 0）へ濃くする
        for (var step = (int)Math.Ceiling(extent); step >= 0; step--)
        {
            var offset = extent > 0 ? 1 - (step / extent) : 1;
            stops.Add(new ShadowStop(offset, ColorAtDistance(step + extraDistance)));
        }

        return new ShadowEdgePlan(side, extent, extraDistance, outerAtStart, stops);
    }
}


namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>画像の 4 辺と 4 隅へ落とす影の描画計画（4 つの UI 版で共通の唯一の定義）。</summary>
/// <remarks>
/// <b>基準</b>: AvaloniaUI 版の <c>BoxShadow</c>（<c>0 0 10 0 #8A000000</c>）を実測して写した値である。<br/>
/// キャプチャページの画像・フォルダビュー・Undo 一覧のサムネイルは、すべてこの計画から描く<br/>
/// （UI 版ごとに影の濃さ・広がりがずれないようにする）。<br/>
/// <br/>
/// <b>4 辺を同じ濃さにする理由</b>: 影を下方向へずらすと上辺だけが薄くなり、画像と影の間に<br/>
/// 白っぽい隙間が見える（実測で指摘）。 4 辺とも画像の端で最も濃く、外側へ向けて同じ割合で薄くする。<br/>
/// <br/>
/// <b>描き方</b>: <see cref="BuildEdgeRects"/> が返す 4 辺の矩形を <see cref="ShadowEdgePlan.Stops"/> の<br/>
/// グラデーションで塗り、<see cref="BuildCornerFills"/> が返す 4 隅を放射グラデーションで塗る<br/>
/// （GDI+・Skia・XAML・Avalonia のいずれでも同じ手順になる）。<br/>
/// <br/>
/// <b>重ねない</b>: 辺どうし・辺と隅は重ならない（重ねると角が二重に濃くなる）。
/// </remarks>
public static class ShadowPlan
{
    /// <summary>影のぼかしの広がり（DIP）。 画像の端からこの距離まで影を落とす。</summary>
    public const double BlurRadius = 10;

    /// <summary>影の色（黒 54%）。</summary>
    /// <remarks>
    /// グラデーションの各停止点は、この色の透過度を<b>実測した濃さ</b>で置き換えて使う<br/>
    /// （<see cref="AlphaAtDistance"/> が返す値がそのまま透過度になる）。
    /// </remarks>
    public const uint ColorArgb = 0x8A000000;

    /// <summary>影の濃さの実測プロファイル（画像の端からの距離 → 不透明度）。</summary>
    /// <remarks>
    /// AvaloniaUI 版の <c>BoxShadow</c>（ぼかし 10 DIP・黒 54%）の明色の画面での実測値。<br/>
    /// 添字 0 は距離 1 DIP、以降 1 DIP 刻みで距離 10 DIP まで（それ以降は 0）。
    /// </remarks>
    private static readonly double[] s_alphaProfile = [0.245, 0.195, 0.150, 0.112, 0.079, 0.050, 0.033, 0.021, 0.012, 0.005];



    /// <summary>4 辺の計画（描画順は上辺・右辺・下辺・左辺）。</summary>
    public static IReadOnlyList<ShadowEdgePlan> Edges { get; } =
    [
        CreateEdge(ShadowSide.Top, outerAtStart: true),
        CreateEdge(ShadowSide.Right, outerAtStart: false),
        CreateEdge(ShadowSide.Bottom, outerAtStart: false),
        CreateEdge(ShadowSide.Left, outerAtStart: true),
    ];

    /// <summary>4 隅の計画（描画順は左上・右上・右下・左下）。</summary>
    /// <remarks>全 UI 版が同じ値を使う。 位置は画像の矩形から <see cref="BuildCornerFills"/> が決める。</remarks>
    public static IReadOnlyList<ShadowStop> CornerStops { get; } = CreateCornerStops();

    /// <summary>画像の周囲へ確保する影の余白を取得する。</summary>
    /// <remarks>アイテムの寸法を決めるときに使う（画像の外側にこの分の余地を確保する）。</remarks>
    public static ShadowInsets Insets => new(BlurRadius, BlurRadius, BlurRadius, BlurRadius);

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
    /// <remarks>
    /// 距離 1 DIP 以下は端の値（最も濃い。 画像のすぐ外側が最も濃く、影として自然に見える）、<br/>
    /// <see cref="BlurRadius"/> 以上は 0 として扱う。
    /// </remarks>
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

    /// <summary>画像の矩形から、影を塗る 4 辺の矩形を作る。</summary>
    /// <param name="x">画像の左端。</param>
    /// <param name="y">画像の上端。</param>
    /// <param name="width">画像の幅。</param>
    /// <param name="height">画像の高さ。</param>
    /// <returns>4 辺の矩形（画像にも隅にも掛からない）。</returns>
    /// <remarks>
    /// 辺は画像の幅・高さいっぱいにして、外側の隅は <see cref="BuildCornerFills"/> が埋める<br/>
    /// （辺どうし・辺と隅を重ねないため、角が二重に濃くならない）。
    /// </remarks>
    public static IReadOnlyList<ShadowEdgeFill> BuildEdgeRects(double x, double y, double width, double height)
    {
        var fills = new List<ShadowEdgeFill>(4);

        if (width <= 0 || height <= 0)
        {
            return fills;
        }

        foreach (var edge in Edges)
        {
            fills.Add(edge.Side switch
            {
                ShadowSide.Top => new ShadowEdgeFill(x, y - edge.Extent, width, edge.Extent, edge),
                ShadowSide.Bottom => new ShadowEdgeFill(x, y + height, width, edge.Extent, edge),
                ShadowSide.Left => new ShadowEdgeFill(x - edge.Extent, y, edge.Extent, height, edge),
                _ => new ShadowEdgeFill(x + width, y, edge.Extent, height, edge),
            });
        }

        return fills;
    }

    /// <summary>画像の矩形から、影を塗る 4 隅の矩形を作る。</summary>
    /// <param name="x">画像の左端。</param>
    /// <param name="y">画像の上端。</param>
    /// <param name="width">画像の幅。</param>
    /// <param name="height">画像の高さ。</param>
    /// <returns>4 隅の矩形（画像にも辺にも掛からない）。</returns>
    /// <remarks>
    /// 隅は辺と同じ幅の正方形にする。 中心は<b>画像の隅の頂点</b>（左上なら矩形の右下）であり、<br/>
    /// 半径は <see cref="BlurRadius"/>（= 一辺）なので、隅の矩形いっぱいに円が収まる。
    /// </remarks>
    public static IReadOnlyList<ShadowCornerFill> BuildCornerFills(double x, double y, double width, double height)
    {
        var fills = new List<ShadowCornerFill>(4);

        if (width <= 0 || height <= 0)
        {
            return fills;
        }

        var size = BlurRadius;
        fills.Add(new ShadowCornerFill(x - size, y - size, size, 1, 1, CornerStops));
        fills.Add(new ShadowCornerFill(x + width, y - size, size, 0, 1, CornerStops));
        fills.Add(new ShadowCornerFill(x + width, y + height, size, 0, 0, CornerStops));
        fills.Add(new ShadowCornerFill(x - size, y + height, size, 1, 0, CornerStops));

        return fills;
    }

    /// <summary>1 辺分の計画を生成する。</summary>
    /// <param name="side">辺。</param>
    /// <param name="outerAtStart">グラデーションの始点が外側かどうか。</param>
    /// <returns>辺の計画。</returns>
    /// <remarks>濃さの変化は 4 辺で共通（<see cref="BlurRadius"/> の幅で端から外側へ薄くなる）。</remarks>
    private static ShadowEdgePlan CreateEdge(ShadowSide side, bool outerAtStart)
    {
        var steps = (int)Math.Ceiling(BlurRadius);
        var stops = new List<ShadowStop>(steps + 1);

        // 外側（距離 = BlurRadius）から画像の端（距離 0）へ濃くする
        for (var step = steps; step >= 0; step--)
        {
            stops.Add(new ShadowStop(1 - (step / BlurRadius), ColorAtDistance(step)));
        }

        return new ShadowEdgePlan(side, BlurRadius, outerAtStart, stops);
    }

    /// <summary>隅の放射グラデーションの停止点を生成する。</summary>
    /// <returns>停止点（中心からの距離の順）。</returns>
    /// <remarks>位置は「距離 ÷ <see cref="BlurRadius"/>」であり、辺の濃さの変化と同じ値になる。</remarks>
    private static IReadOnlyList<ShadowStop> CreateCornerStops()
    {
        var steps = (int)Math.Ceiling(BlurRadius);
        var stops = new List<ShadowStop>(steps + 1);

        for (var step = 0; step <= steps; step++)
        {
            stops.Add(new ShadowStop(step / BlurRadius, ColorAtDistance(step)));
        }

        return stops;
    }
}
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
/// <b>描き方</b>: <see cref="BuildDeviceBands"/> が返す<b>1 画素の帯</b>を単色で塗るだけである<br/>
/// （グラデーション ブラシの端の扱いに依存しないため、どの UI 版でも同じ結果になる）。<br/>
/// <b>4 隅もグラデーションを使わない</b>: 辺の帯を隅の正方形まで伸ばし、横の帯と縦の帯を重ねて<br/>
/// <b>合成</b>する（重なった画素は 1-(1-横)(1-縦) の濃さになる）。 放射グラデーションは描画 API ごとに<br/>
/// 半径・中心の解釈が揺れ、隅だけ濃い点や薄い筋として見えた（実測）。
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

    /// <summary>4 隅の放射グラデーションの停止点（要素を並べる一覧だけが使う）。</summary>
    /// <remarks>
    /// キャプチャページの画像と Skia / GDI+ の一覧は<b>帯の重ね合わせ</b>（<see cref="BuildDeviceBands"/>）で隅を塗る。<br/>
    /// 本停止点は、隅を 1 つの要素で塗る一覧（WinUI 3 の <c>ThumbnailTileView</c>）が使う。
    /// </remarks>
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

    /// <summary>画像の矩形から、影を塗る 1 画素幅の帯（4 辺）を作る。</summary>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率（DPI の倍率）。</param>
    /// <returns>帯の一覧（上辺・右辺・下辺・左辺の順。 各辺は画像に接する画素から外側へ）。</returns>
    /// <remarks>
    /// <b>これは「描画計画」の画素版である</b>: 辺の濃さを 1 画素ごとの単色へ落としているため、<br/>
    /// どの UI 版も「単色の矩形を塗る」だけで同じ結果になる（グラデーション ブラシの端の扱いに依存しない）。<br/>
    /// 濃さは画素の中心の距離で決める（<see cref="AlphaAtDistance"/>）。<br/>
    /// <b>画像の下へ潜らせる</b>: 内側の端は切り捨てるため、帯は画像の端の画素（画像と背景の混色）に<br/>
    /// 最大 1 画素掛かる。 影を画像より先に描けば隠れ、混色の下地が影になるため白い線が出ない。<br/>
    /// <b>4 隅も同じ帯で塗る</b>: 辺の帯を隅の正方形まで伸ばし、横の帯と縦の帯を重ねて合成する<br/>
    /// （放射グラデーションは描画 API ごとに半径・中心の解釈が揺れ、隅だけ濃い点や薄い筋として見えた。 実測）。
    /// </remarks>
    public static IReadOnlyList<ShadowBand> BuildDeviceBands(double x, double y, double width, double height, double scaling)
    {
        var bands = new List<ShadowBand>();

        if (width <= 0 || height <= 0 || scaling <= 0)
        {
            return bands;
        }

        var extent = (int)Math.Round(BlurRadius * scaling);

        // 画像の端は<b>切り捨て</b>る: 画像は端数のある位置へ描かれ、その最後の画素は画像と背景の<br/>
        // 混色になる。 影の内側を切り捨てて画像の下へ潜らせると、その混色の下地が影になり<br/>
        // 白い線が見えない（切り上げると 1 画素だけ背景が残る。 実測）。
        var left = (int)Math.Floor(x * scaling);
        var top = (int)Math.Floor(y * scaling);
        var right = (int)Math.Floor((x + width) * scaling);
        var bottom = (int)Math.Floor((y + height) * scaling);
        var pixelsWide = right - left;
        var pixelsHigh = bottom - top;
        if (extent <= 0 || pixelsWide <= 0 || pixelsHigh <= 0)
        {
            return bands;
        }

        // 帯は隅の正方形まで伸ばす（横の帯と縦の帯が隅で重なって合成され、隅の濃さになる）
        var spanX = pixelsWide + (2 * extent);
        var spanY = pixelsHigh + (2 * extent);
        for (var i = 0; i < extent; i++)
        {
            // i = 0 が画像に接する画素。 画素の中心までの距離で濃さを決める（勾配の見た目と同じ）
            var argb = ColorAtDistance((i + 0.5) / scaling);
            bands.Add(new ShadowBand(left - extent, top - 1 - i, spanX, 1, argb));
            bands.Add(new ShadowBand(left - extent, bottom + i, spanX, 1, argb));
            bands.Add(new ShadowBand(left - 1 - i, top - extent, 1, spanY, argb));
            bands.Add(new ShadowBand(right + i, top - extent, 1, spanY, argb));
        }

        // 画像の端の画素（切り捨てた行・列）も覆う: 画像の描画側は端の画素を切り捨てたり
        // 切り上げたりし、floor の行・列が画像でも影でも塗られず<b>背景（白）が残る</b>ことがある（実測:
        // 拡大率によって画像の上辺に 1 画素の白い線）。 画像がその画素を塗る場合は画像に隠れる。
        var edgeArgb = ColorAtDistance(0.5 / scaling);
        bands.Add(new ShadowBand(left - extent, top, spanX, 1, edgeArgb));
        bands.Add(new ShadowBand(left - extent, bottom - 1, spanX, 1, edgeArgb));
        bands.Add(new ShadowBand(left, top - extent, 1, spanY, edgeArgb));
        bands.Add(new ShadowBand(right - 1, top - extent, 1, spanY, edgeArgb));

        return bands;
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
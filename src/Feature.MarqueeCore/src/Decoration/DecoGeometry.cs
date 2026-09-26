using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>装飾（選択矩形の外側・内側）の幾何計算（純粋関数）。</summary>
/// <remarks>
/// 暗転領域（穴あけ矩形を除いた 4 帯）と枠線の 4 矩形の計算を単一所有する。<br/>
/// 選択矩形の外側を描く <see cref="OuterDecoFrame"/> と内側を描く <see cref="InnerDecoFrame"/> が共用するため、<br/>
/// 「枠線はどの 4 矩形か」「どの太さを下限とするか」の規則が 2 箇所へ分かれない。<br/>
/// 穴あけ矩形の決定と論理座標から DIP への変換はフレーム全体の関心事のため <c>Drawing.MarqueeFrameGeometry</c> が持つ。<br/>
/// 各 UI 実装は返された <see cref="EsRect"/> を自身の描画 API へ渡すだけにする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// <b>可視性</b>: 描画内容の判断は各フレームの解決が担うため、本クラスは内部実装とする
/// （UI 実装は返されたフレームを描くだけで、幾何計算を直接呼ばない）。
/// </remarks>
internal static class DecoGeometry
{
    /// <summary>枠線の太さの下限（DIP。0 以下の指定を無効化する）。</summary>
    private const double BorderThicknessLowerBound = 0.5;

    /// <summary>選択矩形の四辺に描く枠線の矩形を計算する。</summary>
    /// <param name="rect">選択矩形（DIP 座標）。</param>
    /// <param name="thickness">枠線の太さ（DIP）。0.5 未満は 0.5 に正規化される。</param>
    /// <returns>上・下・左・右の順の枠線矩形（4 個）。</returns>
    /// <remarks>
    /// 辺の矩形の計算は <see cref="EsRect.ComputeInnerEdges(double)"/> が単一所有する（ルーペの外枠と同じ式）。<br/>
    /// 太さの下限のみ本メソッドが決める（選択枠は細い線でも視認できる必要がある）。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeBorderRects(EsRect rect, double thickness)
        => rect.ComputeInnerEdges(Math.Max(BorderThicknessLowerBound, thickness));

    /// <summary>暗転オーバーレイを描画するかどうかを判定する。</summary>
    /// <param name="holeRect">穴あけ矩形（DIP）。null の場合は全面暗転を検討する。</param>
    /// <param name="showFullOverlay">選択なしでも全面暗転を表示するか。</param>
    /// <returns>暗転オーバーレイを描画する場合は true。</returns>
    /// <remarks>
    /// 穴あけ矩形がある場合、または全面暗転表示が有効な場合は描画する。<br/>
    /// 穴あけ矩形がある場合の暗転は「クライアント矩形 − 穴あけ矩形」の 4 領域で表現する（穴あけ矩形を除いた帯状の矩形）。
    /// </remarks>
    public static bool ShouldDrawOverlay(EsRect? holeRect, bool showFullOverlay)
        => holeRect.HasValue || showFullOverlay;

    /// <summary>描画領域から穴あけ矩形を除いた暗転領域（4 帯）を計算する。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。null の場合は全面（1 領域）。</param>
    /// <returns>暗転して塗る矩形の一覧（上・下・左・右の順）。幅・高さが 0 以下の領域は含めない。</returns>
    /// <remarks>
    /// 暗転領域は描画先の原点（0, 0）を基準に計算する（描画 API のコンテキスト原点は常にコントロールの左上であるため、<br/>
    /// 親座標を混ぜるとマスクが原点分ずれる。原点を持たない型で受けることで取り違えを防ぐ）。<br/>
    /// 穴あけ矩形が完全に描画領域の外の場合は全面を返す（描画漏れを防ぐ）。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeOverlayRegions(EsSize clientSize, EsRect? hole)
    {
        var full = new EsRect(0, 0, clientSize.Width, clientSize.Height);

        if (hole is not { } h)
        {
            return [full];
        }

        // 穴あけ矩形を描画領域内へクランプする（領域外の部分は暗転しない）
        var left = Math.Clamp(h.X, 0, clientSize.Width);
        var top = Math.Clamp(h.Y, 0, clientSize.Height);
        var right = Math.Clamp(h.Right, 0, clientSize.Width);
        var bottom = Math.Clamp(h.Bottom, 0, clientSize.Height);

        if (right <= left || bottom <= top)
        {
            // 穴あけ矩形が描画領域と交差しない: 全面を暗転する
            return [full];
        }

        var regions = new List<EsRect>(4);
        AddRegion(regions, new EsRect(0, 0, clientSize.Width, top));                                                 // 上
        AddRegion(regions, new EsRect(0, bottom, clientSize.Width, clientSize.Height - bottom));                      // 下
        AddRegion(regions, new EsRect(0, top, left, bottom - top));                                                   // 左
        AddRegion(regions, new EsRect(right, top, clientSize.Width - right, bottom - top));                           // 右
        return regions;
    }

    /// <summary>幅・高さが正の矩形のみを追加する。</summary>
    /// <param name="regions">追加先のリスト。</param>
    /// <param name="rect">追加する矩形。</param>
    private static void AddRegion(List<EsRect> regions, EsRect rect)
    {
        if (rect.Width > 0 && rect.Height > 0)
        {
            regions.Add(rect);
        }
    }
}

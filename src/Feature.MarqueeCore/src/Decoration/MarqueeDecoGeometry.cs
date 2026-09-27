using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Drawing;

namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>装飾（選択矩形の外側・内側）の幾何計算（純粋関数）。</summary>
/// <remarks>
/// 暗転領域（穴あけ矩形を除いた 4 帯）と枠線の 4 矩形の計算を単一所有する。<br/>
/// 選択矩形の外側を描く <see cref="MarqueeOuterDecoFrame"/> と内側を描く <see cref="MarqueeInnerDecoFrame"/> が共用するため、<br/>
/// 「枠線はどの 4 矩形か」「どの太さを下限とするか」の規則が 2 箇所へ分かれない。<br/>
/// 穴あけ矩形の決定と論理座標から DIP への変換はフレーム全体の関心事のため <see cref="MarqueeRenderRequest"/> が持つ。<br/>
/// 各 UI 実装は返された <see cref="EsRect"/> を自身の描画 API へ渡すだけにする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// <b>可視性</b>: 描画内容の判断は各フレームの解決が担うため、本クラスは内部実装とする
/// （UI 実装は返されたフレームを描くだけで、幾何計算を直接呼ばない）。
/// </remarks>
internal static class MarqueeDecoGeometry
{
    /// <summary>選択矩形の四辺に描く枠線の矩形を計算する。</summary>
    /// <param name="rect">選択矩形（DIP 座標）。</param>
    /// <param name="thickness">枠線の太さ（DIP）。補正済みの値を渡す（安全域は <see cref="Marquee.Limits"/> が単一所有する）。</param>
    /// <returns>上・下・左・右の順の枠線矩形（4 個）。</returns>
    /// <remarks>
    /// 辺の矩形の計算は <see cref="EsRect.ComputeInnerEdges(double)"/> が単一所有する（ルーペの枠線と同じ式）。<br/>
    /// 太さは面の短辺で上限を取る（面より太い枠線は面全体を覆い、矩形が面からはみ出さない）。<br/>
    /// 太さの補正は装飾の外観（<c>MarqueeOuterDecoAppearance</c> / <c>MarqueeInnerDecoAppearance</c>）の生成時に適用される。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeBorderRects(EsRect rect, double thickness)
        => rect.ComputeInnerEdges(Math.Min(thickness, Math.Min(rect.Width, rect.Height)));

    /// <summary>描画面から穴あけ矩形を除いた暗転領域（4 帯）を計算する。</summary>
    /// <param name="surface">描画面の全体（DIP。左上を原点とする矩形）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。null の場合は全面（1 領域）。</param>
    /// <returns>暗転して塗る矩形の一覧（上・下・左・右の順）。幅・高さが 0 以下の領域は含めない。</returns>
    /// <remarks>
    /// 暗転領域は描画面（<paramref name="surface"/>）を基準に計算する（描画 API のコンテキスト原点は常にコントロールの左上であるため、<br/>
    /// 親座標を混ぜるとマスクが原点分ずれる）。<br/>
    /// 穴あけ矩形が完全に描画面の外の場合は全面を返す（描画漏れを防ぐ）。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeOverlayRegions(EsRect surface, EsRect? hole)
    {
        if (hole is not { } h)
        {
            return [surface];
        }

        // 穴あけ矩形を描画面内へクランプする（領域外の部分は暗転しない）
        var left = Math.Clamp(h.X, surface.X, surface.Right);
        var top = Math.Clamp(h.Y, surface.Y, surface.Bottom);
        var right = Math.Clamp(h.Right, surface.X, surface.Right);
        var bottom = Math.Clamp(h.Bottom, surface.Y, surface.Bottom);

        if (right <= left || bottom <= top)
        {
            // 穴あけ矩形が描画面と交差しない: 全面を暗転する
            return [surface];
        }

        var regions = new List<EsRect>(4);
        AddRegion(regions, new EsRect(surface.X, surface.Y, surface.Width, top - surface.Y));                       // 上
        AddRegion(regions, new EsRect(surface.X, bottom, surface.Width, surface.Bottom - bottom));                   // 下
        AddRegion(regions, new EsRect(surface.X, top, left - surface.X, bottom - top));                             // 左
        AddRegion(regions, new EsRect(right, top, surface.Right - right, bottom - top));                            // 右
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

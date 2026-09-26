using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore.Placement;

/// <summary>ルーペの配置の幾何計算（表示位置の決定）を単一所有する。</summary>
/// <remarks>
/// <b>本クラスが持つもの</b>: 拡大中心と表示位置のオフセットからルーペの左上座標を求め、<br/>
/// 親領域の内側へクランプする規則である（表示サイズは呼び出し側がクランプ済みの値を渡す）。<br/>
/// <br/>
/// <b>利用者</b>: 公開ファサード <c>Loupe.ResolveLayout</c> だけである（本クラスは内部手順であり、<br/>
/// 利用者（UI 実装・ホスト）が直接呼ぶ入口にはしない）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupePlacementGeometry
{
    /// <summary>ルーペの表示位置（左上）を計算する（オフセット加算・親領域内へのクランプ）。</summary>
    /// <param name="displaySize">ルーペの表示サイズ（DIP・一辺。クランプ済みの値を渡す）。</param>
    /// <param name="parentSize">親領域のサイズ（DIP）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親領域 DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示位置（左上座標）。</returns>
    /// <remarks>
    /// 表示位置は中心（DIP）+ オフセットとし、親領域サイズ内にクランプする。<br/>
    /// 親領域のサイズが未確定（幅または高さが 0 以下 = レイアウト前・親未接続）の場合はクランプしない<br/>
    /// （「親サイズが確定しているか」の判定は <see cref="EsSize.HasArea"/>、クランプは<br/>
    /// <see cref="EsSize.ClampOffset(double, double, EsAxis)"/> が単一所有する）。<br/>
    /// 表示サイズはクランプ済みの値を渡す（要素のサイズと位置のクランプで同じ値を使い、<br/>
    /// 別々にクランプすると丸めの違いで要素が親領域からはみ出しうる）。
    /// </remarks>
    internal static EsPoint ComputePosition(double displaySize, EsSize parentSize, EsPoint centerDip, double offset)
    {
        double mx = centerDip.X + offset;
        double my = centerDip.Y + offset;
        if (!parentSize.HasArea)
        {
            return new EsPoint(mx, my);
        }

        return new EsPoint(
            parentSize.ClampOffset(mx, displaySize, EsAxis.X),
            parentSize.ClampOffset(my, displaySize, EsAxis.Y));
    }
}

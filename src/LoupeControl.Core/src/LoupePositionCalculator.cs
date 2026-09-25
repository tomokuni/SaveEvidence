using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace Estable.Loupe;

/// <summary>ルーペの表示位置・サイズ計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 表示サイズの親領域へのクランプと、表示位置（中心 + オフセット、親領域内へのクランプ）を計算する。<br/>
/// 座標変換（<c>ICoordTransform.Inverse</c>）は呼び出し側で行い、本クラスは変換後（DIP）の座標を受け取る。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// <b>外部公開しない</b>: 利用者（UI 実装・ホスト）はファサード <see cref="Loupe.ResolveLayout"/> を使うため、
/// 配置の内部手順は本アセンブリと単体テストに限定する。
/// </remarks>
internal static class LoupePositionCalculator
{
    /// <summary>表示サイズ（DIP）から描画に使う物理ピクセルサイズを求める。</summary>
    /// <param name="size">表示サイズ（DIP・一辺）。</param>
    /// <param name="renderScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="availableSize">描画先の領域サイズ。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画に使う一辺の物理ピクセルサイズ（切り上げ・最低 1、領域指定時はクランプ済み）。</returns>
    /// <remarks>
    /// 「表示サイズを物理ピクセルへ換算し、描画先より大きい場合は描画先に合わせる」手順を単一所有する。<br/>
    /// 換算は <see cref="DpiScaler.ScaleToPixel(double, double)"/>、クランプは <see cref="EsSizeMath.ClampToArea"/> へ委譲する。<br/>
    /// 描画と描画引数（イベント通知）が同じ値を使うため、UI 層は本メソッドの結果を両方へ渡す<br/>
    /// （それぞれの計算で値がずれると、ホストへ通知する切り出し領域と実際の描画が食い違う）。
    /// </remarks>
    public static int ResolvePixelSize(double size, double renderScaling, EsSize? availableSize = null)
        => (int)EsSizeMath.ClampToArea(
            DpiScaler.ScaleToPixel(size, renderScaling),
            availableSize ?? default);

    /// <summary>ルーペの表示位置（左上）を計算する（オフセット加算・親領域内へのクランプ）。</summary>
    /// <param name="displaySize">ルーペの表示サイズ（DIP・一辺。クランプ済みの値を渡す）。</param>
    /// <param name="parentSize">親領域のサイズ（DIP）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親領域 DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示位置（左上座標）。</returns>
    /// <remarks>
    /// 表示位置は中心（DIP）+ オフセットとし、親領域サイズ内にクランプする。<br/>
    /// 親領域のサイズが未確定（幅または高さが 0 以下 = レイアウト前・親未接続）の場合はクランプしない<br/>
    /// （「親サイズが確定しているか」の判定は <see cref="EsSizeMath.HasArea"/>、クランプは<br/>
    /// <see cref="EsSizeMath.ClampOffsetToArea"/> が単一所有する）。<br/>
    /// 表示サイズはクランプ済みの値を渡す（要素のサイズと位置のクランプで同じ値を使い、<br/>
    /// 別々にクランプすると丸めの違いで要素が親領域からはみ出しうる）。
    /// </remarks>
    public static EsPoint ComputePosition(double displaySize, EsSize parentSize, EsPoint centerDip, double offset)
    {
        double mx = centerDip.X + offset;
        double my = centerDip.Y + offset;
        if (!EsSizeMath.HasArea(parentSize))
        {
            return new EsPoint(mx, my);
        }

        return new EsPoint(
            EsSizeMath.ClampOffsetToArea(mx, parentSize.Width, displaySize),
            EsSizeMath.ClampOffsetToArea(my, parentSize.Height, displaySize));
    }
}

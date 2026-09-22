using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace Estable.Loupe;

/// <summary>ルーペの表示位置・サイズ計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 表示サイズの親領域へのクランプと、表示位置（中心 + オフセット、親領域内へのクランプ）を計算する。<br/>
/// 座標変換（<c>ICoordTransform.Inverse</c>）は呼び出し側で行い、本クラスは変換後（DIP）の座標を受け取る。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// </remarks>
internal static class LoupePositionCalculator
{
    /// <summary>サイズを提示領域の最小辺へクランプする（領域サイズが未確定の場合はクランプしない）。</summary>
    /// <param name="size">クランプするサイズ（正の値）。</param>
    /// <param name="areaSize">提示領域のサイズ。幅または高さが 0 以下の場合は未確定として扱う。</param>
    /// <returns>クランプ後のサイズ。領域が未確定の場合は <paramref name="size"/> のまま。</returns>
    /// <remarks>
    /// 「領域より大きい場合は領域に合わせる」規則を単一所有する。<br/>
    /// 単位は呼び出し側が揃える（表示サイズのクランプは DIP、描画先が確定している場合は物理ピクセル）。
    /// </remarks>
    public static double ClampSizeToArea(double size, EsSize areaSize)
        => HasArea(areaSize) ? Math.Min(size, Math.Min(areaSize.Width, areaSize.Height)) : size;

    /// <summary>表示サイズ（DIP）から描画に使う物理ピクセルサイズを求める。</summary>
    /// <param name="size">表示サイズ（DIP・一辺）。</param>
    /// <param name="renderScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="availableSize">描画先の領域サイズ。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画に使う一辺の物理ピクセルサイズ（切り上げ・最低 1、領域指定時はクランプ済み）。</returns>
    /// <remarks>
    /// 「表示サイズを物理ピクセルへ換算し、描画先より大きい場合は描画先に合わせる」規則を単一所有する。<br/>
    /// 描画と描画引数（イベント通知）が同じ値を使うため、UI 層は本メソッドの結果を両方へ渡す<br/>
    /// （それぞれの計算で値がずれると、ホストへ通知する切り出し領域と実際の描画が食い違う）。
    /// </remarks>
    public static int ResolvePixelSize(double size, double renderScaling, EsSize? availableSize = null)
        => (int)ClampSizeToArea(
            DpiScaler.ScaleToPixel(size, renderScaling),
            availableSize ?? default);

    /// <summary>提示領域のサイズが確定しているか（正の幅・高さを持つか）を判定する。</summary>
    /// <param name="areaSize">提示領域のサイズ。</param>
    /// <returns>確定している場合は true。</returns>
    private static bool HasArea(EsSize areaSize) => areaSize.Width > 0 && areaSize.Height > 0;

    /// <summary>ルーペの表示位置（左上）を計算する（オフセット加算・親領域内へのクランプ）。</summary>
    /// <param name="displaySize">ルーペの表示サイズ（DIP・一辺。クランプ済みの値を渡す）。</param>
    /// <param name="parentSize">親領域のサイズ（DIP）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親領域 DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示位置（左上座標）。</returns>
    /// <remarks>
    /// 表示位置は中心（DIP）+ オフセットとし、親領域サイズ内にクランプする。<br/>
    /// 親領域のサイズが未確定（幅または高さが 0 以下 = レイアウト前・親未接続）の場合はクランプしない<br/>
    /// （「親サイズが確定しているか」の判断は本メソッドが単一所有する）。<br/>
    /// 表示サイズは <see cref="ClampSizeToArea"/> の結果を渡す（要素のサイズと位置のクランプで同じ値を使い、<br/>
    /// 別々にクランプすると丸めの違いで要素が親領域からはみ出しうる）。
    /// </remarks>
    public static EsPoint ComputePosition(double displaySize, EsSize parentSize, EsPoint centerDip, double offset)
    {
        double mx = centerDip.X + offset;
        double my = centerDip.Y + offset;
        if (!HasArea(parentSize))
        {
            return new EsPoint(mx, my);
        }

        return new EsPoint(
            Math.Clamp(mx, 0, Math.Max(0, parentSize.Width - displaySize)),
            Math.Clamp(my, 0, Math.Max(0, parentSize.Height - displaySize)));
    }

    /// <summary>ルーペの表示サイズ・位置を計算する（親領域サイズへのクランプ・オフセット・範囲クランプ）。</summary>
    /// <param name="requestedSize">基本サイズ（DIP）。</param>
    /// <param name="parentSize">親領域のサイズ（DIP）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親領域 DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示サイズ（正方形）と表示位置（左上座標）。</returns>
    /// <remarks>
    /// <see cref="ClampSizeToArea"/> と <see cref="ComputePosition"/> を合成する（サイズだけが必要な場合は<br/>
    /// 両者を個別に呼ぶ。表示サイズを要素へ先に反映する必要がある UI はそちらを使う）。
    /// </remarks>
    public static (EsSize Size, EsPoint Position) Compute(
        double requestedSize, EsSize parentSize, EsPoint centerDip, double offset)
    {
        var size = ClampSizeToArea(requestedSize, parentSize);

        return (new EsSize(size, size), ComputePosition(size, parentSize, centerDip, offset));
    }
}

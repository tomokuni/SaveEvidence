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
    /// <summary>ルーペの表示サイズ・位置を計算する（親領域サイズへのクランプ・オフセット・範囲クランプ）。</summary>
    /// <param name="requestedSize">基本サイズ（DIP）。</param>
    /// <param name="parentSize">親領域のサイズ（DIP）。</param>
    /// <param name="hasParentSize">親領域のサイズが確定しているか（正の幅・高さを持つか）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親領域 DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示サイズ（正方形）と表示位置（左上座標）。</returns>
    /// <remarks>
    /// 表示サイズは基本サイズを親領域の最小辺にクランプする。<br/>
    /// 表示位置は中心（DIP）+ オフセットとし、親領域サイズ内にクランプする。
    /// </remarks>
    public static (EsSize Size, EsPoint Position) Compute(
        double requestedSize, EsSize parentSize, bool hasParentSize,
        EsPoint centerDip, double offset)
    {
        double size = requestedSize;
        if (hasParentSize)
        {
            size = Math.Min(size, Math.Min(parentSize.Width, parentSize.Height));
        }

        double mx = centerDip.X + offset;
        double my = centerDip.Y + offset;
        if (hasParentSize)
        {
            mx = Math.Clamp(mx, 0, Math.Max(0, parentSize.Width - size));
            my = Math.Clamp(my, 0, Math.Max(0, parentSize.Height - size));
        }

        return (new EsSize(size, size), new EsPoint(mx, my));
    }
}

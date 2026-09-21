using Estable.Platform.Geometry;
using RectInt = System.Drawing.Rectangle;

namespace SaveEvidence.MewUI.Services;

/// <summary>キャプチャ画像の切り出し範囲（ドメイン座標）を画像内のピクセル矩形へ変換する。</summary>
/// <remarks>
/// キャプチャの選択矩形は画像ピクセル座標（ドメイン座標）で得られるため、四捨五入して整数の矩形へ直し、<br/>
/// 画像の範囲内へクランプする。<br/>
/// <b>空の判定</b>: 幅・高さが 1 ピクセル未満、または画像と重ならない場合は <see cref="RectInt.Empty"/> を返す<br/>
/// （呼び出し側は空をキャンセルとして扱い、0 サイズの画像を生成しない）。<br/>
/// <b>純粋関数</b>: UI ホストを必要としないため、境界値（負の座標・画像外・端数の丸め）をヘッドレスで検証できる。
/// </remarks>
internal static class CaptureSelectionMath
{
    /// <summary>選択矩形を画像内のピクセル矩形へ変換する。</summary>
    /// <param name="selection">選択矩形（画像ピクセル座標）。</param>
    /// <param name="imageWidth">画像の幅（ピクセル）。0 以下の場合は空を返す。</param>
    /// <param name="imageHeight">画像の高さ（ピクセル）。0 以下の場合は空を返す。</param>
    /// <returns>画像内に収めた整数の矩形。切り出せる範囲が無い場合は <see cref="RectInt.Empty"/>。</returns>
    /// <remarks>
    /// 1. 画像が空なら空を返す。<br/>
    /// 2. 左右・上下をそれぞれ四捨五入（0.5 は切り上げ）して整数へ直し、画像の範囲へクランプする。<br/>
    /// 3. 幅・高さが 1 ピクセル未満なら空を返す。
    /// </remarks>
    public static RectInt ToPixelRect(EsRect selection, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return RectInt.Empty;
        }

        var left = Math.Clamp(Round(selection.X), 0, imageWidth);
        var top = Math.Clamp(Round(selection.Y), 0, imageHeight);
        var right = Math.Clamp(Round(selection.Right), 0, imageWidth);
        var bottom = Math.Clamp(Round(selection.Bottom), 0, imageHeight);

        if (right - left < 1 || bottom - top < 1)
        {
            return RectInt.Empty;
        }

        return new RectInt(left, top, right - left, bottom - top);
    }

    /// <summary>半開区間の矩形を閉区間（始点・終点のピクセルを含む）へ広げる。</summary>
    /// <param name="rect">切り出し矩形（半開区間。幅 = 終点 − 始点）。</param>
    /// <param name="imageWidth">画像の幅（ピクセル）。</param>
    /// <param name="imageHeight">画像の高さ（ピクセル）。</param>
    /// <returns>終点の 1 ピクセルを加えた矩形（画像の外へはみ出さないようクランプする）。</returns>
    /// <remarks>
    /// ドラッグ選択の矩形は半開区間（幅 = 終点 − 始点）だが、アプリの仕様は「始点・終点のピクセルを切り出し対象に含める（閉区間）」である。<br/>
    /// そのため終点の 1 ピクセルを加える（Avalonia 版の <c>ToClosedIntervalPixelRect</c> と同じ規則）。<br/>
    /// 画像の右端・下端では画像サイズを超えないようにクランプする。
    /// </remarks>
    public static RectInt ToClosedInterval(RectInt rect, int imageWidth, int imageHeight)
        => new(
            rect.X,
            rect.Y,
            Math.Min(rect.Width + 1, Math.Max(0, imageWidth - rect.X)),
            Math.Min(rect.Height + 1, Math.Max(0, imageHeight - rect.Y)));

    /// <summary>座標を四捨五入して整数へ直す。</summary>
    /// <param name="value">対象の座標。</param>
    /// <returns>四捨五入した値（0.5 は切り上げ）。</returns>
    /// <remarks>
    /// <see cref="Math.Round(double)"/> の既定は銀行丸め（0.5 を偶数へ）で直感に反するため、<br/>
    /// ピクセル座標の慣例に合わせて 0.5 は切り上げる（<see cref="MidpointRounding.AwayFromZero"/>）。
    /// </remarks>
    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

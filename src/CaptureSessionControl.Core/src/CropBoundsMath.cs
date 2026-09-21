using EsUtil.Platform.Core.Geometry;

namespace Estable.CaptureSession;

/// <summary>切り出し範囲（選択矩形 → 画像内のピクセル矩形）の計算を単一所有する。</summary>
/// <remarks>
/// Avalonia 版（<c>CropSession</c>）と MewUI 版（<c>CaptureOverlayWindow</c> / <c>PreviewCropSession</c>）が<br/>
/// 同じ規則を使うため、計算をコアへ集約する（規則の二重実装を防ぐ）。<br/>
/// <br/>
/// <b>規則</b>:<br/>
/// 1. 画像サイズが 0 以下なら空を返す。<br/>
/// 2. 選択矩形が最小サイズ未満なら空を返す（画像内へクランプする前の値で判定する）。<br/>
/// 3. 座標は四捨五入（0.5 は切り上げ）し、画像の範囲へクランプする。<br/>
/// 4. 閉区間（始点・終点のピクセルを含む）の場合は終点の 1 ピクセルを加える（画像の端でクランプする）。<br/>
/// <br/>
/// <b>純粋関数</b>: UI ホストを必要としないため、境界値（負の座標・画像外・端数の丸め）をヘッドレスで検証できる。
/// </remarks>
public static class CropBoundsMath
{
    /// <summary>選択矩形から切り出し範囲を求める。</summary>
    /// <param name="selection">選択矩形（画像ピクセル座標・半開区間）。</param>
    /// <param name="imageSize">画像のピクセルサイズ。</param>
    /// <param name="minSelectionSize">選択矩形の最小サイズ（画像ピクセル）。これ未満は空を返す。</param>
    /// <param name="closedInterval">true の場合は閉区間（終点の 1 ピクセルを含む）。false の場合は半開区間のまま。</param>
    /// <returns>切り出し範囲。切り出せない場合は <c>default</c>（<see cref="EsPixelRect.IsEmpty"/> が true）。</returns>
    /// <remarks>
    /// ドラッグ選択の矩形は半開区間（幅 = 終点 − 始点）だが、アプリの仕様は「始点・終点のピクセルを<br/>
    /// 切り出し対象に含める（閉区間）」である。一方、スクリーン選択・ウィンドウ選択のハイライト矩形は<br/>
    /// 検出結果が全ピクセルを含むため加算しない（<paramref name="closedInterval"/> = false）。<br/>
    /// 最小サイズの判定は画像内へクランプする前の値で行う（画像外へ出た選択を「大きい」と誤判定しない）。
    /// </remarks>
    public static EsPixelRect ComputeCropBounds(
        EsRect selection, EsSize imageSize, double minSelectionSize, bool closedInterval = true)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return default;
        }

        if (selection.Width < minSelectionSize || selection.Height < minSelectionSize)
        {
            return default;
        }

        var rect = ToPixelRect(selection, imageSize);
        if (rect.IsEmpty)
        {
            return default;
        }

        return closedInterval ? ToClosedInterval(rect, imageSize) : rect;
    }

    /// <summary>選択矩形（半開区間）を画像内の整数矩形へ変換する。</summary>
    /// <param name="selection">選択矩形（画像ピクセル座標・半開区間）。</param>
    /// <param name="imageSize">画像のピクセルサイズ。</param>
    /// <returns>画像内に収めた矩形。切り出せる範囲が無い場合は <c>default</c>。</returns>
    /// <remarks>
    /// 1. 画像が空なら空を返す。<br/>
    /// 2. 左右・上下をそれぞれ四捨五入（0.5 は切り上げ）して整数へ直し、画像の範囲へクランプする。<br/>
    /// 3. 幅・高さが 1 ピクセル未満なら空を返す。
    /// </remarks>
    public static EsPixelRect ToPixelRect(EsRect selection, EsSize imageSize)
    {
        var imageWidth = (int)imageSize.Width;
        var imageHeight = (int)imageSize.Height;
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return default;
        }

        var left = Math.Clamp(Round(selection.X), 0, imageWidth);
        var top = Math.Clamp(Round(selection.Y), 0, imageHeight);
        var right = Math.Clamp(Round(selection.Right), 0, imageWidth);
        var bottom = Math.Clamp(Round(selection.Bottom), 0, imageHeight);

        if (right - left < 1 || bottom - top < 1)
        {
            return default;
        }

        return new EsPixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>半開区間の矩形を閉区間（始点・終点のピクセルを含む）へ広げる。</summary>
    /// <param name="rect">切り出し矩形（半開区間。幅 = 終点 − 始点）。</param>
    /// <param name="imageSize">画像のピクセルサイズ。</param>
    /// <returns>終点の 1 ピクセルを加えた矩形（画像の外へはみ出さないようクランプする）。</returns>
    /// <remarks>画像の右端・下端では画像サイズを超えないようにクランプする。</remarks>
    public static EsPixelRect ToClosedInterval(EsPixelRect rect, EsSize imageSize)
    {
        var imageWidth = (int)imageSize.Width;
        var imageHeight = (int)imageSize.Height;
        return new EsPixelRect(
            rect.X,
            rect.Y,
            Math.Min(rect.Width + 1, Math.Max(0, imageWidth - rect.X)),
            Math.Min(rect.Height + 1, Math.Max(0, imageHeight - rect.Y)));
    }

    /// <summary>座標を四捨五入して整数へ直す。</summary>
    /// <param name="value">対象の座標。</param>
    /// <returns>四捨五入した値（0.5 は切り上げ）。</returns>
    /// <remarks>
    /// <see cref="Math.Round(double)"/> の既定は銀行丸め（0.5 を偶数へ）で直感に反するため、<br/>
    /// ピクセル座標の慣例に合わせて 0.5 は切り上げる（<see cref="MidpointRounding.AwayFromZero"/>）。
    /// </remarks>
    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

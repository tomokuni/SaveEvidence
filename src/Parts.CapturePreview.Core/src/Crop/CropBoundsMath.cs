using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;

/// <summary>切り出し範囲（選択矩形 → 画像内のピクセル矩形）の計算を単一所有する。</summary>
/// <remarks>
/// 切り出しの規則（閉区間・クランプ・丸め）は 2 つの UI で共通であり、UI ごとに書くと 1 ピクセルずれる。<br/>
/// そのため本クラスが計算を単一所有する（UI は本クラスの結果を使うだけ）。<br/>
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
    /// <param name="selection">選択矩形（画像ピクセル座標）。 区間の流儀（<see cref="EsRectInterval"/>）で終点のピクセルを含めるかを決める。</param>
    /// <param name="imageSize">画像のピクセルサイズ。</param>
    /// <param name="minSelectionSize">選択矩形の最小サイズ（画像ピクセル）。 これ未満は空を返す。</param>
    /// <returns>切り出し範囲。 切り出せない場合は <c>default</c>（<see cref="EsRectInt.IsEmpty"/> が true）。</returns>
    /// <remarks>
    /// ドラッグ選択の矩形は半開区間（幅 = 終点 − 始点）で与えられ、アプリの仕様は「始点・終点のピクセルを<br/>
    /// 切り出し対象に含める（閉区間）」である。 一方、スクリーン選択・ウィンドウ選択のハイライト矩形は<br/>
    /// 検出結果が全ピクセルを含むため加算しない。 この区別は <paramref name="selection"/> の<br/>
    /// <see cref="EsRect.Interval"/> が持つ（本メソッドは受け取った値に従うだけで、終点の扱いを再判定しない）。<br/>
    /// 最小サイズの判定は画像内へクランプする前の値で行う（画像外へ出た選択を「大きい」と誤判定しない）。
    /// </remarks>
    public static EsRectInt ComputeCropBounds(
        EsRect selection, EsSize imageSize, double minSelectionSize)
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

        return selection.IsClosedInterval ? ToClosedInterval(rect, imageSize) : rect;
    }

    /// <summary>選択矩形（半開区間）を画像内の整数矩形へ変換する。</summary>
    /// <param name="selection">選択矩形（画像ピクセル座標・半開区間）。</param>
    /// <param name="imageSize">画像のピクセルサイズ。</param>
    /// <returns>画像内に収めた矩形。 切り出せる範囲が無い場合は <c>default</c>。</returns>
    /// <remarks>
    /// 1. 画像が空なら空を返す。<br/>
    /// 2. 左右・上下をそれぞれ四捨五入（0.5 は切り上げ）して整数へ直し、画像の範囲へクランプする。<br/>
    /// 3. 幅・高さが 1 ピクセル未満なら空を返す。
    /// </remarks>
    public static EsRectInt ToPixelRect(EsRect selection, EsSize imageSize)
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

        return new EsRectInt(left, top, right - left, bottom - top);
    }

    /// <summary>半開区間の矩形を閉区間（始点・終点のピクセルを含む）へ広げる。</summary>
    /// <param name="rect">切り出し矩形（半開区間。 幅 = 終点 − 始点）。</param>
    /// <returns>終点の 1 ピクセルを加えた矩形（画像の端でのクランプは行わない）。</returns>
    /// <remarks>
    /// 画像の端でのクランプが不要な場合（切り出し範囲が画像を超えないことが保証されている場合）に使う。<br/>
    /// 端でのクランプが必要な場合は <see cref="ToClosedInterval(EsRectInt, EsSize)"/> を使う。
    /// </remarks>
    public static EsRectInt ToClosedInterval(EsRectInt rect)
        => new(rect.X, rect.Y, rect.Width + 1, rect.Height + 1);

    /// <summary>半開区間の矩形を閉区間（始点・終点のピクセルを含む）へ広げる（画像の端でクランプする）。</summary>
    /// <param name="rect">切り出し矩形（半開区間。 幅 = 終点 − 始点）。</param>
    /// <param name="imageSize">画像のピクセルサイズ。</param>
    /// <returns>終点の 1 ピクセルを加えた矩形（画像の外へはみ出さないようクランプする）。</returns>
    /// <remarks>画像の右端・下端では画像サイズを超えないようにクランプする。</remarks>
    public static EsRectInt ToClosedInterval(EsRectInt rect, EsSize imageSize)
    {
        var widened = ToClosedInterval(rect);
        var imageWidth = (int)imageSize.Width;
        var imageHeight = (int)imageSize.Height;
        return new EsRectInt(
            widened.X,
            widened.Y,
            Math.Min(widened.Width, Math.Max(0, imageWidth - widened.X)),
            Math.Min(widened.Height, Math.Max(0, imageHeight - widened.Y)));
    }

    /// <summary>座標を四捨五入して整数へ直す。</summary>
    /// <param name="value">対象の座標。</param>
    /// <returns>四捨五入した値（0.5 は切り上げ）。</returns>
    /// <remarks>
    /// <see cref="Math.Round(double)"/> の既定は銀行丸め（0.5 を偶数へ）で直感に反するため、<br/>
    /// ピクセル座標の慣例に合わせて 0.5 は切り上げる（<see cref="MidpointRounding.AwayFromZero"/>）。<br/>
    /// 同じ丸めを UI が <c>(int)</c> 切り捨てや既定の <see cref="Math.Round(double)"/> で書くと、<br/>
    /// 同じ座標が UI ごとに 1 ピクセルずれるため、丸め規則として公開する。
    /// </remarks>
    public static int RoundToPixel(double value) => Round(value);

    /// <summary>座標を四捨五入して整数へ直す（内部用）。</summary>
    /// <param name="value">対象の座標。</param>
    /// <returns>四捨五入した値（0.5 は切り上げ）。</returns>
    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

using EsUtil.Platform.Core.Geometry;

namespace Estable.DragRect;

/// <summary>両 UI の描画で共有する外観値と外観の配置計算を単一所有する（UI 非依存）。</summary>
/// <remarks>
/// Avalonia 版と MewUI 版が同じ外観になるよう、値と計算を 1 箇所に集約する（重複排除・DRY）。<br/>
/// <br/>
/// 本クラスが持たないもの:<br/>
/// - フォントの実測値（テキストの幅・高さ・縦位置の調整）とラベルの内側余白の縦成分（UI ごとに文字描画の基準が異なるため各 UI が持つ）。<br/>
/// - 色・太さ・サイズの設定値（利用者が指定するため <see cref="DragRectOptions"/> が持つ）。<br/>
/// </remarks>
internal static class DragRectAppearance
{
    /// <summary>十字マーカーの線の太さ（DIP）。</summary>
    public const double MarkerThickness = 2.0;

    /// <summary>ドラッグハンドルの枠線の太さ（DIP）。</summary>
    public const double HandlePenThickness = 1.5;

    /// <summary>座標ラベルをカーソルから離すオフセット（DIP）。</summary>
    public const double LabelOffset = 8.0;

    /// <summary>座標ラベルの内側の余白（横方向・DIP）。</summary>
    public const double LabelPadding = 4.0;

    /// <summary>座標ラベルのフォントサイズの最小値。</summary>
    public const double CoordLabelFontSizeMin = 6.0;

    /// <summary>座標ラベルのフォントサイズの最大値。</summary>
    public const double CoordLabelFontSizeMax = 48.0;

    /// <summary>座標ラベルのフォントサイズの既定値。</summary>
    public const double CoordLabelFontSizeDefault = 12.0;

    /// <summary>座標ラベルのフォントサイズを安全域（6〜48）へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>6〜48 に補正した値。非数（NaN / ∞）の場合は既定値。</returns>
    public static double CoerceCoordLabelFontSize(double value)
        => double.IsFinite(value)
            ? Math.Clamp(value, CoordLabelFontSizeMin, CoordLabelFontSizeMax)
            : CoordLabelFontSizeDefault;

    /// <summary>座標ラベルの左上位置を求める。</summary>
    /// <param name="cursor">カーソルのローカル DIP 座標。</param>
    /// <param name="clientSize">描画領域のサイズ（DIP）。0 以下の軸はクランプしない。</param>
    /// <param name="labelWidth">ラベルの幅（DIP）。</param>
    /// <param name="labelHeight">ラベルの高さ（DIP）。</param>
    /// <returns>ラベルの左上位置（DIP）。</returns>
    /// <remarks>
    /// カーソルの右側（<see cref="LabelOffset"/> 分のオフセット）に配置し、描画領域の右端・下端をはみ出す場合は<br/>
    /// 領域内へクランプする（ラベル全体が見えるよう左寄せ・上寄せ）。
    /// </remarks>
    public static EsPoint ComputeCoordLabelPosition(EsPoint cursor, EsSize clientSize, double labelWidth, double labelHeight)
    {
        var x = cursor.X + LabelOffset;
        var y = cursor.Y + LabelOffset;

        if (clientSize.Width > 0)
        {
            x = Math.Min(x, Math.Max(0, clientSize.Width - labelWidth));
        }

        if (clientSize.Height > 0)
        {
            y = Math.Min(y, Math.Max(0, clientSize.Height - labelHeight));
        }

        return new EsPoint(x, y);
    }
}

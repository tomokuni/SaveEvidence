using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee;

/// <summary>両 UI の描画で共有する外観値と外観の配置計算を単一所有する（UI 非依存）。</summary>
/// <remarks>
/// 各 UI 実装が同じ外観になるよう、値と計算を 1 箇所に集約する（重複排除・DRY）。<br/>
/// <br/>
/// 本クラスが持たないもの:<br/>
/// - フォントの実測値（テキストの幅・高さ・縦位置の調整）。<br/>
/// - 色・太さ・サイズの設定値（利用者が指定するため <see cref="MarqueeOptions"/> が持つ）。<br/>
/// <br/>
/// 文字そのものの描画はホスト（描画側）が担うため、本クラスは配置の計算のみを提供する。
/// </remarks>
public static class MarqueeAppearance
{
    /// <summary>十字マーカーの線の太さ（DIP）。</summary>
    public const double MarkerThickness = 2.0;

    /// <summary>ドラッグハンドルの枠線の太さ（DIP）。</summary>
    public const double HandlePenThickness = 1.5;

    /// <summary>座標ラベルをカーソルから離すオフセット（DIP）。</summary>
    public const double LabelOffset = 8.0;

    /// <summary>座標ラベルの内側の余白（横方向・DIP）。</summary>
    public const double LabelPadding = 4.0;

    /// <summary>サイズラベルの内側の余白（DIP）。</summary>
    public const double SizeLabelPadding = 4.0;

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
        => CoerceLabelFontSize(value, CoordLabelFontSizeDefault);

    /// <summary>ラベルのフォントサイズを安全域（6〜48）へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <param name="defaultValue">非数（NaN / ∞）の場合に使う既定値。</param>
    /// <returns>6〜48 に補正した値。非数の場合は <paramref name="defaultValue"/>。</returns>
    /// <remarks>
    /// 安全域と「非数は既定値」という規則を単一所有する（サイズラベルと座標ラベルで既定値だけが異なる）。<br/>
    /// ラベルごとに補正式を書くと、片方だけが非数を素通しする余地が生じる。
    /// </remarks>
    public static double CoerceLabelFontSize(double value, double defaultValue)
        => double.IsFinite(value)
            ? Math.Clamp(value, CoordLabelFontSizeMin, CoordLabelFontSizeMax)
            : defaultValue;

    /// <summary>ドラッグハンドルの塗り色を解決する。</summary>
    /// <param name="color">設定された塗り色。</param>
    /// <returns>描画に使う塗り色。未指定（全成分 0）の場合は白。</returns>
    /// <remarks>
    /// 未指定の色（<see cref="System.Drawing.Color.Empty"/> 等の全成分 0）はハンドルが見えなくなるため白へフォールバックする。<br/>
    /// 解決を本メソッドが単一所有し、両 UI が同じ見た目になるようにする（UI 層に分岐を置かない）。
    /// </remarks>
    public static DrawingColor ResolveHandleColor(DrawingColor color)
        => color.ToArgb() == 0 ? DrawingColor.White : color;

    /// <summary>座標ラベルの背景の大きさを求める（テキストの実寸 + 内側余白 × 2）。</summary>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値）。</param>
    /// <returns>背景の大きさ（DIP）。</returns>
    public static EsSize ComputeCoordLabelBackground(EsSize textSize)
        => new(textSize.Width + LabelPadding * 2, textSize.Height + LabelPadding * 2);

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

        // 文字の送り幅は中央寄せの基準となるため、行番号の右へ 2 倍の余白を取る
        return new EsPoint(x, y);
    }

    /// <summary>サイズラベルの背景矩形を求める（テキストの実寸 + 内側余白 × 2）。</summary>
    /// <param name="origin">ラベルの基準点（穴あけ矩形の左上・DIP）。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>背景矩形（DIP）。</returns>
    public static EsRect ComputeSizeLabelBackground(EsPoint origin, EsSize textSize)
        => new(origin.X, origin.Y, textSize.Width + SizeLabelPadding * 2, textSize.Height + SizeLabelPadding * 2);

    /// <summary>サイズラベルの文字の描画原点を求める。</summary>
    /// <param name="origin">ラベルの基準点（背景矩形の左上・DIP）。</param>
    /// <returns>文字の描画原点（左へ内側余白 × 2、上へ内側余白・DIP）。</returns>
    /// <remarks>
    /// 横方向の余白を 2 倍にするのは、両 UI 実装で同一の見た目にするため（従来の行送りと同じ規則）。
    /// </remarks>
    public static EsPoint ComputeSizeLabelTextOrigin(EsPoint origin)
        => new(origin.X + SizeLabelPadding * 2, origin.Y + SizeLabelPadding);
}

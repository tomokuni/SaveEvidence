using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの数値パラメータの安全域への補正ロジック（純粋関数）。</summary>
/// <remarks>
/// ルーペの数値プロパティ（表示サイズ・拡大率・スケーリング・市松模様・外枠・ラベル文字サイズ）の<br/>
/// 既定値と安全域（最小・最大）を単一所有し、範囲外の値をクランプする純粋関数を提供する。<br/>
/// ラベルのフォントサイズの安全域は、他のコントロールと共通の規則のため <see cref="EsLabelMetrics"/> が単一所有する<br/>
/// （本クラスは <see cref="CoordLabelFontSizeDefault"/> を添えて委譲するだけ）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// </remarks>
public static class LoupeValueCoercer
{
    /// <summary>表示サイズ要求値の最小値（DIP）。</summary>
    public const int RequestedSizeMin = 1;

    /// <summary>表示サイズ要求値の最大値（DIP）。</summary>
    public const int RequestedSizeMax = 4096;

    /// <summary>表示サイズ要求値の既定値（DIP）。</summary>
    public const int RequestedSizeDefault = 160;

    /// <summary>拡大率の最小値。</summary>
    public const int ZoomLevelMin = 1;

    /// <summary>拡大率の最大値。</summary>
    public const int ZoomLevelMax = 64;

    /// <summary>拡大率の既定値。</summary>
    public const int ZoomLevelDefault = 4;

    /// <summary>スケーリング係数の最小値。</summary>
    public const double RenderScalingMin = 0.1;

    /// <summary>スケーリング係数の最大値。</summary>
    public const double RenderScalingMax = 8.0;

    /// <summary>スケーリング係数の既定値（補正不能時のフォールバックも兼ねる）。</summary>
    public const double RenderScalingDefault = 1.0;

    /// <summary>市松模様の 1 タイルサイズの最小値（ピクセル）。</summary>
    public const int CheckerSizeMin = 1;

    /// <summary>市松模様の 1 タイルサイズの最大値（ピクセル）。</summary>
    public const int CheckerSizeMax = 128;

    /// <summary>市松模様の 1 タイルサイズの既定値（ピクセル）。</summary>
    public const int CheckerSizeDefault = 6;

    /// <summary>外枠の太さの最小値（ピクセル）。</summary>
    public const int FrameWidthMin = 1;

    /// <summary>外枠の太さの最大値（ピクセル）。</summary>
    public const int FrameWidthMax = 64;

    /// <summary>外枠の太さの既定値（ピクセル）。</summary>
    public const int FrameWidthDefault = 2;

    /// <summary>座標ラベルのフォントサイズの最小値（DIP）。</summary>
    /// <remarks>ラベルのフォントサイズの安全域は両コントロールで共通のため、値は <see cref="EsLabelMetrics"/> が単一所有する。</remarks>
    public const double CoordLabelFontSizeMin = EsLabelMetrics.FontSizeMin;

    /// <summary>座標ラベルのフォントサイズの最大値（DIP）。</summary>
    /// <remarks>ラベルのフォントサイズの安全域は両コントロールで共通のため、値は <see cref="EsLabelMetrics"/> が単一所有する。</remarks>
    public const double CoordLabelFontSizeMax = EsLabelMetrics.FontSizeMax;

    /// <summary>座標ラベルのフォントサイズの既定値（DIP）。</summary>
    public const double CoordLabelFontSizeDefault = EsLabelDefaults.FontSize;

    /// <summary>座標ラベルの背景の角丸半径の最小値（ピクセル。0 は角丸なし）。</summary>
    public const int CoordLabelCornerRadiusMin = 0;

    /// <summary>座標ラベルの背景の角丸半径の最大値（ピクセル）。</summary>
    public const int CoordLabelCornerRadiusMax = 64;

    /// <summary>座標ラベルの背景の角丸半径の既定値（ピクセル）。</summary>
    public const int CoordLabelCornerRadiusDefault = 2;

    /// <summary>表示サイズ要求値を安全域へ補正する（1〜4096）。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <returns>安全域にクランプした値。</returns>
    public static int CoerceRequestedSize(int value) => Math.Clamp(value, RequestedSizeMin, RequestedSizeMax);

    /// <summary>拡大率を安全域へ補正する（1〜64）。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>安全域にクランプした値。</returns>
    public static int CoerceZoomLevel(int value) => Math.Clamp(value, ZoomLevelMin, ZoomLevelMax);

    /// <summary>スケーリング係数を安全域へ補正する（0.1〜8.0。非有限は既定値）。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>安全域にクランプした値。非有限の場合は既定値。</returns>
    public static double CoerceRenderScaling(double value)
        => double.IsFinite(value) ? Math.Clamp(value, RenderScalingMin, RenderScalingMax) : RenderScalingDefault;

    /// <summary>市松模様の 1 タイルサイズを安全域へ補正する（1〜128）。</summary>
    /// <param name="value">補正する値（ピクセル）。</param>
    /// <returns>安全域にクランプした値。</returns>
    public static int CoerceCheckerSize(int value) => Math.Clamp(value, CheckerSizeMin, CheckerSizeMax);

    /// <summary>外枠の太さを安全域へ補正する（1〜64）。</summary>
    /// <param name="value">補正する値（ピクセル）。</param>
    /// <returns>安全域にクランプした値。</returns>
    public static int CoerceFrameWidth(int value) => Math.Clamp(value, FrameWidthMin, FrameWidthMax);

    /// <summary>座標ラベルのフォントサイズを安全域へ補正する（6〜48。非有限は既定値）。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <returns>安全域にクランプした値。非有限の場合は既定値。</returns>
    /// <remarks>安全域と「非数は既定値」の規則は <see cref="EsLabelMetrics"/> が単一所有する（他のラベルと同じ規則）。</remarks>
    public static double CoerceCoordLabelFontSize(double value)
        => EsLabelMetrics.CoerceFontSize(value, CoordLabelFontSizeDefault);

    /// <summary>座標ラベルの背景の角丸半径を安全域へ補正する（0〜64）。</summary>
    /// <param name="value">補正する値（ピクセル。0 は角丸なし）。</param>
    /// <returns>安全域にクランプした値。</returns>
    public static int CoerceCoordLabelCornerRadius(int value)
        => Math.Clamp(value, CoordLabelCornerRadiusMin, CoordLabelCornerRadiusMax);
}

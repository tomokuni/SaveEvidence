namespace Estable.AvaloniaUI.Loupe;

/// <summary>LoupeControl の数値プロパティの安全域への補正ロジック（純粋関数）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> の <c>StyledProperty</c> coerce に渡す補正を UI 非依存の純粋関数として提供する。<br/>
/// 範囲外の値を安全域へ補正（clamp）し、UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。<br/>
/// 既定値と最小・最大値は本クラスで単一所有する（LoupeControl から委譲される）。
/// </remarks>
internal static class LoupeValueCoercer
{
    /// <summary><see cref="LoupeControl.RequestedSize"/> の最小値（DIP）。</summary>
    internal const int RequestedSizeMin = 1;

    /// <summary><see cref="LoupeControl.RequestedSize"/> の最大値（DIP）。</summary>
    internal const int RequestedSizeMax = 4096;

    /// <summary><see cref="LoupeControl.RequestedSize"/> の既定値（DIP）。</summary>
    internal const int RequestedSizeDefault = 160;

    /// <summary><see cref="LoupeControl.ZoomLevel"/> の最小値。</summary>
    internal const int ZoomLevelMin = 1;

    /// <summary><see cref="LoupeControl.ZoomLevel"/> の最大値。</summary>
    internal const int ZoomLevelMax = 64;

    /// <summary><see cref="LoupeControl.ZoomLevel"/> の既定値。</summary>
    internal const int ZoomLevelDefault = 4;

    /// <summary><see cref="LoupeControl.RenderScaling"/> の最小値。</summary>
    internal const double RenderScalingMin = 0.1;

    /// <summary><see cref="LoupeControl.RenderScaling"/> の最大値。</summary>
    internal const double RenderScalingMax = 8.0;

    /// <summary><see cref="LoupeControl.RenderScaling"/> の既定値（補正不能時のフォールバックも兼ねる）。</summary>
    internal const double RenderScalingDefault = 1.0;

    /// <summary><see cref="LoupeControl.CheckerSize"/> の最小値（ピクセル）。</summary>
    internal const int CheckerSizeMin = 1;

    /// <summary><see cref="LoupeControl.CheckerSize"/> の最大値（ピクセル）。</summary>
    internal const int CheckerSizeMax = 128;

    /// <summary><see cref="LoupeControl.CheckerSize"/> の既定値（ピクセル）。</summary>
    internal const int CheckerSizeDefault = 6;

    /// <summary><see cref="LoupeControl.FrameWidth"/> の最小値（ピクセル）。</summary>
    internal const int FrameWidthMin = 1;

    /// <summary><see cref="LoupeControl.FrameWidth"/> の最大値（ピクセル）。</summary>
    internal const int FrameWidthMax = 64;

    /// <summary><see cref="LoupeControl.FrameWidth"/> の既定値（ピクセル）。</summary>
    internal const int FrameWidthDefault = 2;

    /// <summary><see cref="LoupeControl.CoordLabelFontSize"/> の最小値（DIP）。</summary>
    internal const double CoordLabelFontSizeMin = 6;

    /// <summary><see cref="LoupeControl.CoordLabelFontSize"/> の最大値（DIP）。</summary>
    internal const double CoordLabelFontSizeMax = 48;

    /// <summary><see cref="LoupeControl.CoordLabelFontSize"/> の既定値（DIP）。</summary>
    internal const double CoordLabelFontSizeDefault = 12.0;

    /// <summary><see cref="LoupeControl.RequestedSize"/> を安全域へ補正する（1〜4096）。</summary>
    internal static int CoerceRequestedSize(int value) => Math.Clamp(value, RequestedSizeMin, RequestedSizeMax);

    /// <summary><see cref="LoupeControl.ZoomLevel"/> を安全域へ補正する（1〜64）。</summary>
    internal static int CoerceZoomLevel(int value) => Math.Clamp(value, ZoomLevelMin, ZoomLevelMax);

    /// <summary><see cref="LoupeControl.RenderScaling"/> を安全域へ補正する（0.1〜8.0。非有限は既定値）。</summary>
    internal static double CoerceRenderScaling(double value)
        => double.IsFinite(value) ? Math.Clamp(value, RenderScalingMin, RenderScalingMax) : RenderScalingDefault;

    /// <summary><see cref="LoupeControl.CheckerSize"/> を安全域へ補正する（1〜128）。</summary>
    internal static int CoerceCheckerSize(int value) => Math.Clamp(value, CheckerSizeMin, CheckerSizeMax);

    /// <summary><see cref="LoupeControl.FrameWidth"/> を安全域へ補正する（1〜64）。</summary>
    internal static int CoerceFrameWidth(int value) => Math.Clamp(value, FrameWidthMin, FrameWidthMax);

    /// <summary><see cref="LoupeControl.CoordLabelFontSize"/> を安全域へ補正する（6〜48。非有限は既定値）。</summary>
    internal static double CoerceCoordLabelFontSize(double value)
        => double.IsFinite(value) ? Math.Clamp(value, CoordLabelFontSizeMin, CoordLabelFontSizeMax) : CoordLabelFontSizeDefault;
}

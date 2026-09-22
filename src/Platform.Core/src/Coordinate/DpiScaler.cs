namespace EsUtil.Platform.Core.Coordinate;

/// <summary>DIP 値をスケーリング係数で物理ピクセルへ変換する計算ロジック（純粋関数）。</summary>
/// <remarks>
/// DIP 値をスケーリング係数（表示 DIP → 物理ピクセル）で変換する（切り上げ・最低 1 に正規化）。<br/>
/// <c>LoupeControl</c> の表示サイズ・市松模様サイズ・外枠太さ等で共用する。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class DpiScaler
{
    /// <summary>DIP（デバイス非依存ピクセル）の基準 DPI（1 ピクセル = 1 DIP となる DPI）。</summary>
    /// <remarks>
    /// スケーリング係数（<see cref="ToScaling"/>）と、DIP を 1:1 で描く描画先（<c>RenderTargetBitmap</c> 等）の DPI が共用する。<br/>
    /// 各 UI が <c>96</c> を直書きすると、同じ「DIP 基準」の規則が複数箇所へ散る（片方だけ変更される）。
    /// </remarks>
    public const double StandardDpi = 96.0;

    /// <summary>DPI をスケーリング係数（表示 DIP → 物理ピクセル）へ換算する。</summary>
    /// <param name="dpi">DPI（画面・モニターの実効 DPI）。</param>
    /// <returns>スケーリング係数（<see cref="StandardDpi"/> を 1.0 とする倍率）。</returns>
    /// <remarks>DPI の変化（モニター間の移動）を <see cref="ScaleToPixel"/> へ渡す倍率へ変換する規則を単一所有する。</remarks>
    public static double ToScaling(double dpi) => dpi / StandardDpi;

    /// <summary>DIP 値をスケーリング係数で物理ピクセルへ変換する（切り上げ・最低 1）。</summary>
    /// <param name="dipValue">変換する DIP 値。</param>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。</param>
    /// <returns>物理ピクセル値（切り上げ・最低 1）。</returns>
    /// <remarks>浮動小数の誤差（例: <c>100 × 1.1 = 110.00000000000001</c>）を吸収するため、整数に極めて近い値は切り上げない。</remarks>
    public static int ScaleToPixel(int dipValue, double scaling)
        => ScaleToPixel((double)dipValue, scaling);

    /// <summary>DIP 値（浮動小数）をスケーリング係数で物理ピクセルへ変換する（切り上げ・最低 1）。</summary>
    /// <param name="dipValue">変換する DIP 値（浮動小数）。</param>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。</param>
    /// <returns>物理ピクセル値（切り上げ・最低 1）。</returns>
    /// <remarks>浮動小数の誤差（例: <c>100 × 1.1 = 110.00000000000001</c>）を吸収するため、整数に極めて近い値は切り上げない。</remarks>
    public static int ScaleToPixel(double dipValue, double scaling)
    {
        var scaled = dipValue * scaling;
        // 浮動小数誤差（1e-9 未満）を吸収する（100 × 1.1 が 110.00000000000001 になる等の誤差で誤切り上げしない）
        var rounded = Math.Round(scaled);
        if (Math.Abs(scaled - rounded) < 1e-9)
        {
            return Math.Max(1, (int)rounded);
        }

        return Math.Max(1, (int)Math.Ceiling(scaled));
    }
}

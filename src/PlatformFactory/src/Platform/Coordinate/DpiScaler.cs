namespace Estable.AvaloniaUI.Platform.Coordinate;

/// <summary>DIP 値をスケーリング係数で物理ピクセルへ変換する計算ロジック（純粋関数）。</summary>
/// <remarks>
/// DIP 値をスケーリング係数（表示 DIP → 物理ピクセル）で変換する（切り上げ・最低 1 に正規化）。<br/>
/// <see cref="LoupeControl"/> の表示サイズ・市松模様サイズ・外枠太さ等で共用する。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class DpiScaler
{
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

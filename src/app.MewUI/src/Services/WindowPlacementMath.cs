using Estable.Platform.Geometry;

namespace SaveEvidence.MewUI.Services;

/// <summary>ウィンドウの位置・サイズの相互変換（DIP ⇔ 画面物理ピクセル）を提供する純粋クラス。</summary>
/// <remarks>
/// <para>
/// MewUI のウィンドウ位置・サイズは DIP で扱われるが、設定（<c>Settings.MainWindowBounds</c>）は<br/>
/// UI 非依存の画面物理ピクセル（<see cref="EsPixelRect"/>）で保持する。<br/>
/// 本クラスがその変換を単一所有し、往復の誤差と丸めの規則を 1 箇所に閉じる。
/// </para>
/// <para>
/// 純粋関数のみのため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </para>
/// </remarks>
internal static class WindowPlacementMath
{
    /// <summary>DIP の座標を画面物理ピクセルの座標へ変換する。</summary>
    /// <param name="dip">DIP の座標。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。1 以下の場合は有効値として扱わない。</param>
    /// <returns>画面物理ピクセルの座標（四捨五入）。</returns>
    /// <remarks>DPI スケールは 1 未満（縮小表示）もあり得るため、0 以下だけを無効として扱う。</remarks>
    public static EsPixelPoint ToPhysicalPixelPoint(EsPoint dip, double scale)
    {
        var factor = NormalizeScale(scale);
        return new EsPixelPoint(
            (int)Math.Round(dip.X * factor),
            (int)Math.Round(dip.Y * factor));
    }

    /// <summary>DIP のサイズを画面物理ピクセルのサイズへ変換する。</summary>
    /// <param name="dip">DIP のサイズ。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>画面物理ピクセルのサイズ（四捨五入・最小 1）。</returns>
    /// <remarks>
    /// ウィンドウの幅・高さが 0 になると復元できないため、最小 1 にクランプする。<br/>
    /// 戻り値は設定が使う整数型（<c>System.Drawing.Size</c>）とする（<see cref="EsPixelRect"/> へそのまま渡せる）。
    /// </remarks>
    public static System.Drawing.Size ToPhysicalPixelSize(EsSize dip, double scale)
    {
        var factor = NormalizeScale(scale);
        return new System.Drawing.Size(
            Math.Max(1, (int)Math.Round(dip.Width * factor)),
            Math.Max(1, (int)Math.Round(dip.Height * factor)));
    }

    /// <summary>画面物理ピクセルの座標を DIP の座標へ変換する。</summary>
    /// <param name="physical">画面物理ピクセルの座標。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>DIP の座標。</returns>
    public static EsPoint ToDipPoint(EsPixelPoint physical, double scale)
        => new(physical.X / NormalizeScale(scale), physical.Y / NormalizeScale(scale));

    /// <summary>画面物理ピクセルの長さを DIP の長さへ変換する。</summary>
    /// <param name="physical">画面物理ピクセルの長さ。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>DIP の長さ。</returns>
    public static double ToDipLength(int physical, double scale)
        => physical / NormalizeScale(scale);

    /// <summary>物理ピクセルの矩形が復元に使えるかどうかを判定する。</summary>
    /// <param name="bounds">判定する矩形。null 可。</param>
    /// <returns>幅と高さがともに 1 以上なら true。</returns>
    /// <remarks>
    /// 幅・高さが 0 以下の値（壊れた設定ファイル・手編集の誤り）では復元しない<br/>
    /// （0 サイズのウィンドウが作られて操作不能になるのを防ぐ）。
    /// </remarks>
    public static bool IsUsable(EsPixelRect? bounds)
        => bounds is { Width: > 0, Height: > 0 };

    /// <summary>DPI スケール係数を有効値へ正規化する。</summary>
    /// <param name="scale">DPI スケール係数。</param>
    /// <returns>正の値の場合はその値、0 以下または非数の場合は 1.0。</returns>
    /// <remarks>スケールを取得できない場合（ウィンドウ未生成・API 失敗）に座標が 0 へ潰れるのを防ぐ。</remarks>
    private static double NormalizeScale(double scale)
        => double.IsFinite(scale) && scale > 0 ? scale : 1.0;
}

using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;

/// <summary>モニターごとの DPI（表示倍率）を解決する Win32 の入口。</summary>
/// <remarks>
/// <b>位置付け</b>: WinUI 3 の公開 API はウィンドウの表示倍率（<c>XamlRoot.RasterizationScale</c>）までしか与えず、<br/>
/// 仮想スクリーン全体を覆う 1 枚のウィンドウからはモニターごとの実倍率を特定できない。<br/>
/// サイズラベルの DisplayScale（モニターの実表示スケール）を求めるため、本クラスが DPI 照会だけを所有する。<br/>
/// <br/>
/// <b>宣言の仕方</b>: 安全でないコードの許可を必要としない <c>DllImport</c> を使い、<br/>
/// 読み込み先を System32 に固定する（<c>DefaultDllImportSearchPaths</c>。 探索順の脆弱性を作らない）。<br/>
/// <b>失敗時</b>: 照会できない場合は基準の DPI（96 = 100%）を返す（例外にしない。 表示倍率は診断用の補正値である）。
/// </remarks>
internal static class MonitorDpi
{
    /// <summary>基準の DPI（96 = 表示倍率 100%）。</summary>
    private const uint StandardDpi = 96;

    /// <summary>DPI の種類（有効 DPI）。</summary>
    private const int EffectiveDpi = 0;

    /// <summary>指定位置を含むモニターの有効 DPI を返す。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>モニターの有効 DPI。 照会できない場合は 96。</returns>
    /// <remarks>最も近いモニターを対象にする（ウィンドウが跨っていても座標から一意に決まる）。</remarks>
    public static uint ForPoint(EsPointInt screenPoint)
    {
        var monitor = MonitorFromPoint(new NativePoint { X = screenPoint.X, Y = screenPoint.Y }, MonitorDefaultToNearest);
        if (monitor == 0)
        {
            return StandardDpi;
        }

        return GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0
            ? dpiX
            : StandardDpi;
    }

    /// <summary>Win32 の <c>POINT</c> 構造体（画面座標）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        /// <summary>X 座標。</summary>
        public int X;

        /// <summary>Y 座標。</summary>
        public int Y;
    }

    /// <summary>最も近いモニターを選ぶ指定。</summary>
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>指定位置を含むモニターのハンドルを取得する（Win32 の <c>MonitorFromPoint</c>）。</summary>
    /// <param name="point">判定する位置（画面物理座標）。</param>
    /// <param name="flags">モニターの選択方法。</param>
    /// <returns>モニターのハンドル。 取得できない場合は 0。</returns>
    [DllImport("user32.dll", EntryPoint = "MonitorFromPoint")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    /// <summary>モニターの DPI を取得する（Shcore の <c>GetDpiForMonitor</c>）。</summary>
    /// <param name="monitor">対象のモニター ハンドル。</param>
    /// <param name="dpiType">DPI の種類（0 = 有効 DPI）。</param>
    /// <param name="dpiX">X 方向の DPI。</param>
    /// <param name="dpiY">Y 方向の DPI。</param>
    /// <returns>成功した場合は 0（<c>S_OK</c>）。</returns>
    [DllImport("shcore.dll", EntryPoint = "GetDpiForMonitor")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);
}

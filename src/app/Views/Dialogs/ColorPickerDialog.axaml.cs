using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;

namespace app.Views.Dialogs;

/// <summary>Win32 ChooseColor API を使用した Windows 標準カラーピッカー。</summary>
/// <remarks>
/// 内部で <c>comdlg32.dll</c> の <c>ChooseColorW</c> を呼び出し、Windows 標準の
/// 色選択ダイアログを表示する。<br/>
/// 自身は Avalonia ウィンドウを生成しないため、<c>PopupRoot PlatformImpl is null</c>
/// の警告が発生しない。<br/>
/// </remarks>
public static partial class ColorPickerDialog
{
    // カスタム色を保持する静的バッファ（セッション間で保持）
    private static readonly uint[] s_customColors = new uint[16];

    /// <summary>Win32 ChooseColor を呼び出して Windows 標準カラーピッカーを表示する。</summary>
    /// <param name="owner">親ウィンドウ（ハンドル取得に使用）。</param>
    /// <param name="initialColor">初期色の16進文字列（例: "#FF0000", "Red"）。</param>
    /// <returns>選択された色の16進文字列（#RRGGBB）。キャンセル時は <c>null</c>。</returns>
    public static Task<string?> ShowAsync(TopLevel owner, string? initialColor)
    {
        var handle = owner.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        return ShowCore(handle, initialColor);
    }

    /// <summary>Win32 ウィンドウハンドルを直接指定してカラーピッカーを表示する。</summary>
    private static Task<string?> ShowCore(IntPtr hwndOwner, string? initialColor)
    {
        var colorRef = ParseColorToCOLORREF(initialColor);

        var cc = new CHOOSECOLOR
        {
            lStructSize = Marshal.SizeOf<CHOOSECOLOR>(),
            hwndOwner = hwndOwner,
            lpCustColors = Marshal.AllocHGlobal(16 * 4),
            Flags = CC_RGBINIT | CC_FULLOPEN | CC_SOLIDCOLOR,
            rgbResult = colorRef,
        };

        try
        {
            for (int i = 0; i < 16; i++)
                Marshal.WriteInt32(cc.lpCustColors, i * 4, unchecked((int)s_customColors[i]));

            string? result = null;
            if (ChooseColorW(ref cc))
            {
                for (int i = 0; i < 16; i++)
                    s_customColors[i] = unchecked((uint)Marshal.ReadInt32(cc.lpCustColors, i * 4));

                result = COLORREFToHex(cc.rgbResult);
            }

            return Task.FromResult(result);
        }
        finally
        {
            Marshal.FreeHGlobal(cc.lpCustColors);
        }
    }

    /// <summary>16進色文字列（#RRGGBB または色名）を COLORREF（0x00BBGGRR）に変換する。</summary>
    private static uint ParseColorToCOLORREF(string? hexOrName)
    {
        if (string.IsNullOrWhiteSpace(hexOrName)) return 0x00FFFFFF;
        if (Color.TryParse(hexOrName, out var color))
            return (uint)(color.B << 16 | color.G << 8 | color.R);
        return 0x00FFFFFF;
    }

    /// <summary>COLORREF（0x00BBGGRR）を #RRGGBB 形式の文字列に変換する。</summary>
    private static string COLORREFToHex(uint colorRef)
    {
        var r = (byte)(colorRef & 0xFF);
        var g = (byte)((colorRef >> 8) & 0xFF);
        var b = (byte)((colorRef >> 16) & 0xFF);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    [LibraryImport("comdlg32.dll", EntryPoint = "ChooseColorW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChooseColorW(ref CHOOSECOLOR lpcc);

    [StructLayout(LayoutKind.Sequential)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public uint rgbResult;
        public IntPtr lpCustColors;
        public uint Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    private const uint CC_RGBINIT = 0x00000001;
    private const uint CC_FULLOPEN = 0x00000002;
    private const uint CC_SOLIDCOLOR = 0x00000080;
}

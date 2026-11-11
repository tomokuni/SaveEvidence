using System.Runtime.InteropServices;

namespace EsUtil.SaveEvidence.App.Core.Native;

/// <summary>ファイルをごみ箱へ移動する Windows のシェル操作（<c>SHFileOperationW</c>）を単一所有する。</summary>
/// <remarks>
/// <b>シェルの既定 UI を出さない</b>: 確認はアプリのダイアログが行うため、シェル側の確認・進行・<br/>
/// エラーのダイアログはすべて抑止する（<c>FOF_NOCONFIRMATION</c> / <c>FOF_SILENT</c> / <c>FOF_NOERRORUI</c>）。<br/>
/// 失敗は戻り値で表し、例外への変換は呼び出し側（<c>FileSystemActions</c>）が行う<br/>
/// （ネイティブの戻り値の解釈を UI 層へ漏らさない）。
/// </remarks>
internal static class WinRecycleBin
{
    /// <summary>削除（ごみ箱へ移動）の操作コード。</summary>
    private const uint FO_DELETE = 0x0003;

    /// <summary>ごみ箱へ移動する（完全削除しない）。</summary>
    private const ushort FOF_ALLOWUNDO = 0x0040;

    /// <summary>確認のダイアログを出さない。</summary>
    private const ushort FOF_NOCONFIRMATION = 0x0010;

    /// <summary>進行状況のダイアログを出さない。</summary>
    private const ushort FOF_SILENT = 0x0004;

    /// <summary>エラーのダイアログを出さない。</summary>
    private const ushort FOF_NOERRORUI = 0x0400;

    /// <summary>ファイルをごみ箱へ移動する。</summary>
    /// <param name="filePath">移動するファイルの絶対パス。</param>
    /// <returns>移動できた場合は true。 シェルが失敗した（または操作が中止された）場合は false。</returns>
    /// <remarks>
    /// <c>pFrom</c> は<b>二重の null 終端</b>を要求するため、パスの末尾へ <c>\0</c> を足す<br/>
    /// （マーシャラーが付ける終端と合わせて 2 つになる）。 パスに空白が含まれても、この形式では区切りにならない。
    /// </remarks>
    public static bool TryDelete(string filePath)
    {
        var operation = new ShellFileOperation
        {
            Function = FO_DELETE,
            From = filePath + "\0",
            Flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        var result = SHFileOperationW(ref operation);
        return result == 0 && operation.AnyOperationsAborted == 0;
    }

    /// <summary>シェルのファイル操作を表す構造体（<c>SHFILEOPSTRUCTW</c>）。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileOperation
    {
        /// <summary>操作のダイアログを所有するウィンドウ（不要のため 0）。</summary>
        public IntPtr Window;

        /// <summary>操作の種類（<c>FO_DELETE</c> 等）。</summary>
        public uint Function;

        /// <summary>操作するファイル（複数の場合は null で区切り、最後に二重の null 終端）。</summary>
        public string From;

        /// <summary>移動先（削除では使わない）。</summary>
        public string? To;

        /// <summary>操作のフラグ。</summary>
        public ushort Flags;

        /// <summary>操作が中止されたかどうか（0 以外なら中止）。</summary>
        public int AnyOperationsAborted;

        /// <summary>シェルが返す名前の対応表（削除では使わない）。</summary>
        public IntPtr NameMappings;

        /// <summary>進行状況のダイアログのタイトル（<c>FOF_SILENT</c> のため使わない）。</summary>
        public string? ProgressTitle;
    }

    /// <summary>シェルのファイル操作（削除はごみ箱へ移動）を実行する。</summary>
    /// <param name="operation">操作の内容。</param>
    /// <returns>0 の場合は成功。</returns>
    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode, SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SHFileOperationW(ref ShellFileOperation operation);
}

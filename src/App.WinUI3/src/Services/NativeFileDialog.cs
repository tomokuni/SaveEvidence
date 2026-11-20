using System.Runtime.InteropServices;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>エクスプローラー形式のファイルを開くダイアログ（既定のフォルダを指定できる）。</summary>
/// <remarks>
/// <b>WinRT の <c>FileOpenPicker</c> は既定のフォルダを指定できない</b>（<c>SuggestedStartLocation</c> は
/// ライブラリ単位のみ）。 保存フォルダを既定にするため、COM の <c>IFileOpenDialog</c> を直接使う。<br/>
/// <b>オーナー ウィンドウは必須</b>: デスクトップ アプリでは指定しないとダイアログが出ない（実測 2026-11-20）。
/// </remarks>
internal static class NativeFileDialog
{
    /// <summary>取り消しを表す HRESULT（<c>ERROR_CANCELLED</c>）。</summary>
    private const uint CancelledHResult = 0x800704C7u;

    /// <summary>ファイルを 1 つ選ぶダイアログを表示する。</summary>
    /// <param name="owner">オーナー ウィンドウのハンドル。</param>
    /// <param name="initialFolder">最初に開くフォルダ（存在しない場合は無視する）。</param>
    /// <param name="title">ダイアログの題名。</param>
    /// <param name="filterName">絞り込みの表示名。</param>
    /// <param name="filterPattern">絞り込みのパターン（例: <c>*.annotations.json</c>）。</param>
    /// <returns>選ばれたファイルのパス。 取り消した場合は <see langword="null"/>。</returns>
    public static string? ShowOpenFile(
        IntPtr owner, string? initialFolder, string title, string filterName, string filterPattern)
    {
        var dialogType = Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7"))
            ?? throw new InvalidOperationException("ファイル ダイアログの COM クラスが見つかりません。");
        var dialog = (IFileDialog)Activator.CreateInstance(dialogType)!;
        try
        {
            dialog.SetOptions(FileOpenOptions.ForceFileSystem | FileOpenOptions.FileMustExist | FileOpenOptions.NoChangeDir);
            dialog.SetTitle(title);

            var specs = new[]
            {
                new FilterSpec { Name = filterName, Spec = filterPattern },
                new FilterSpec { Name = "すべてのファイル (*.*)", Spec = "*.*" },
            };
            dialog.SetFileTypes((uint)specs.Length, specs);

            if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
            {
                var iid = typeof(IShellItem).GUID;
                if (SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref iid, out var item) == 0)
                {
                    dialog.SetFolder(item);
                    Marshal.ReleaseComObject(item);
                }
            }

            var hr = dialog.Show(owner);
            if ((uint)hr == CancelledHResult)
            {
                return null;
            }

            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            dialog.GetResult(out var result);
            if (result is null)
            {
                return null;
            }

            try
            {
                return result.GetDisplayName(ShellItemDisplayName.FileSystemPath);
            }
            finally
            {
                Marshal.ReleaseComObject(result);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    /// <summary>ファイル ダイアログのオプション。</summary>
    [Flags]
    private enum FileOpenOptions : uint
    {
        /// <summary>ファイル システムの項目だけを対象にする。</summary>
        ForceFileSystem = 0x00000040,

        /// <summary>存在するファイルだけを選べるようにする。</summary>
        FileMustExist = 0x00001000,

        /// <summary>現在のフォルダをダイアログの結果で変えない。</summary>
        NoChangeDir = 0x00000008,
    }

    /// <summary>絞り込みの 1 件（<c>COMDLG_FILTERSPEC</c>）。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FilterSpec
    {
        /// <summary>表示名。</summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Name;

        /// <summary>パターン。</summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Spec;
    }

    /// <summary>シェル項目（<c>IShellItem</c>）。</summary>
    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        /// <summary>表示名を取得する。</summary>
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName(ShellItemDisplayName sigdnName);
    }

    /// <summary>シェル項目の表示名の種類。</summary>
    private enum ShellItemDisplayName : uint
    {
        /// <summary>ファイル システムのパス。</summary>
        FileSystemPath = 0x80058000,
    }

    /// <summary>ファイルを開くダイアログ（<c>IFileOpenDialog</c>）。</summary>
    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        /// <summary>ダイアログを表示する。</summary>
        [PreserveSig]
        int Show(IntPtr parent);

        /// <summary>絞り込みを設定する。</summary>
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] FilterSpec[] specs);

        /// <summary>絞り込みの初期選択を設定する。</summary>
        void SetFileTypeIndex(uint index);

        /// <summary>絞り込みの選択を取得する。</summary>
        uint GetFileTypeIndex();

        /// <summary>イベントの通知先を登録する。</summary>
        uint Advise(IntPtr events);

        /// <summary>イベントの通知先を解除する。</summary>
        void Unadvise(uint cookie);

        /// <summary>オプションを設定する。</summary>
        void SetOptions(FileOpenOptions options);

        /// <summary>オプションを取得する。</summary>
        uint GetOptions();

        /// <summary>既定のフォルダを設定する。</summary>
        void SetDefaultFolder(IShellItem folder);

        /// <summary>フォルダを設定する。</summary>
        void SetFolder(IShellItem folder);

        /// <summary>フォルダを取得する。</summary>
        IShellItem GetFolder();

        /// <summary>現在の選択を取得する。</summary>
        IShellItem GetCurrentSelection();

        /// <summary>ファイル名を設定する。</summary>
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

        /// <summary>ファイル名を取得する。</summary>
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetFileName();

        /// <summary>題名を設定する。</summary>
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

        /// <summary>OK ボタンの文言を設定する。</summary>
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

        /// <summary>ファイル名のラベルを設定する。</summary>
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

        /// <summary>選ばれた項目を取得する。</summary>
        IShellItem GetResult(out IShellItem item);

        /// <summary>場所を追加する。</summary>
        void AddPlace(IShellItem folder, int place);

        /// <summary>既定の拡張子を設定する。</summary>
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);

        /// <summary>ダイアログを閉じる。</summary>
        void Close(int hresult);

        /// <summary>クライアントの識別子を設定する。</summary>
        void SetClientGuid(ref Guid guid);

        /// <summary>クライアントのデータを消す。</summary>
        void ClearClientData();

        /// <summary>絞り込みの対象を設定する。</summary>
        void SetFilter(IntPtr filter);
    }

    /// <summary>シェル項目をパスから作る。</summary>
    /// <param name="path">ファイル システムのパス。</param>
    /// <param name="bindContext">バインド コンテキスト（通常は 0）。</param>
    /// <param name="riid">要求するインターフェース。</param>
    /// <param name="item">作られたシェル項目。</param>
    /// <returns>HRESULT。</returns>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SHCreateItemFromParsingName(
        string path, IntPtr bindContext, ref Guid riid, out IShellItem item);
}

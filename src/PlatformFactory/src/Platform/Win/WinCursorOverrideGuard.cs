using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Estable.AvaloniaUI.Platform.Win;

/// <summary>ホストウィンドウのカーソル上書き抑止（Windows 実装: WndProc サブクラス化）。</summary>
/// <remarks>
/// 選択操作（Crop モード等）中の WM_SETCURSOR によるカーソル上書きを防ぐために使用する。<br/>
/// <c>SetWindowLongPtrW</c> で元の WndProc を置き換え、<c>CallWindowProcW</c> で元の WndProc に委譲する。<br/>
/// デリゲートはフィールドに保持し、WndProc の呼び出し中に GC されないようにする。<br/>
/// 破棄時（Detach / Dispose）に元の WndProc を復元する。<br/>
/// <br/>
/// Windows 固有のメッセージ番号（WM_SETCURSOR）とヒットテスト値（HTCLIENT）の判定は本クラスに内蔵し、<br/>
/// 利用側には <see cref="ICursorOverrideGuard.Guard"/> という抽象操作のみを公開する（プラットフォーム分離）。<br/>
/// 生成は <see cref="PlatformFactory.CreateCursorOverrideGuard"/> が唯一の経路。<br/>
/// 継承不可。<br/>
/// </remarks>
internal sealed class WinCursorOverrideGuard : ICursorOverrideGuard
{
    /// <summary>lParam 下位ワード（ヒットテスト結果）を取得するための 16bit マスク。</summary>
    private const int LowWordMask = 0xFFFF;

    /// <summary>サブクラス化中のウィンドウハンドル。未アタッチ時は <see cref="IntPtr.Zero"/>。</summary>
    private IntPtr _hwnd;

    /// <summary>元の WndProc アドレス。</summary>
    private IntPtr _originalWndProc;

    /// <summary>サブクラス化した WndProc デリゲート。GC 防止のためフィールドに保持する。</summary>
    private WinNative.WndProcDelegate? _wndProcDelegate;

    /// <summary>カーソル上書き抑止の判定コールバック（WM_SETCURSOR かつクライアント領域内のときに評価される）。</summary>
    /// <remarks>
    /// true を返すと OS によるカーソル上書きを抑止する。<br/>
    /// 未設定の場合は常に OS のデフォルト動作に委譲する。<br/>
    /// </remarks>
    public Func<bool>? Guard { get; set; }

    /// <summary>サブクラス化が設定されているかどうか。</summary>
    public bool IsAttached => _hwnd != IntPtr.Zero;

    /// <summary>指定したウィンドウの WndProc をサブクラス化する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <remarks>
    /// ネイティブハンドルを取得できない環境（デザインモード等）では何もしない（<see cref="IsAttached"/> は false のまま）。<br/>
    /// 既にアタッチ済みの場合は何もしない。<br/>
    /// </remarks>
    public void Attach(TopLevel window)
    {
        if (IsAttached) return;
        var hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;
        _hwnd = hwnd;
        _wndProcDelegate = WndProc;
        _originalWndProc = WinNative.SetWindowLongPtr(
            hwnd, WinNative.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    /// <summary>サブクラス化を解除し、元の WndProc を復元する。</summary>
    public void Detach()
    {
        if (!IsAttached) return;
        if (_originalWndProc != IntPtr.Zero)
            WinNative.SetWindowLongPtr(_hwnd, WinNative.GWLP_WNDPROC, _originalWndProc);
        _originalWndProc = IntPtr.Zero;
        _wndProcDelegate = null;
        _hwnd = IntPtr.Zero;
    }

    /// <summary>サブクラス化を解除する。</summary>
    public void Dispose() => Detach();

    /// <summary>サブクラス化した WndProc。フックしたメッセージ以外は元の WndProc へ委譲する。</summary>
    /// <remarks>
    /// WM_SETCURSOR かつクライアント領域（HTCLIENT）内ではカーソル上書き抑止の判定を行い、<br/>
    /// 抑止が決定した場合のみメッセージを処理済み（TRUE）として元の WndProc へ委譲しない。<br/>
    /// </remarks>
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // WM_SETCURSOR: OS がカーソル形状を上書きしようとしたときに送られる。
        if (msg == WinNative.WM_SETCURSOR)
        {
            // lParam 下位ワード = ヒットテスト結果。クライアント領域内のときのみ抑止判定を行う。
            int hitTest = (int)(lParam.ToInt64() & LowWordMask);
            if (hitTest == WinNative.HTCLIENT && Guard?.Invoke() == true)
                return new IntPtr(1); // 処理済み（TRUE）: OS によるカーソル上書きを抑止
        }
        return WinNative.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }
}

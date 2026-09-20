using Estable.Platform.NoOp;
using Estable.Platform.Win;
using Estable.Platform;
using Microsoft.Extensions.Logging;

namespace Estable.Platform;

/// <summary>実行時プラットフォームに応じた既定実装を生成するファクトリ（Factory Method パターン: <see cref="PlatformFactory"/>）。</summary>
/// <remarks>
/// 提供機能: 実行時プラットフォーム（Windows / 他環境）に応じて、プラットフォーム固有の既定実装を生成する。<br/>
/// <br/>
/// 特徴:<br/>
/// - Windows: Win32 API ベースの実装（<see cref="WinCursorService"/> / <see cref="WinElementFinder"/> / <see cref="WinCursorOverrideGuard"/> / <see cref="WinCaptureService"/> / <see cref="WinWindowService"/> / <see cref="WinGlobalHotKeyService{T}"/>）を生成する。<br/>
/// - 他環境（Linux / macOS）: NoOp 実装（<see cref="NoOpCursorService"/> / <see cref="NoOpElementFinder"/> / <see cref="NoOpCaptureService"/> / <see cref="NoOpWindowService"/> / <see cref="NoOpGlobalHotKeyService{T}"/>）を生成する。
///   Win32 DLL 依存を回避し、例外なく安全に動作するフォールバック。<br/>
/// <br/>
/// 実装の詳細:<br/>
/// - <c>OperatingSystem.IsWindows()</c> による分岐を本クラスに集約し、プラットフォーム判定を一元化する（DRY）。<br/>
/// - 生成された既定実装は <see cref="ICursorService"/> / <see cref="IElementFinder"/> / <see cref="ICursorOverrideGuard"/> / <see cref="ICaptureService"/> / <see cref="IWindowService"/> / <see cref="IGlobalHotKeyService{T}"/> の抽象で返し、
///   具象型への依存を排除する（DIP）。<br/>
/// - プラットフォーム実装（Win 系 / NoOp 系）は本ファクトリが唯一の生成経路となるため、internal に隠蔽できる（公開 API 最小化）。<br/>
/// <br/>
/// 注意点:<br/>
/// - ホスト（上位 AP）が独自実装を注入する場合は、本ファクトリを使わず各インターフェースへ直接注入する（DIP）。<br/>
/// </remarks>
public static class PlatformFactory
{
    /// <summary>実行時プラットフォームに応じた既定のカーソル操作サービスを生成する。</summary>
    /// <param name="logger">ロガー。null の場合は出力なし。</param>
    /// <returns>既定の <see cref="ICursorService"/> 実装。</returns>
    /// <remarks>
    /// - Windows: <see cref="WinCursorService"/>（Win32 API ベース）<br/>
    /// - 他環境（Linux / macOS）: <see cref="NoOpCursorService"/>（何もしない。Win32 DLL 依存を回避）<br/>
    /// </remarks>
    public static ICursorService CreateCursorService(ILogger? logger = null)
        => OperatingSystem.IsWindows()
            ? new WinCursorService(logger)
            : NoOpCursorService.Instance;

    /// <summary>実行時プラットフォームに応じた既定の要素検出サービスを生成する。</summary>
    /// <returns>既定の <see cref="IElementFinder"/> 実装。</returns>
    /// <remarks>
    /// - Windows: <see cref="WinElementFinder"/>（Win32 API ベース）<br/>
    /// - 他環境（Linux / macOS）: <see cref="NoOpElementFinder"/>（常に未検出。Win32 DLL 依存を回避）<br/>
    /// </remarks>
    public static IElementFinder CreateElementFinder()
        => OperatingSystem.IsWindows()
            ? new WinElementFinder()
            : NoOpElementFinder.Instance;

    /// <summary>実行時プラットフォームに応じた既定のカーソル上書き抑止ガードを生成する。</summary>
    /// <returns>既定の <see cref="ICursorOverrideGuard"/> 実装。</returns>
    /// <remarks>
    /// - Windows: <see cref="WinCursorOverrideGuard"/>（WndProc サブクラス化で WM_SETCURSOR を抑止）<br/>
    /// - 他環境（Linux / macOS）: カーソル上書きは発生しないため NoOp 相当（未アタッチのまま動作）。<br/>
    /// ホスト（上位 AP）が Crop モード等のカーソル上書き抑止を利用する場合に使用する。<br/>
    /// </remarks>
    public static ICursorOverrideGuard CreateCursorOverrideGuard()
        => OperatingSystem.IsWindows()
            ? new WinCursorOverrideGuard()
            : new NoOpCursorOverrideGuard();

    /// <summary>実行時プラットフォームに応じた既定のキャプチャサービスを生成する。</summary>
    /// <param name="logger">ロガー。null の場合は出力なし。</param>
    /// <returns>既定の <see cref="ICaptureService"/> 実装。</returns>
    /// <remarks>
    /// - Windows: <see cref="WinCaptureService"/>（System.Drawing + Win32 API ベース）<br/>
    /// - 他環境（Linux / macOS）: <see cref="NoOpCaptureService"/>（常に null を返す）<br/>
    /// </remarks>
    public static ICaptureService CreateCaptureService(ILogger? logger = null)
        => OperatingSystem.IsWindows()
            ? new WinCaptureService(logger)
            : NoOpCaptureService.Instance;

    /// <summary>実行時プラットフォームに応じた既定のネイティブウィンドウ操作サービスを生成する。</summary>
    /// <param name="logger">ロガー。null の場合は出力なし。</param>
    /// <returns>既定の <see cref="IWindowService"/> 実装。</returns>
    /// <remarks>
    /// - Windows: <see cref="WinWindowService"/>（Win32 API ベース）<br/>
    /// - 他環境（Linux / macOS）: <see cref="NoOpWindowService"/>（何もしない）<br/>
    /// </remarks>
    public static IWindowService CreateWindowService(ILogger? logger = null)
        => OperatingSystem.IsWindows()
            ? new WinWindowService(logger)
            : NoOpWindowService.Instance;

    /// <summary>実行時プラットフォームに応じた既定のグローバルホットキーサービスを生成する。</summary>
    /// <typeparam name="T">ホットキーに割り当てるアクションの識別子。</typeparam>
    /// <returns>既定の <see cref="IGlobalHotKeyService{T}"/> 実装。</returns>
    /// <remarks>
    /// - Windows: <see cref="WinGlobalHotKeyService{T}"/>（RegisterHotKey + WndProc サブクラス化）<br/>
    /// - 他環境（Linux / macOS）: NoOp 相当の無操作実装（現在は Windows のみ対応）<br/>
    /// </remarks>
    public static IGlobalHotKeyService<T> CreateGlobalHotKeyService<T>() where T : notnull
        => OperatingSystem.IsWindows()
            ? new WinGlobalHotKeyService<T>()
            : new NoOpGlobalHotKeyService<T>();
}

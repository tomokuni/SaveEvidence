using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;

namespace Estable.CaptureSession;

/// <summary>撮影中のオーナーウィンドウの退避と復帰を 1 つの操作にまとめる（<see cref="IDisposable"/> で復帰する）。</summary>
/// <remarks>
/// 撮影中のオーナー退避と復帰の手順は 2 つの UI で共通であり、順序を誤ると自ウィンドウが写り込む。<br/>
/// そのため本クラスが手順（順序を含む）を単一所有する（UI は同じ手順を書かない）。<br/>
/// <br/>
/// <b>手順（順序が重要）</b>:<br/>
/// 1. キャプチャ除外（<c>WDA_EXCLUDEFROMCAPTURE</c>）を設定する。非表示の DWM 反映を待たずに効くため、<br/>
///    先に設定すると撮影画像へ自ウィンドウが写らない。<br/>
/// 2. オーナーを画面外へ退避する（<see cref="ICaptureOwnerWindow.HideOffScreen"/>）。<br/>
/// 3. 撮影する（呼び出し側の処理）。<br/>
/// 4. オーナーの配置を復帰する（<see cref="ICaptureOwnerWindow.RestorePlacement"/>）。<br/>
/// 5. キャプチャ除外を解除する。<br/>
/// <br/>
/// <b>使用例</b>:<br/>
/// <c>using var scope = CaptureOwnerScope.Enter(windowService, handle, owner, hideOwner: true);</c> の後、<br/>
/// 撮影を行い、スコープの破棄（または <see cref="Dispose"/>）で配置と除外が復帰する。
/// </remarks>
public sealed class CaptureOwnerScope : IDisposable
{
    /// <summary>ネイティブウィンドウ操作サービス（キャプチャ除外の設定・解除）。</summary>
    private readonly IWindowService _windowService;

    /// <summary>オーナーウィンドウのハンドル。</summary>
    private readonly EsPlatformHandle _handle;

    /// <summary>退避したオーナーウィンドウ（退避していない場合は null）。</summary>
    private readonly ICaptureOwnerWindow? _owner;

    /// <summary>キャプチャ除外を設定したかどうか。</summary>
    private readonly bool _excluded;

    /// <summary>復帰済みかどうか（多重呼び出しを防ぐ）。</summary>
    private bool _disposed;

    /// <summary>オーナーウィンドウの退避と除外の設定を行う。</summary>
    /// <param name="windowService">ネイティブウィンドウ操作サービス。</param>
    /// <param name="handle">オーナーウィンドウのハンドル。</param>
    /// <param name="owner">オーナーウィンドウの操作ポート。</param>
    /// <param name="hideOwner">撮影中にオーナーを画面外へ退避するか（設定 <c>HideOwnerWindow</c>）。</param>
    private CaptureOwnerScope(
        IWindowService windowService, EsPlatformHandle handle, ICaptureOwnerWindow owner, bool hideOwner)
    {
        _windowService = windowService;
        _handle = handle;

        if (!hideOwner || !owner.IsVisible)
        {
            return;
        }

        // 1. 撮影より先にキャプチャ除外を設定する（画面キャプチャへ自ウィンドウを写さない）
        _excluded = windowService.SetCaptureExclusion(handle, exclude: true);

        // 2. 画面外へ退避する（非表示は使わない。位置の変更のためウィンドウは再作成されない）
        owner.HideOffScreen();
        _owner = owner;
    }

    /// <summary>オーナーウィンドウの退避とキャプチャ除外の設定を開始する。</summary>
    /// <param name="windowService">ネイティブウィンドウ操作サービス。</param>
    /// <param name="handle">オーナーウィンドウのハンドル。</param>
    /// <param name="owner">オーナーウィンドウの操作ポート。</param>
    /// <param name="hideOwner">撮影中にオーナーを画面外へ退避するか（設定 <c>HideOwnerWindow</c>）。</param>
    /// <returns>撮影の完了時に <see cref="Dispose"/> して配置と除外を復帰させるスコープ。</returns>
    /// <remarks>
    /// 撮影が例外で終わっても復帰するため、必ず <c>using</c> で使うこと。<br/>
    /// 退避しない場合（設定が false・オーナーが非表示）でもスコープは返す（呼び出し側の分岐を不要にする）。
    /// </remarks>
    public static CaptureOwnerScope Enter(
        IWindowService windowService, EsPlatformHandle handle, ICaptureOwnerWindow owner, bool hideOwner)
        => new(windowService, handle, owner, hideOwner);

    /// <summary>撮影前に退避していたオーナーウィンドウを復帰する（呼び出し側の処理の後に行う）。</summary>
    /// <remarks>
    /// 1. オーナーの配置を復帰する（不透明度も含む）。<br/>
    /// 2. キャプチャ除外を解除する（解除しないと以後の撮影から自ウィンドウが消え続ける）。<br/>
    /// 2 回目以降の呼び出しは何もしない。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // 4. 配置を復帰する（画像の差し替えが済んだ後に呼ぶと、新しい画像が描画済みの状態で表示される）
        _owner?.RestorePlacement();

        // 5. キャプチャ除外を解除する
        if (_excluded)
        {
            _windowService.SetCaptureExclusion(_handle, exclude: false);
        }
    }
}

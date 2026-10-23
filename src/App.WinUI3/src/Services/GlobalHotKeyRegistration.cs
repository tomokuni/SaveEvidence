using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Window;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>グローバル ホットキーの登録と押下の配信。</summary>
/// <remarks>
/// <b>役割</b>: 設定に保存された 3 種のキャプチャ（スクリーン / ウィンドウ / 範囲）のホットキーを<br/>
/// 登録し、押下時にキャプチャの開始へ配線する。 実装は共通基盤の <see cref="WinGlobalHotKeyService{T}"/>を使う。<br/>
/// <br/>
/// <b>登録できなかった組み合わせを記録する</b>: 他のアプリが同じ組み合わせを使用している場合にも起こるため、<br/>
/// 1 件の競合でアプリを止めず、失敗した組み合わせだけを Warning で残す。<br/>
/// <b>変更は次回起動時に反映</b>: OS への登録は起動時に 1 度だけ行う（設定画面からの再登録は行わない）。<br/>
/// <b>受信の開始はウィンドウ表示後</b>: <c>WM_HOTKEY</c> の受信先はウィンドウ プロシージャであり、<br/>
/// ウィンドウの生成後に確定するため、初回の表示後に <see cref="Initialize"/> を呼ぶ。
/// </remarks>
internal sealed class GlobalHotKeyRegistration : IDisposable
{
    /// <summary>ロガー（登録の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(GlobalHotKeyRegistration));

    /// <summary>共通基盤のホットキー サービス。</summary>
    private readonly IGlobalHotKeyService<CaptureType> _service = new WinGlobalHotKeyService<CaptureType>();

    /// <summary>押下を配信する購読（解放時に解除する）。</summary>
    private IDisposable? _subscription;

    /// <summary>ウィンドウ ハンドルを設定して受信を開始する。</summary>
    /// <param name="handle">ホットキーの通知先ウィンドウのネイティブ ハンドル。</param>
    /// <returns>受信を開始できた場合は true。</returns>
    /// <remarks>
    /// ウィンドウ プロシージャを差し替えられない場合は例外を投げず false を返す<br/>
    /// （ホットキーが使えないだけで、アプリの他の機能は動作する）。
    /// </remarks>
    public bool Initialize(EsPlatformHandle handle)
    {
        try
        {
            _service.Initialize(handle.Handle);
            return true;
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            s_logger.LogHotKeyInitializeFailed(ex);
            return false;
        }
    }

    /// <summary>押下されたときに配信する処理を購読する。</summary>
    /// <param name="onPressed">押下時に呼び出す処理（キャプチャの開始）。</param>
    public void Subscribe(Action<CaptureType> onPressed)
    {
        ArgumentNullException.ThrowIfNull(onPressed);
        _subscription = _service.Subscribe(onPressed);
    }

    /// <summary>設定のホットキーを登録する。</summary>
    /// <param name="settings">登録するホットキーを持つ設定。</param>
    /// <remarks>登録できなかった組み合わせは Warning で記録する（アプリは継続する）。</remarks>
    public void RegisterAll(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var failed = _service.RegisterAll(
        [
            (CaptureType.ScreenSelect, settings.SelectScreenHotKey),
            (CaptureType.WindowSelect, settings.WindowSelectHotKey),
            (CaptureType.AreaSelect, settings.AreaSelectHotKey),
        ]);

        foreach (var (action, setting) in failed)
        {
            s_logger.LogHotKeyRegisterFailed(action, setting.Key);
        }
    }

    /// <summary>登録と購読を解除する。</summary>
    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
        _service.Dispose();
    }
}

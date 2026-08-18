using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.Platform;

/// <summary>UI スレッド完全ハング時の最終保険（ウォッチドッグ）。</summary>
/// <remarks>
/// 最前面のオーバーレイウィンドウがバグ等でユーザー操作を妨げる事態を防ぐ。<br/>
/// <br/>
/// **役割分担（通常時の防止は Deactivate が担う）**:<br/>
/// - 通常時: ユーザーが他のウィンドウへ移動した瞬間（<c>Deactivated</c>）にホスト側がキャンセルし、<br/>
///   オーバーレイ削除・ポインタ表示・ポインタアイコン復帰を確実に行う。<br/>
/// - 本クラス: UI スレッドが完全にハングし <c>Deactivated</c> イベントすら発火しない場合の最終保険。<br/>
///   バックグラウンドスレッドから <see cref="IWindowService.EvacuateWindow"/> でオーバーレイを退避する。<br/>
/// <br/>
/// **スレッド安全**:<br/>
/// - UI スレッドは 1秒間隔のハートビート（<see cref="DispatcherTimer"/>）で <see cref="Pulse"/> を更新する。<br/>
/// - バックグラウンドスレッド（<see cref="System.Threading.Timer"/>）が 5秒間の無応答を検知した場合、<br/>
///   <see cref="IWindowService.EvacuateWindow"/>（プラットフォーム抽象）でオーバーレイを退避する。<br/>
/// - 退避はプラットフォーム実装（Win32 API 等のスレッド非依存 API）に委譲する。<br/>
/// - UI スレッドが復帰した場合の自動キャンセルは WM_CLOSE 送信に委ねる。<br/>
/// <br/>
/// **例外**: ウォッチドッグ開始・停止・退避処理の失敗は致命的ではないため捕捉して無視する。<br/>
/// 本クラスは <see cref="IWindowService"/> / <see cref="ILogger"/> / Avalonia のみに依存する汎用ユーティリティである。
/// </remarks>
public sealed class Watchdog(ILogger? logger, IWindowService windowService)
{
    /// <summary>UI ハングと判定するまでの無応答時間。</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>ウォッチドッグの監視間隔。</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger ?? NullLogger.Instance;

    /// <summary>UI スレッドのハートビートタイマー（1秒間隔）。UI スレッドが正常に動いていれば Pulse を更新する。</summary>
    private DispatcherTimer? _heartbeatTimer;

    /// <summary>UI ハング監視タイマー（バックグラウンドスレッドで動作）。UI スレッドが応答しない場合にオーバーレイを退避させる。</summary>
    private Timer? _watchdogTimer;

    /// <summary>UI スレッドのハートビート（最終応答時刻の UTC Tick）。UI スレッドのイベントハンドラで更新する。</summary>
    private long _lastPulseTicks = DateTime.UtcNow.Ticks;

    /// <summary>退避対象のオーバーレイウィンドウの識別子。UI スレッドで取得・キャッシュし、ウォッチドッグ（バックグラウンドスレッド）から参照する。</summary>
    private IPlatformHandle? _windowHandle;

    /// <summary>ウォッチドッグを開始する（バックグラウンドスレッドで UI ハングを監視）。</summary>
    /// <param name="window">退避対象のオーバーレイウィンドウ。</param>
    /// <remarks>
    /// UI スレッドが一定時間（5秒）応答しない場合、バックグラウンドスレッドからプラットフォーム実装で<br/>
    /// オーバーレイを非トップモスト化・非表示化し、タスクマネージャー等を前面に出せるようにする。<br/>
    /// ネイティブハンドルは UI スレッドでここでキャッシュし、バックグラウンドスレッド（Tick）は<br/>
    /// キャッシュ済みの <see cref="IPlatformHandle"/>（数値）のみを使用する（Avalonia オブジェクトに触れない）。<br/>
    /// UI スレッドの生存確認は 1秒間隔のハートビートタイマーで行う。<br/>
    /// </remarks>
    public void Start(TopLevel window)
    {
        // UI スレッドでネイティブハンドルを取得・キャッシュする（バックグラウンドスレッドから Avalonia に触れないため）
        _windowHandle = window.TryGetPlatformHandle();
        try
        {
            // ハートビート: UI スレッドが生きていれば 1 秒ごとに Pulse が更新される
            _heartbeatTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
                (_, _) => Pulse());
            _heartbeatTimer.Start();

            _watchdogTimer = new Timer(
                _ => Tick(), null, TimeSpan.FromSeconds(1), Interval);
        }
        catch
        {
            // ウォッチドッグ開始失敗は致命的ではない（監視なしで継続）
        }
    }

    /// <summary>ウォッチドッグを停止する。</summary>
    public void Stop()
    {
        try
        {
            _heartbeatTimer?.Stop();
            _heartbeatTimer = null;
            _watchdogTimer?.Dispose();
            _watchdogTimer = null;
        }
        catch
        {
            // 既に破棄済みの場合は何もしない
        }
    }

    /// <summary>UI スレッドが生存していることを通知する。各イベントハンドラで呼び出す。</summary>
    public void Pulse() => Interlocked.Exchange(ref _lastPulseTicks, DateTime.UtcNow.Ticks);

    /// <summary>ウォッチドッグの定期処理。バックグラウンドスレッドから呼ばれる。</summary>
    /// <remarks>
    /// UI スレッドがハングした場合でも、バックグラウンドスレッドからプラットフォーム実装（スレッド非依存）のみで<br/>
    /// オーバーレイを退避する。<br/>
    /// コールバックは呼ばない（UI スレッド外からの Avalonia 操作はスレッド非安全のため）。<br/>
    /// 代わりに WM_CLOSE 送信（EvacuateWindow 内）により、UI スレッドが復帰した場合の自動キャンセルに委ねる。<br/>
    /// </remarks>
    private void Tick()
    {
        // UI スレッドが応答している場合は何もしない
        var lastPulse = new DateTime(Interlocked.Read(ref _lastPulseTicks), DateTimeKind.Utc);
        if (!IsUiHung(lastPulse, Timeout)) return;

        // UI ハング検知: プラットフォーム実装でオーバーレイを退避する（キャッシュ済みハンドルのみ使用）
        if (_windowHandle is null) return;

        _log.LogWatchdogEvacuate();

        try
        {
            windowService.EvacuateWindow(_windowHandle);
        }
        catch
        {
            // 退避処理の失敗は無視（退避できない場合は次回の監視で再試行しないよう停止）
        }

        // 以後の監視を停止する（バックグラウンドスレッドのため _watchdogTimer のみ停止し、
        // ハートビートは UI スレッドの Stop に委ねる）
        try
        {
            _watchdogTimer?.Dispose();
            _watchdogTimer = null;
        }
        catch
        {
            // 既に破棄済みの場合は何もしない
        }
    }

    /// <summary>UI スレッドがハング状態かを判定する（純粋関数）。</summary>
    /// <param name="lastPulse">最終ハートビート時刻（UTC）。</param>
    /// <param name="timeout">無応答と判定するまでの時間。</param>
    /// <returns>最終ハートビートから <paramref name="timeout"/> 以上経過している場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// <see cref="Tick"/> のハング判定に使用する。
    /// </remarks>
    internal static bool IsUiHung(DateTime lastPulse, TimeSpan timeout)
        => DateTime.UtcNow - lastPulse >= timeout;
}

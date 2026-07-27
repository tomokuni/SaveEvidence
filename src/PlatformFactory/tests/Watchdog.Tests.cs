using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Headless.XUnit;
using BindingFlags = System.Reflection.BindingFlags;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="Watchdog"/> の単体テスト（ハートビート・停止）。</summary>
[Collection("AvaloniaHeadless")]
public class WatchdogTests
{
    /// <summary>ウィンドウ操作を記録するモック。</summary>
    private sealed class MockWindowService : IWindowService
    {
        public int EvacuateCount { get; private set; }
        public void HideFromTaskSwitcher(IPlatformHandle handle) { }
        public bool SetCaptureExclusion(IPlatformHandle handle, bool exclude) => true;
        public void EvacuateWindow(IPlatformHandle handle) => EvacuateCount++;
    }

    [AvaloniaFact]
    public void Start_タイマーを開始して停止できる()
    {
        var watchdog = new Watchdog(null, new MockWindowService());
        var window = new Window();

        // 開始（ヘッドレスでもタイマー生成は可能）→ Pulse → 停止
        watchdog.Start(window);
        watchdog.Pulse();
        watchdog.Stop();

        // 停止後も例外なく Pulse できる
        watchdog.Pulse();
    }

    [AvaloniaFact]
    public void Start_停止後に再開始できる()
    {
        var watchdog = new Watchdog(null, new MockWindowService());
        var window = new Window();

        watchdog.Start(window);
        watchdog.Stop();

        // 再開始（タイマー再生成）
        watchdog.Start(window);
        watchdog.Stop();
    }

    // ====================================================================
    // Tick（UI ハング検知・退避）
    // ====================================================================

    [AvaloniaFact]
    public void Tick_UI応答中は退避しない()
    {
        var service = new MockWindowService();
        var watchdog = new Watchdog(null, service);

        // Pulse 直後（最終応答が現在）→ タイムアウト未達 → 退避しない
        watchdog.Pulse();
        InvokeTick(watchdog);

        Assert.Equal(0, service.EvacuateCount);
    }

    [AvaloniaFact]
    public void Tick_UIハング時はオーバーレイを退避する()
    {
        var service = new MockWindowService();
        var watchdog = new Watchdog(null, service);
        var window = new Window();

        watchdog.Start(window);
        // 最終応答時刻を 5 秒以上過去へ巻き戻す（UI ハング状態を再現）
        SetLastPulseToPast(watchdog, TimeSpan.FromSeconds(10));
        InvokeTick(watchdog);

        // 退避が 1 回実行される
        Assert.Equal(1, service.EvacuateCount);
        watchdog.Stop();
    }

    [AvaloniaFact]
    public void Tick_ハンドル未設定なら退避しない()
    {
        var service = new MockWindowService();
        var watchdog = new Watchdog(null, service);

        // Start 未実行（_windowHandle = null）→ ハングしても退避しない
        SetLastPulseToPast(watchdog, TimeSpan.FromSeconds(10));
        InvokeTick(watchdog);

        Assert.Equal(0, service.EvacuateCount);
    }

    [AvaloniaFact]
    public void Tick_退避処理が失敗しても無視される()
    {
        var service = new ThrowingWindowService();
        var watchdog = new Watchdog(null, service);
        var window = new Window();

        watchdog.Start(window);
        SetLastPulseToPast(watchdog, TimeSpan.FromSeconds(10));
        InvokeTick(watchdog);

        // 退避失敗は無視（catch で握りつぶし、例外は伝播しない）
        Assert.True(true);
        watchdog.Stop();
    }

    [AvaloniaFact]
    public void Tick_退避後にウォッチドッグタイマーが破棄される()
    {
        var service = new MockWindowService();
        var watchdog = new Watchdog(null, service);
        var window = new Window();

        watchdog.Start(window);
        SetLastPulseToPast(watchdog, TimeSpan.FromSeconds(10));
        InvokeTick(watchdog);

        // 退避後は _watchdogTimer が null に設定される（以後のタイマー再発火を防ぐ）
        var timerField = typeof(Watchdog).GetField("_watchdogTimer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(timerField.GetValue(watchdog));
        watchdog.Stop();
    }

    /// <summary>退避処理が例外を投げるモック。</summary>
    private sealed class ThrowingWindowService : IWindowService
    {
        public void HideFromTaskSwitcher(IPlatformHandle handle) { }
        public bool SetCaptureExclusion(IPlatformHandle handle, bool exclude) => true;
        public void EvacuateWindow(IPlatformHandle handle) => throw new InvalidOperationException("evacuate failed");
    }

    /// <summary>private Tick をリフレクションで呼び出す。</summary>
    private static void InvokeTick(Watchdog watchdog)
    {
        typeof(Watchdog).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(watchdog, null);
    }

    /// <summary>最終応答時刻を過去へ巻き戻す（UI ハング状態の再現）。</summary>
    private static void SetLastPulseToPast(Watchdog watchdog, TimeSpan ago)
    {
        var field = typeof(Watchdog).GetField("_lastPulseTicks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(watchdog, (DateTime.UtcNow - ago).Ticks);
    }
}

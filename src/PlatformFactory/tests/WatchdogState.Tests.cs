using Avalonia.Platform;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="Watchdog"/> のヘッドレス不要テスト（純粋ロジック・並列実行）。</summary>
/// <remarks>
/// <see cref="Watchdog.Pulse"/> / <see cref="Watchdog.Stop"/> / <see cref="Watchdog.IsUiHung"/> は<br/>
/// UI スレッド（Dispatcher）を必要としないため、<c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// ヘッドレス必須のテスト（<see cref="Watchdog.Start"/>・退避フロー）は <see cref="WatchdogTests"/> が直列コレクションで検証する。
/// </remarks>
public class WatchdogStateTests
{
    /// <summary>ウィンドウ操作を記録するモック。</summary>
    private sealed class MockWindowService : IWindowService
    {
        public void HideFromTaskSwitcher(IPlatformHandle handle) { }
        public bool SetCaptureExclusion(IPlatformHandle handle, bool exclude) => true;
        public void EvacuateWindow(IPlatformHandle handle) { }
    }

    [Fact]
    public void Pulse_ハートビートを更新する()
    {
        var watchdog = new Watchdog(null, new MockWindowService());

        // 例外なく実行される
        watchdog.Pulse();
        watchdog.Pulse();
    }

    [Fact]
    public void Stop_未開始でも安全()
    {
        var watchdog = new Watchdog(null, new MockWindowService());

        // 例外なく実行される（タイマー未開始）
        watchdog.Stop();
    }

    [Fact]
    public void Stop_複数回呼んでも安全()
    {
        var watchdog = new Watchdog(null, new MockWindowService());

        watchdog.Stop();
        watchdog.Stop();
    }

    [Fact]
    public void IsUiHung_最終パルスからタイムアウト未満ならfalseを返す()
    {
        // 最終パルスが 1 秒前・タイムアウト 5 秒 → ハングしていない
        var result = Watchdog.IsUiHung(DateTime.UtcNow - TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));

        Assert.False(result);
    }

    [Fact]
    public void IsUiHung_最終パルスからタイムアウト以上経過ならtrueを返す()
    {
        // 最終パルスが 6 秒前・タイムアウト 5 秒 → ハングしている
        var result = Watchdog.IsUiHung(DateTime.UtcNow - TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(5));

        Assert.True(result);
    }

    [Fact]
    public void IsUiHung_ちょうどタイムアウトならtrueを返す()
    {
        // 最終パルスがちょうど 5 秒前 → ハングとみなす（>= 判定）
        var result = Watchdog.IsUiHung(DateTime.UtcNow - TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        Assert.True(result);
    }
}

using System.ComponentModel;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="WindowFrameMetrics"/> の仕様テスト（枠の太さの計測）。</summary>
/// <remarks>
/// 正しい値は実ウィンドウでしか測れないため、本クラスは<b>失敗の扱い</b>を固定する
/// （測れないときに 0 を返すと、枠を無視した寸法でウィンドウが作られ、最小サイズが破られる）。
/// </remarks>
public class WindowFrameMetricsTests
{
    [Fact]
    internal void Get_ハンドルがゼロなら_Win32Exception_を投げる()
    {
        var ex = Assert.Throws<Win32Exception>(() => WindowFrameMetrics.Get(IntPtr.Zero));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}

using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Session;

/// <summary><see cref="CaptureTiming"/> と <see cref="CropSessionActions"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>両 UI が同じ方針値と操作決定を使うことを固定する。</remarks>
public class CaptureTimingTests
{
    [Fact]
    internal void ScreenCaptureTimeout_は5秒である()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(5000), CaptureTiming.ScreenCaptureTimeout);
    }

    [Fact]
    internal void OwnerOffScreenCoord_は画面外の負座標である()
    {
        // ヒットテスト・要素検出の対象外になる画面外配置
        Assert.Equal(-32000, CaptureTiming.OwnerOffScreenCoord);
        Assert.True(CaptureTiming.OwnerOffScreenCoord < 0);
    }

    [Fact]
    internal void Resolve_未実行なら開始する()
    {
        Assert.Equal(CropSessionAction.Start, CropSessionActions.Resolve(isRunning: false, hasSelection: false));
        Assert.Equal(CropSessionAction.Start, CropSessionActions.Resolve(isRunning: false, hasSelection: true));
    }

    [Fact]
    internal void Resolve_実行中かつ選択ありなら確定する()
    {
        Assert.Equal(CropSessionAction.Confirm, CropSessionActions.Resolve(isRunning: true, hasSelection: true));
    }

    [Fact]
    internal void Resolve_実行中かつ選択なしならキャンセルする()
    {
        // 空の切り出しを作らない
        Assert.Equal(CropSessionAction.Cancel, CropSessionActions.Resolve(isRunning: true, hasSelection: false));
    }

    [Fact]
    internal void Resolve_実行状態と選択有無の組み合わせを全て扱う()
    {
        // 4 通りの入力すべてが定義済みの操作へ解決される（未定義値や例外を返さない）
        foreach (var isRunning in new[] { true, false })
        {
            foreach (var hasSelection in new[] { true, false })
            {
                var action = CropSessionActions.Resolve(isRunning, hasSelection);
                Assert.True(Enum.IsDefined(action));
            }
        }
    }
}


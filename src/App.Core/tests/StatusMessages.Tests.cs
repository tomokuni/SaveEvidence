using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="StatusMessages"/> の仕様テスト（通知文言）。</summary>
/// <remarks>
/// 文言は 4 つの UI 版で共通に表示されるため、本文（サイズの表記を含む）をここで固定する。<br/>
/// カルチャに依存すると表示が変わる（桁区切り・全角）ため、インバリアントな表記であることも検証する。
/// </remarks>
public class StatusMessagesTests
{
    [Fact]
    internal void CaptureCompleted_はサイズを含む()
        => Assert.Equal("キャプチャしました（640 x 480）", StatusMessages.CaptureCompleted(640, 480));

    [Fact]
    internal void CropCompleted_はサイズを含む()
        => Assert.Equal("切り出しました（120 x 90）", StatusMessages.CropCompleted(120, 90));

    [Fact]
    internal void Size_はカルチャに依存しない()
    {
        // 桁区切り（1,234）や全角の数字が混ざらない
        Assert.Equal("キャプチャしました（1234 x 5678）", StatusMessages.CaptureCompleted(1234, 5678));
    }

    [Fact]
    internal void PasteCompleted_とPasteUnavailable_は別の文言()
        => Assert.NotEqual(StatusMessages.PasteCompleted(), StatusMessages.PasteUnavailable());

    [Fact]
    internal void SaveFailed_は空でない()
        => Assert.False(string.IsNullOrWhiteSpace(StatusMessages.SaveFailed()));

    [Fact]
    internal void SaveUnavailable_はテンプレートが無効なら検証結果を含む()
        => Assert.Contains("拡張子がありません", StatusMessages.SaveUnavailable(true, "拡張子がありません"), StringComparison.Ordinal);

    [Fact]
    internal void SaveUnavailable_はテンプレートが有効なら状態を説明する()
        => Assert.Contains("保存できる状態ではありません", StatusMessages.SaveUnavailable(false, null), StringComparison.Ordinal);

    [Fact]
    internal void AutoCropDetected_は件数を含む()
        => Assert.Contains("3", StatusMessages.AutoCropDetected(3), StringComparison.Ordinal);

    [Fact]
    internal void AutoCropNotFound_とAutoCropCanceled_は空でない()
    {
        Assert.False(string.IsNullOrWhiteSpace(StatusMessages.AutoCropNotFound()));
        Assert.False(string.IsNullOrWhiteSpace(StatusMessages.AutoCropCanceled()));
    }
}

using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary><see cref="ICaptureTarget"/> の契約（セッションが期待する振る舞い）を固定するテスト。</summary>
/// <remarks>
/// インターフェイス自体に実装は無いが、セッション（Avalonia / MewUI）が依存する契約<br/>
/// （現在の画像・差し替えの通知・切り出しの適用）を検証可能な形で文書化するために置く。
/// </remarks>
public class CaptureTargetContractTests
{
    [Fact]
    public void ApplyCrop_画像の差し替えを通知する()
    {
        var target = new FakeCaptureTarget();
        var notifications = 0;
        target.ImageChanged += (_, _) => notifications++;

        target.ApplyCrop(new EsRectInt(10, 20, 30, 40));

        // セッションは本通知でルーペのソースと座標変換を更新する
        Assert.Equal(1, notifications);
        Assert.Equal(new EsRectInt(10, 20, 30, 40), target.AppliedBounds);
    }

    [Fact]
    public void CurrentImage_未設定の場合はnullを返す()
    {
        var target = new FakeCaptureTarget();

        Assert.Null(target.CurrentImage);
    }

    [Fact]
    public void CurrentImage_設定後は画像を返す()
    {
        using var image = EsBitmap.Create(4, 4);
        var target = new FakeCaptureTarget { Image = image };

        Assert.Same(image, target.CurrentImage);
    }

    /// <summary>契約を満たすフェイク実装（アクセスの記録のみ）。</summary>
    private sealed class FakeCaptureTarget : ICaptureTarget
    {
        /// <summary>保持する画像。</summary>
        public EsBitmap? Image { get; init; }

        /// <summary>適用された切り出し範囲。</summary>
        public EsRectInt? AppliedBounds { get; private set; }

        /// <inheritdoc/>
        public EsBitmap? CurrentImage => Image;

        /// <inheritdoc/>
        public event EventHandler? ImageChanged;

        /// <inheritdoc/>
        public void ApplyCrop(EsRectInt bounds)
        {
            AppliedBounds = bounds;
            ImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

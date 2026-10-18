using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="StartupPreviewImage"/> の仕様テスト（グラデーションの生成と適用）。</summary>
/// <remarks>
/// 画像は 4 つの UI 版が共通に使うため、大きさと端の色を固定する。<br/>
/// 画素は BGRA8888 で、左上が青・右下がオレンジになる（単色でないことを保証する）。
/// </remarks>
public class StartupPreviewImageTests
{
    [Fact]
    internal void CreateGradient_は同じ大きさの画像を返す()
    {
        using var image = StartupPreviewImage.CreateGradient();

        Assert.Equal(StartupPreviewImage.Width, image.Width);
        Assert.Equal(StartupPreviewImage.Height, image.Height);
    }

    [Fact]
    internal void CreateGradient_は左上が青で右下がオレンジ()
    {
        using var image = StartupPreviewImage.CreateGradient();
        using var read = EsUtil.Platform.Core.Imaging.EsReadOnlyBitmap.FromBitmap(image);

        var topLeft = read.GetPixel(0, 0);
        var bottomRight = read.GetPixel(StartupPreviewImage.Width - 1, StartupPreviewImage.Height - 1);

        // 左上は青が強い（B > R）、右下は赤が強い（R > B）
        Assert.True(topLeft.B > topLeft.R);
        Assert.True(bottomRight.R > bottomRight.B);
    }

    [Fact]
    internal void CreateGradient_は左上と右下で色が異なる()
    {
        using var image = StartupPreviewImage.CreateGradient();
        using var read = EsUtil.Platform.Core.Imaging.EsReadOnlyBitmap.FromBitmap(image);

        Assert.NotEqual(read.GetPixel(0, 0), read.GetPixel(StartupPreviewImage.Width - 1, StartupPreviewImage.Height - 1));
    }

    [Fact]
    internal void Apply_は空白なら画像を設定しない()
    {
        var viewModel = new ViewModels.MainViewModel(
            new MainViewModelTests.MockSettingsService(), new MainViewModelTests.MockImageWriter());

        Assert.False(StartupPreviewImage.Apply(viewModel, StartupPreviewMode.Blank));
        Assert.Null(viewModel.PreviewImage);
    }
}

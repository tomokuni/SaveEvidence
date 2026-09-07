using Estable.SaveEvidence.ViewModels;
using Estable.AvaloniaUI.Platform.Imaging;
using MockSettingsService = Estable.SaveEvidence.Tests.MainViewModelTests.MockSettingsService;
using Xunit;

namespace Estable.SaveEvidence.Tests;

/// <summary>自動切り出し（LineDetection 矩形検出）の統合テスト。</summary>
/// <remarks>
/// <c>[Fact]</c> で <see cref="MainViewModel.AutoCropWindowCommand"/> を実行し、<br/>
/// 検出結果（<see cref="MainViewModel.DetectedRects"/>）が設定されることを検証する。<br/>
/// 単純な矩形パターン（白背景・黒枠線）の <see cref="PbBitmap"/> をプレビューに設定する。<br/>
/// LineDetection の <see cref="LogicLineDetector"/> は純粋なピクセル検出のため、Avalonia ヘッドレスは不要。
/// </remarks>
public sealed class MainViewModelAutoCropTests
{
    /// <summary>白背景に黒枠線の矩形を描画した PbBitmap を生成する。</summary>
    /// <returns>生成した PbBitmap。</returns>
    /// <remarks>
    /// ヘッドレスでは <c>WriteableBitmap.Lock()</c> 書き込みが読み出しに反映されないため、<br/>
    /// ピクセル保持が確実な <see cref="PbBitmap"/>（SkiaSharp 内部）で構築する。
    /// </remarks>
    private static PbBitmap CreateRectBitmap()
    {
        const int size = 200;
        const int border = 3;

        var unified = PbBitmap.Create(size, size);
        unified.Clear(b: 255, g: 255, r: 255, a: 255); // 白

        // 外周に黒枠線を描く
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var isBorder = y < border || y >= size - border
                    || x < border || x >= size - border;
                if (isBorder)
                {
                    unified.SetPixel(x, y, b: 0, g: 0, r: 0, a: 255);
                }
            }
        }

        return unified;
    }

    /// <summary>自動切り出しを実行すると検出矩形が設定されることを検証する。</summary>
    [Fact]
    public void AutoCropWindow_矩形画像で検出矩形が設定される()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateRectBitmap(),
        };

        vm.AutoCropWindowCommand.Execute(null);

        Assert.True(vm.IsAutoCropActive, "自動切り出しモードが有効になること");
        Assert.NotEmpty(vm.DetectedRects);
    }

    /// <summary>画像が無い場合は自動切り出しを実行しても検出矩形が設定されないことを検証する。</summary>
    [Fact]
    public void AutoCropWindow_画像が無い場合は何もしない()
    {
        var vm = new MainViewModel(new MockSettingsService());

        vm.AutoCropWindowCommand.Execute(null);

        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);
    }

    /// <summary>画像変更時に自動切り出しの検出矩形がクリアされることを検証する。</summary>
    [Fact]
    public void AutoCropWindow_画像変更で検出矩形がクリアされる()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateRectBitmap(),
        };

        vm.AutoCropWindowCommand.Execute(null);
        Assert.NotEmpty(vm.DetectedRects);

        // 別の画像に変更すると検出矩形がクリアされる
        vm.PreviewImage = CreateRectBitmap();
        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);
    }

    /// <summary>キャンセルすると自動切り出しモードと検出矩形がクリアされることを検証する。</summary>
    [Fact]
    public void CancelAutoCrop_モードと検出矩形がクリアされる()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateRectBitmap(),
        };

        vm.AutoCropWindowCommand.Execute(null);
        Assert.True(vm.IsAutoCropActive);

        vm.CancelAutoCropCommand.Execute(null);

        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);
        Assert.Null(vm.HoveredRect);
        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    /// <summary>矩形を確定すると切り出しが実行され、モードが終了することを検証する。</summary>
    [Fact]
    public void ConfirmAutoCrop_確定した矩形で切り出してモードが終了する()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateRectBitmap(),
        };
        var beforeSize = vm.PreviewImage!.Size.Width;

        // 画像の一部（左上 100x100）を切り出す
        var bounds = new Avalonia.PixelRect(0, 0, 100, 100);
        vm.ConfirmAutoCropCommand.Execute(bounds);

        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);

        // 切り出し後は画像サイズが変わる（100x100 以外に矩形があるため既定サイズから縮小される）
        Assert.NotNull(vm.PreviewImage);
        Assert.NotEqual(beforeSize, vm.PreviewImage!.Size.Width);
    }
}

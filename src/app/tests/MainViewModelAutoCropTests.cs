using app.ViewModels;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MockSettingsService = app.Tests.MainViewModelTests.MockSettingsService;
using Xunit;

namespace app.Tests;

/// <summary>自動切り出し（LineDetection 矩形検出）の統合テスト。</summary>
/// <remarks>
/// <c>[AvaloniaFact]</c>（ヘッドレス）で <see cref="MainViewModel.AutoCropWindowCommand"/> を実行し、<br/>
/// 検出結果（<see cref="MainViewModel.DetectedRects"/>）が設定されることを検証する。<br/>
/// 単純な矩形パターン（白背景・黒枠線）の <see cref="Bitmap"/> をプレビューに設定する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public sealed class MainViewModelAutoCropTests
{
    /// <summary>白背景に黒枠線の矩形を描画した Bitmap を生成する。</summary>
    /// <returns>生成した Bitmap。</returns>
    /// <remarks>検出器の最小長（<c>MinLineLength=50</c>）を満たすよう十分なサイズ（200x200）を描画する。</remarks>
    private static Bitmap CreateRectBitmap()
    {
        const int size = 200;
        const int border = 3;
        var stride = size * 4;
        var pixels = new byte[size * stride];

        // 全体を白で塗る
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;     // B
            pixels[i + 1] = 255; // G
            pixels[i + 2] = 255; // R
            pixels[i + 3] = 255; // A
        }

        // 外周に黒枠線を描く
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var isBorder = y < border || y >= size - border
                    || x < border || x >= size - border;
                if (isBorder)
                {
                    var i = y * stride + x * 4;
                    pixels[i] = 0;
                    pixels[i + 1] = 0;
                    pixels[i + 2] = 0;
                    pixels[i + 3] = 255;
                }
            }
        }

        var wb = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = wb.Lock();
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, locked.Address, pixels.Length);
        return wb;
    }

    /// <summary>自動切り出しを実行すると検出矩形が設定されることを検証する。</summary>
    [AvaloniaFact]
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
    [AvaloniaFact]
    public void AutoCropWindow_画像が無い場合は何もしない()
    {
        var vm = new MainViewModel(new MockSettingsService());

        vm.AutoCropWindowCommand.Execute(null);

        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);
    }

    /// <summary>画像変更時に自動切り出しの検出矩形がクリアされることを検証する。</summary>
    [AvaloniaFact]
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
    [AvaloniaFact]
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
    [AvaloniaFact]
    public void ConfirmAutoCrop_確定した矩形で切り出してモードが終了する()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateRectBitmap(),
        };
        var beforeSize = vm.PreviewImage!.PixelSize;

        // 画像の一部（左上 100x100）を切り出す
        var bounds = new Avalonia.PixelRect(0, 0, 100, 100);
        vm.ConfirmAutoCropCommand.Execute(bounds);

        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);

        // 切り出し後は画像サイズが変わる（100x100 以外に矩形があるため既定サイズから縮小される）
        Assert.NotNull(vm.PreviewImage);
        Assert.NotEqual(beforeSize, vm.PreviewImage!.PixelSize);
    }
}

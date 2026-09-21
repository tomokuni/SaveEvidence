using Estable.SaveEvidence.ViewModels;
using Estable.Platform.Geometry;
using Estable.Platform.Imaging;
using MockSettingsService = Estable.SaveEvidence.Tests.MainViewModelTests.MockSettingsService;
using MockImageWriter = Estable.SaveEvidence.Tests.MainViewModelTests.MockImageWriter;
using Xunit;

namespace Estable.SaveEvidence.Tests;

/// <summary>自動切り出し（LineDetection 矩形検出）の統合テスト。</summary>
/// <remarks>
/// <c>[Fact]</c> で <see cref="MainViewModel.AutoCropWindowCommand"/> を実行し、<br/>
/// 検出結果（<see cref="MainViewModel.DetectedRects"/>）が設定されることを検証する。<br/>
/// 単純な矩形パターン（白背景・黒枠線）の <see cref="EsBitmap"/> をプレビューに設定する。<br/>
/// LineDetection の <see cref="LogicLineDetector"/> は純粋なピクセル検出のため、Avalonia ヘッドレスは不要。
/// </remarks>
public sealed class MainViewModelAutoCropTests
{
    /// <summary>白背景に黒枠線の矩形を描画した EsBitmap を生成する。</summary>
    /// <returns>生成した EsBitmap。</returns>
    /// <remarks>
    /// ヘッドレスでは <c>WriteableBitmap.Lock()</c> 書き込みが読み出しに反映されないため、<br/>
    /// ピクセル保持が確実な <see cref="EsBitmap"/>（SkiaSharp 内部）で構築する。
    /// </remarks>
    private static EsBitmap CreateRectBitmap()
    {
        const int size = 200;
        const int border = 3;

        var unified = EsBitmap.Create(size, size);
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
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            PreviewImage = CreateRectBitmap(),
        };

        vm.AutoCropWindowCommand.Execute(null);

        Assert.True(vm.IsAutoCropActive, "自動切り出しモードが有効になること");
        Assert.NotEmpty(vm.DetectedRects);
    }

    /// <summary>白背景に罫線を多数描いた EsBitmap（矩形を構成できない画像）を生成する。</summary>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <returns>生成した EsBitmap。</returns>
    /// <remarks>
    /// 重なり合うウィンドウ風の枠（コーナーがずれて 1 つの矩形を構成しない）と、<br/>
    /// 表・テキスト相当の短い罫線を描く。検出器は矩形を構成できないため、<br/>
    /// 検出した線分をそのまま返す経路（多数の線分）を通る。
    /// </remarks>
    private static EsBitmap CreateLineHeavyBitmap(int width, int height)
    {
        var image = EsBitmap.Create(width, height);
        image.Clear(b: 255, g: 255, r: 255, a: 255); // 白

        void Horizontal(int x1, int x2, int y)
        {
            for (var x = x1; x <= x2 && x < width; x++)
            {
                image.SetPixel(x, y, 0, 0, 0, 255);
            }
        }

        void Vertical(int y1, int y2, int x)
        {
            for (var y = y1; y <= y2 && y < height; y++)
            {
                image.SetPixel(x, y, 0, 0, 0, 255);
            }
        }

        // 重なり合うウィンドウ風の枠（左右・上下がずれており矩形を構成しない）
        Horizontal(width / 10, width / 2, height / 10);
        Horizontal(width / 2, width * 9 / 10, height * 3 / 10);
        Horizontal(width / 8, width / 2, height * 3 / 5);
        Horizontal(width / 2, width * 9 / 10, height * 4 / 5);
        Vertical(height / 10, height / 2, width / 10);
        Vertical(height * 3 / 10, height * 9 / 10, width / 2);
        Vertical(height * 3 / 10, height * 4 / 5, width / 8);
        Vertical(height * 7 / 10, height * 9 / 10, width * 9 / 10);

        // 表・テキスト相当の短い罫線（線分数を増やす）
        for (var i = 0; i < 60; i++)
        {
            Horizontal(width / 10 + i, width / 2 - i, height / 10 + i * 4);
            Vertical(height / 3 + i, height * 3 / 4 - i, width / 8 + i * 6);
        }

        return image;
    }

    /// <summary>矩形を構成できない線分が多い画像でも候補が上限内で短時間に収まることを検証する。</summary>
    /// <remarks>
    /// 修正前は垂直ペア×水平ペアの全組み合わせ（この画像で数千万件）を候補にしていたため、<br/>
    /// UI スレッドが長時間ブロックし、数 GB のメモリを割り当てて固まっていた（実測: 4K 相当で数十秒〜）。<br/>
    /// 修正後は矩形を構成する組み合わせのみを対象とし、候補数と時間が上限内に収まる。
    /// </remarks>
    [Fact]
    public void AutoCropWindow_矩形を構成できない画像でも候補が上限内に収まる()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            PreviewImage = CreateLineHeavyBitmap(3840, 2160),
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        vm.AutoCropWindowCommand.Execute(null);
        stopwatch.Stop();

        Assert.True(vm.IsAutoCropActive, "自動切り出しモードが有効になること");
        Assert.NotEmpty(vm.DetectedRects);
        Assert.True(vm.DetectedRects.Count <= 256, $"候補が上限を超えている: {vm.DetectedRects.Count}");

        // 修正前は同じ画像で数千万件（数十秒・数 GB）になっていた。上限内なら数秒で終わる
        Assert.True(stopwatch.Elapsed.TotalSeconds < 15, $"自動切り出しに時間がかかりすぎている: {stopwatch.Elapsed.TotalSeconds:0.0} 秒");
    }

    /// <summary>画像が無い場合は自動切り出しを実行しても検出矩形が設定されないことを検証する。</summary>
    [Fact]
    public void AutoCropWindow_画像が無い場合は何もしない()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        vm.AutoCropWindowCommand.Execute(null);

        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);
    }

    /// <summary>画像変更時に自動切り出しの検出矩形がクリアされることを検証する。</summary>
    [Fact]
    public void AutoCropWindow_画像変更で検出矩形がクリアされる()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
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
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
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
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            PreviewImage = CreateRectBitmap(),
        };
        var beforeSize = vm.PreviewImage!.Size.Width;

        // 画像の一部（左上 100x100）を切り出す
        var bounds = new EsPixelRect(0, 0, 100, 100);
        vm.ConfirmAutoCropCommand.Execute(bounds);

        Assert.False(vm.IsAutoCropActive);
        Assert.Empty(vm.DetectedRects);

        // 切り出し後は画像サイズが変わる（100x100 以外に矩形があるため既定サイズから縮小される）
        Assert.NotNull(vm.PreviewImage);
        Assert.NotEqual(beforeSize, vm.PreviewImage!.Size.Width);
    }
}

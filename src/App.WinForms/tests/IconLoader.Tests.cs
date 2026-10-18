using EsUtil.SaveEvidence.App.WinForms.Icons;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary><see cref="IconLoader"/> の描画結果を検証するテスト。</summary>
/// <remarks>
/// 描画は <see cref="Bitmap"/> と <c>Graphics.FromImage</c> だけで完結するため、<br/>
/// ウィンドウ（ハンドル）を生成せずに画素を検証できる。
/// </remarks>
public class IconLoaderTests
{
    /// <summary>すべてのアイコンが指定サイズで描画されることを検証する。</summary>
    [Fact]
    public void すべてのアイコンが指定サイズで描画される()
    {
        foreach (var icon in Enum.GetValues<AppIcon>())
        {
            using var bitmap = IconLoader.Create(icon, 24, Color.White);

            Assert.Equal(24, bitmap.Width);
            Assert.Equal(24, bitmap.Height);
            Assert.True(ContainsInk(bitmap), $"{icon} の描画結果が透明のままです。");
        }
    }

    /// <summary>線の色が描画結果へ反映されることを検証する。</summary>
    [Fact]
    public void 線の色が描画結果へ反映される()
    {
        using var red = IconLoader.Create(AppIcon.Menu, 32, Color.Red);
        using var blue = IconLoader.Create(AppIcon.Menu, 32, Color.Blue);

        Assert.True(CountRedPixels(red) > 0);
        Assert.Equal(0, CountRedPixels(blue));
    }

    /// <summary>1 未満のサイズは 1 として扱うことを検証する。</summary>
    [Fact]
    public void サイズが一未満の場合は一として扱う()
    {
        using var bitmap = IconLoader.Create(AppIcon.Save, 0, Color.White);

        Assert.Equal(1, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
    }

    /// <summary>ウィンドウ アイコンが指定サイズで作られ、呼び出しごとに別のインスタンスになることを検証する。</summary>
    /// <remarks>
    /// Windows Forms はフォームへ設定した <see cref="Icon"/> を破棄するため、同じインスタンスを共有すると<br/>
    /// 2 つ目のウィンドウで破棄済みのアイコンを使うことになる。
    /// </remarks>
    [Fact]
    public void ウィンドウアイコンを呼び出しごとに作る()
    {
        var first = IconLoader.CreateWindowIcon(24);
        var second = IconLoader.CreateWindowIcon(24);

        Assert.Equal(24, first.Width);
        Assert.Equal(24, first.Height);
        Assert.NotSame(first, second);
    }

    /// <summary>透明でない画素が 1 つ以上あるかどうかを判定する。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>透明でない画素がある場合は true。</returns>
    private static bool ContainsInk(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>赤が優勢な画素の数を数える。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>赤が優勢な画素の数。</returns>
    private static int CountRedPixels(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 0 && pixel.R > pixel.B)
                {
                    count++;
                }
            }
        }

        return count;
    }
}

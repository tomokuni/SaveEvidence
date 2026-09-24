using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeLabelLayout"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 中心座標ラベルの余白と配置計算（すべて DIP）を検証する。<br/>
/// ラベルは UI 層がテキスト要素として配置するため、コアの計算結果がそのまま要素の位置になる。
/// </remarks>
public class LoupeLabelLayoutTests
{
    [Fact]
    public void 余白はDIP単位の固定値である()
    {
        Assert.Equal(2.0, LoupeLabelLayout.OuterMargin);
        Assert.Equal(4.0, LoupeLabelLayout.PaddingX);
        Assert.Equal(1.0, LoupeLabelLayout.PaddingY);
    }

    [Fact]
    public void ComputeTextOrigin_ルーペ上端中央に配置する()
    {
        // ルーペ 100 DIP・文字幅 40 DIP → 左端は (100 - 40) / 2 = 30、上端は余白 2
        var origin = LoupeLabelLayout.ComputeTextOrigin(100, 40);

        Assert.Equal(30.0, origin.X);
        Assert.Equal(2.0, origin.Y);
    }

    [Fact]
    public void ComputeTextOrigin_文字幅が変わっても中央を保つ()
    {
        var narrow = LoupeLabelLayout.ComputeTextOrigin(200, 40);
        var wide = LoupeLabelLayout.ComputeTextOrigin(200, 80);

        Assert.Equal(80.0, narrow.X);
        Assert.Equal(60.0, wide.X);
        // 上端の余白は文字幅に依存しない
        Assert.Equal(narrow.Y, wide.Y);
    }

    [Fact]
    public void ComputeBackground_文字列の周囲へ内側余白を加える()
    {
        // ルーペ 100 DIP・文字幅 40 DIP・フォント 12 DIP
        var background = LoupeLabelLayout.ComputeBackground(100, 40, 12);

        // 左端は 30 - 4 = 26、幅は 40 + 8 = 48、高さは 12 + 1×2 = 14、上端は 2
        Assert.Equal(26.0, background.X);
        Assert.Equal(2.0, background.Y);
        Assert.Equal(48.0, background.Width);
        Assert.Equal(14.0, background.Height);
    }

    [Fact]
    public void ComputeBackground_文字幅がルーペより大きくても負の幅にならない()
    {
        var background = LoupeLabelLayout.ComputeBackground(10, 40, 12);

        // 幅は常に正（文字幅 + 内側余白）。左端は負になりうる（中央揃えのため）
        Assert.True(background.Width > 0);
        Assert.Equal(-19.0, background.X);
    }

    [Fact]
    public void ComputeBackground_上端は文字原点と一致する()
    {
        var background = LoupeLabelLayout.ComputeBackground(100, 40, 12);
        var origin = LoupeLabelLayout.ComputeTextOrigin(100, 40);

        Assert.Equal(origin.Y, background.Y);
        Assert.Equal(origin.X - LoupeLabelLayout.PaddingX, background.X);
    }

    [Fact]
    public void ComputeBackground_高さはフォントサイズと上下余白で決まる()
    {
        var small = LoupeLabelLayout.ComputeBackground(100, 40, 10);
        var large = LoupeLabelLayout.ComputeBackground(100, 40, 20);

        Assert.Equal(10 + LoupeLabelLayout.PaddingY * 2, small.Height);
        Assert.Equal(20 + LoupeLabelLayout.PaddingY * 2, large.Height);
    }
}

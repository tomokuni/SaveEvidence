using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeLabelLayout"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 中心座標ラベルの余白と配置計算を検証する。<br/>
/// Avalonia 版は同じ余白をレイアウトシステムへ渡すため、本テストが両 UI の見た目が一致する根拠になる。
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
        // ルーペ 100px・文字幅 40px → 左端は (100 - 40) / 2 = 30、上端は余白 2（拡大率 1.0）
        var origin = LoupeLabelLayout.ComputeTextOrigin(100, 40, scaling: 1.0);

        Assert.Equal(30.0, origin.X);
        Assert.Equal(2.0, origin.Y);
    }

    [Fact]
    public void ComputeTextOrigin_スケーリングを上端の余白へ適用する()
    {
        // 上端の余白は物理ピクセルへ換算する（文字幅は実測値のためそのまま）
        var origin = LoupeLabelLayout.ComputeTextOrigin(200, 50, scaling: 2.0);

        Assert.Equal(75.0, origin.X);
        Assert.Equal(4.0, origin.Y);
    }

    [Fact]
    public void ComputeBackground_文字列の周囲へ内側余白を加える()
    {
        // ルーペ 100px・文字幅 40px・フォント 12px・拡大率 1.0
        var background = LoupeLabelLayout.ComputeBackground(100, 40, 12, scaling: 1.0);

        // 左端は 30 - 4 = 26、幅は 40 + 8 = 48、高さは 12 + 1×2 = 14、上端は 2
        Assert.Equal(26.0, background.X);
        Assert.Equal(2.0, background.Y);
        Assert.Equal(48.0, background.Width);
        Assert.Equal(14.0, background.Height);
    }

    [Fact]
    public void ComputeBackground_スケーリングを余白と高さへ適用する()
    {
        var background = LoupeLabelLayout.ComputeBackground(100, 20, 10, scaling: 2.0);

        // 内側余白 4×2（左右）= 16 → 幅は 20 + 16 = 36、高さは 10 + 1×2×2 = 14、上端は 2×2 = 4
        Assert.Equal(36.0, background.Width);
        Assert.Equal(14.0, background.Height);
        Assert.Equal(4.0, background.Y);
    }

    [Fact]
    public void ComputeBackground_文字幅がルーペより大きくても負の幅にならない()
    {
        var background = LoupeLabelLayout.ComputeBackground(10, 40, 12, scaling: 1.0);

        // 幅は常に正（文字幅 + 内側余白）。左端は負になりうる（中央揃えのため）
        Assert.True(background.Width > 0);
        Assert.Equal(-19.0, background.X);
    }

    [Fact]
    public void ComputeBackground_上端は文字原点と一致する()
    {
        var background = LoupeLabelLayout.ComputeBackground(100, 40, 12, scaling: 1.5);
        var origin = LoupeLabelLayout.ComputeTextOrigin(100, 40, scaling: 1.5);

        Assert.Equal(origin.Y, background.Y);
        Assert.Equal(origin.X - LoupeLabelLayout.PaddingX * 1.5, background.X);
    }
}

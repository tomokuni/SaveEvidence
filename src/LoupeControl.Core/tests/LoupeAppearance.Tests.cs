using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeAppearance"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 既定値・安全域への補正・描画可否の判定（描画の材料になる値のみ）を検証する。<br/>
/// 実際の描画結果は各 UI のレンダラーテストが担う。
/// </remarks>
public class LoupeAppearanceTests
{
    [Fact]
    public void 既定値がコアの既定値と一致する()
    {
        var appearance = new LoupeAppearance();

        Assert.Equal(LoupeValueCoercer.CheckerSizeDefault, appearance.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthDefault, appearance.FrameWidth);
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, appearance.CoordLabelFontSize);
        Assert.True(appearance.ShowCoordLabel);
    }

    [Fact]
    public void 既定色が期待どおりである()
    {
        var appearance = new LoupeAppearance();

        // 十字線は赤 40%、市松模様は Gray / DarkGray 30%、外枠は LightGray
        Assert.Equal(DrawingColor.FromArgb(102, 255, 0, 0), appearance.CrossColor);
        Assert.Equal(DrawingColor.FromArgb(0x4C, 0x80, 0x80, 0x80), appearance.CheckerColorA);
        Assert.Equal(DrawingColor.FromArgb(0x4C, 0xA9, 0xA9, 0xA9), appearance.CheckerColorB);
        Assert.Equal(DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3), appearance.FrameColor);
        // 座標ラベルは白文字・黒 60% 背景
        Assert.Equal(DrawingColor.White, appearance.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), appearance.CoordLabelBackgroundColor);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var appearance = new LoupeAppearance();

        var changed = appearance with { FrameWidth = 5 };

        Assert.Equal(5, changed.FrameWidth);
        // 他の項目は維持される
        Assert.Equal(appearance.CheckerSize, changed.CheckerSize);
        Assert.Equal(appearance.CrossColor, changed.CrossColor);
    }

    /// <summary>数値項目が生成時に安全域へ補正されることを検証する。</summary>
    /// <remarks>
    /// 補正を <c>init</c> アクセサで行うため、オブジェクト初期化子・<c>with</c> 式・逆シリアル化の
    /// どの設定経路でも範囲内の値だけが保持される（UI 側に補正の登録を要求しない）。
    /// </remarks>
    [Fact]
    public void 生成時に数値項目が安全域へ補正される()
    {
        var appearance = new LoupeAppearance
        {
            CheckerSize = 1000,
            FrameWidth = 0,
            CoordLabelFontSize = 100,
            CoordLabelCornerRadius = 1000,
        };

        Assert.Equal(LoupeValueCoercer.CheckerSizeMax, appearance.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthMin, appearance.FrameWidth);
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeMax, appearance.CoordLabelFontSize);
        Assert.Equal(LoupeValueCoercer.CoordLabelCornerRadiusMax, appearance.CoordLabelCornerRadius);
    }

    /// <summary><c>with</c> 式による変更も生成時と同じ補正を受けることを検証する。</summary>
    [Fact]
    public void with式の変更も安全域へ補正される()
    {
        var changed = new LoupeAppearance() with { CheckerSize = 1000 };

        Assert.Equal(LoupeValueCoercer.CheckerSizeMax, changed.CheckerSize);
    }

    [Fact]
    public void 非有限のフォントサイズは既定値へ補正される()
    {
        var appearance = new LoupeAppearance { CoordLabelFontSize = double.NaN };

        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, appearance.CoordLabelFontSize);
    }

    [Fact]
    public void 安全域内の値は変更されない()
    {
        var appearance = new LoupeAppearance
        {
            CheckerSize = 8,
            FrameWidth = 3,
            CoordLabelFontSize = 16,
        };

        Assert.Equal(8, appearance.CheckerSize);
        Assert.Equal(3, appearance.FrameWidth);
        Assert.Equal(16, appearance.CoordLabelFontSize);
    }

    [Fact]
    public void 既定の外観は描画をすべて有効にする()
    {
        var appearance = new LoupeAppearance();

        Assert.True(appearance.HasCrosshair);
        Assert.True(appearance.HasFrame);
        Assert.True(appearance.HasCheckerColorA);
        Assert.True(appearance.HasCheckerColorB);
    }

    /// <summary>下地の既定が透明（背後の要素を透かす）であることを検証する。</summary>
    /// <remarks>下地はホストが指定する（不透明色を指定すると市松模様が背後の要素を透かさない）。</remarks>
    [Fact]
    public void 既定の下地は透明で描画しない()
    {
        var appearance = new LoupeAppearance();

        Assert.Equal(DrawingColor.Transparent, appearance.BackgroundColor);
        Assert.False(appearance.HasBackground);
    }

    /// <summary>下地のアルファが 1 以上なら描画する判定になることを検証する。</summary>
    [Fact]
    public void 不透明な下地は描画する判定になる()
    {
        Assert.False((new LoupeAppearance { BackgroundColor = DrawingColor.FromArgb(0, 255, 255, 255) }).HasBackground);
        Assert.True((new LoupeAppearance { BackgroundColor = DrawingColor.FromArgb(1, 0, 0, 0) }).HasBackground);
        Assert.True((new LoupeAppearance { BackgroundColor = DrawingColor.White }).HasBackground);
    }

    [Fact]
    public void 透明色は描画しない判定になる()
    {
        var appearance = new LoupeAppearance
        {
            CrossColor = DrawingColor.Transparent,
            FrameColor = DrawingColor.FromArgb(0, 1, 2, 3),
            CheckerColorA = DrawingColor.Transparent,
            CheckerColorB = DrawingColor.Transparent,
        };

        Assert.False(appearance.HasCrosshair);
        Assert.False(appearance.HasFrame);
        Assert.False(appearance.HasCheckerColorA);
        Assert.False(appearance.HasCheckerColorB);
        // アルファが 1 以上なら描画する
        Assert.True((appearance with { CrossColor = DrawingColor.FromArgb(1, 0, 0, 0) }).HasCrosshair);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldShowCoordLabel_設定と中心の有無で判定する(bool show, bool hasCenter, bool expected)
    {
        var appearance = new LoupeAppearance { ShowCoordLabel = show };

        Assert.Equal(expected, appearance.ShouldShowCoordLabel(hasCenter));
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = new LoupeAppearance { CrossColor = DrawingColor.Red, CheckerSize = 4 };
        var b = new LoupeAppearance { CrossColor = DrawingColor.Red, CheckerSize = 4 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { CheckerSize = 5 });
    }

    [Fact]
    public void 補正後も描画判定が矛盾しない()
    {
        // 補正しても透明度（アルファ）は変わらないため、描画可否の判定結果は維持される
        var appearance = new LoupeAppearance
        {
            CheckerSize = -1,
            CrossColor = DrawingColor.Transparent,
        };

        Assert.False(appearance.HasCrosshair);
        Assert.Equal(LoupeValueCoercer.CheckerSizeMin, appearance.CheckerSize);
    }

    [Fact]
    public void ラベルに使う座標は文字列化される()
    {
        // ラベル文言はコアの CoordinateFormatter が単一所有する（外観は文言を持たない）
        var text = CoordinateFormatter.Format(new EsPoint(123, 456));

        Assert.Equal("(123, 456)", text);
    }
}

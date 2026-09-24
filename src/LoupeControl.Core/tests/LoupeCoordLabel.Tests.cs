using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeCoordLabel"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 中心座標ラベルの文言・表示可否・配置はコアが単一所有する。<br/>
/// テキストの実寸は描画 API 依存のため引数で与え、本テストは契約（解決結果）と配置の計算を検証する。<br/>
/// <b>単位は DIP</b>である（UI 層がテキスト要素として配置するため、物理ピクセルへの換算は行わない）。
/// </remarks>
public class LoupeCoordLabelTests
{
    /// <summary>テスト用の描画入力を生成する。</summary>
    /// <param name="center">拡大中心。null の場合は未設定。</param>
    /// <param name="appearance">外観。null の場合は既定。</param>
    /// <param name="pixelSize">ルーペの一辺（物理ピクセル）。</param>
    /// <param name="scaling">スケーリング係数。</param>
    /// <returns>生成した描画入力。</returns>
    private static LoupeRenderRequest CreateRequest(
        EsPoint? center = null,
        LoupeAppearance? appearance = null,
        int pixelSize = 100,
        double scaling = 1.0)
        => new(center ?? new EsPoint(8, 8), pixelSize, 1, appearance ?? new LoupeAppearance(), scaling);

    // ====================================================================
    // Resolve（表示可否・文言・寸法）
    // ====================================================================

    /// <summary>中心と表示可否が揃う場合に文言と色が解決されることを検証する。</summary>
    [Fact]
    public void Resolve_文言と色を解決する()
    {
        var appearance = new LoupeAppearance
        {
            ShowCoordLabel = true,
            CoordLabelColor = DrawingColor.Yellow,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(200, 1, 2, 3),
        };

        var label = LoupeCoordLabel.Resolve(CreateRequest(new EsPoint(123, 456), appearance));

        // 文言の書式はコアの CoordinateFormatter が単一所有する
        Assert.NotNull(label);
        Assert.Equal("(123, 456)", label.Value.Text);
        Assert.Equal(DrawingColor.Yellow, label.Value.TextColor);
        Assert.Equal(DrawingColor.FromArgb(200, 1, 2, 3), label.Value.BackgroundColor);
    }

    /// <summary>中心が未設定の場合は null になることを検証する。</summary>
    [Fact]
    public void Resolve_中心未設定ならnull()
    {
        Assert.Null(LoupeCoordLabel.Resolve(CreateRequest(center: null) with { Center = null }));
    }

    /// <summary>表示しない設定の場合は中心があっても null になることを検証する。</summary>
    [Fact]
    public void Resolve_非表示の設定ならnull()
    {
        var appearance = new LoupeAppearance { ShowCoordLabel = false };

        Assert.Null(LoupeCoordLabel.Resolve(CreateRequest(appearance: appearance)));
    }

    /// <summary>フォントサイズと角丸が設定（DIP）のまま引き継がれることを検証する。</summary>
    [Fact]
    public void Resolve_寸法はDIPのまま引き継ぐ()
    {
        var appearance = new LoupeAppearance { CoordLabelFontSize = 14, CoordLabelCornerRadius = 3 };

        var label = LoupeCoordLabel.Resolve(CreateRequest(appearance: appearance, scaling: 2.0));

        Assert.NotNull(label);
        // 物理ピクセルへの換算は描画時に行うため、契約は DIP の値のまま
        Assert.Equal(14.0, label.Value.FontSize);
        Assert.Equal(3, label.Value.CornerRadius);
    }

    /// <summary>ルーペの一辺が DIP の表示サイズとして契約に引き継がれることを検証する。</summary>
    [Theory]
    [InlineData(220, 1.0, 220.0)]
    [InlineData(220, 2.0, 110.0)]
    public void Resolve_表示サイズをDIPで引き継ぐ(int pixelSize, double scaling, double expected)
    {
        var label = LoupeCoordLabel.Resolve(CreateRequest(pixelSize: pixelSize, scaling: scaling));

        // 物理ピクセルを DIP へ換算した値（PixelSize / Scaling）になる
        Assert.Equal(expected, Assert.NotNull(label).DisplaySize);
    }

    /// <summary>角丸 0（角丸なし）はそのまま保持されることを検証する。</summary>
    [Fact]
    public void Resolve_角丸0はそのまま保持する()
    {
        var appearance = new LoupeAppearance { CoordLabelCornerRadius = 0 };

        var label = LoupeCoordLabel.Resolve(CreateRequest(appearance: appearance));

        // 角丸なしは「描かない」ではなく「四角形として描く」指定のため、切り上げない
        Assert.Equal(0, Assert.NotNull(label).CornerRadius);
    }

    // ====================================================================
    // ComputeFrame（配置）
    // ====================================================================

    /// <summary>背景と文字の原点がコアのレイアウト計算と一致することを検証する。</summary>
    [Fact]
    public void ComputeFrame_背景と文字の原点を返す()
    {
        var label = new LoupeCoordLabel("(8, 8)", 100, 12, 0, DrawingColor.White, DrawingColor.White);
        var textSize = new EsSize(40, 14);

        var frame = label.ComputeFrame(textSize);

        // 背景と文字原点は LoupeLabelLayout が単一所有する（余白・上端からの距離・中央寄せ）
        Assert.Equal(LoupeLabelLayout.ComputeBackground(100, 40, 12), frame.Background);
        Assert.Equal(LoupeLabelLayout.ComputeTextOrigin(100, 40), frame.TextOrigin);
        // 文字は背景の内側へ余白分だけ入る
        Assert.Equal(LoupeLabelLayout.PaddingX, frame.TextOrigin.X - frame.Background.X);
        Assert.Equal(frame.Background.Y, frame.TextOrigin.Y);
    }

    /// <summary>表示サイズ（DIP）が配置の基準になることを検証する。</summary>
    [Fact]
    public void ComputeFrame_表示サイズを基準に配置する()
    {
        var wide = new LoupeCoordLabel("(8, 8)", 200, 12, 0, DrawingColor.White, DrawingColor.White);
        var narrow = new LoupeCoordLabel("(8, 8)", 100, 12, 0, DrawingColor.White, DrawingColor.White);

        var wideFrame = wide.ComputeFrame(new EsSize(40, 14));
        var narrowFrame = narrow.ComputeFrame(new EsSize(40, 14));

        // 水平位置はルーペ中央（(サイズ - 文字幅) / 2）になる
        Assert.Equal(80.0, wideFrame.TextOrigin.X);
        Assert.Equal(30.0, narrowFrame.TextOrigin.X);
        // 上端の余白は表示サイズに依存しない
        Assert.Equal(wideFrame.TextOrigin.Y, narrowFrame.TextOrigin.Y);
    }
}

using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="LoupeCoordLabel"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 中心座標ラベルの文言・表示可否・配置はコアが単一所有する。<br/>
/// テキストの実寸は描画 API 依存のため引数で与え、本テストは契約（解決結果）と配置の計算を検証する。<br/>
/// <b>単位は DIP</b>である（UI 層がテキスト要素として配置するため、物理ピクセルへの換算は行わない）。
/// </remarks>
public class LoupeCoordLabelTests
{
    /// <summary>テストで使用する拡大中心。</summary>
    private static readonly EsPoint s_center = new(8, 8);

    /// <summary>テスト用のラベルを解決する（拡大中心は既定の座標）。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP）。</param>
    /// <param name="appearance">外観。null の場合は既定。</param>
    /// <returns>解決したラベル（描かない場合は null）。</returns>
    private static LoupeCoordLabel? Resolve(double displaySize = 100, LoupeMetrics? appearance = null)
        => LoupeCoordLabel.Resolve(displaySize, s_center, appearance ?? new LoupeMetrics());

    // ====================================================================
    // Resolve（表示可否・文言・寸法）
    // ====================================================================

    /// <summary>中心と表示可否が揃う場合に文言が解決されることを検証する。</summary>
    [Fact]
    public void Resolve_文言を解決する()
    {
        var metrics = new LoupeMetrics { ShowCoordLabel = true };

        var label = LoupeCoordLabel.Resolve(100, new EsPoint(123, 456), metrics);

        // 文言の書式はコアの EsPoint.Format が単一所有する
        Assert.NotNull(label);
        Assert.Equal("(123, 456)", label.Value.Text);
    }

    /// <summary>中心が未設定の場合は null になることを検証する。</summary>
    [Fact]
    public void Resolve_中心未設定ならnull()
    {
        Assert.Null(LoupeCoordLabel.Resolve(100, null, new LoupeMetrics()));
    }

    /// <summary>表示しない設定の場合は中心があっても null になることを検証する。</summary>
    [Fact]
    public void Resolve_非表示の設定ならnull()
    {
        var appearance = new LoupeMetrics { ShowCoordLabel = false };

        Assert.Null(Resolve(appearance: appearance));
    }

    /// <summary>フォントサイズと角丸が設定（DIP）のまま引き継がれることを検証する。</summary>
    [Fact]
    public void Resolve_寸法はDIPのまま引き継ぐ()
    {
        var appearance = new LoupeMetrics { CoordLabelFontSize = 14, CoordLabelCornerRadius = 3 };

        var label = Resolve(appearance: appearance);

        Assert.NotNull(label);
        // 物理ピクセルへの換算は描画時に行うため、契約は DIP の値のまま
        Assert.Equal(14.0, label.Value.FontSize);
        Assert.Equal(3, label.Value.CornerRadius);
    }

    /// <summary>表示サイズは渡された DIP 値がそのまま使われることを検証する。</summary>
    [Theory]
    [InlineData(220.0)]
    [InlineData(110.5)]
    public void Resolve_表示サイズをそのまま使う(double displaySize)
    {
        var label = Resolve(displaySize: displaySize);

        // 表示サイズは要素の大きさ（DIP）をそのまま使う（物理ピクセルからの逆算は行わない）
        Assert.Equal(displaySize, Assert.NotNull(label).DisplaySize);
    }

    /// <summary>角丸 0（角丸なし）はそのまま保持されることを検証する。</summary>
    [Fact]
    public void Resolve_角丸0はそのまま保持する()
    {
        var appearance = new LoupeMetrics { CoordLabelCornerRadius = 0 };

        var label = Resolve(appearance: appearance);

        // 角丸なしは「描かない」ではなく「四角形として描く」指定のため、切り上げない
        Assert.Equal(0, Assert.NotNull(label).CornerRadius);
    }

    // ====================================================================
    // ComputeFrame（配置）
    // ====================================================================

    /// <summary>背景と文字の原点が、文字の実寸と余白から決まることを検証する。</summary>
    [Fact]
    public void ComputeFrame_背景と文字の原点を返す()
    {
        var label = new LoupeCoordLabel("(8, 8)", 100, 12, 0);
        var textSize = new EsSize(40, 14);

        var frame = label.ComputeFrame(textSize);

        // 背景は文字の実寸へ左右 4 / 上下 1 を加えた大きさ（48 x 16）で、領域（100）の中央（(100 - 48) / 2 = 26）
        Assert.Equal(new EsRect(26, 2, 48, 16), frame.Background);
        // 文字は背景の内側へ余白分だけ入る（四方の余白が等しい）
        Assert.Equal(new EsPoint(30, 3), frame.TextOrigin);
        Assert.Equal(4.0, frame.TextOrigin.X - frame.Background.X);
        Assert.Equal(1.0, frame.TextOrigin.Y - frame.Background.Y);
    }

    /// <summary>表示サイズ（DIP）が配置の基準になることを検証する。</summary>
    [Fact]
    public void ComputeFrame_表示サイズを基準に配置する()
    {
        var wide = new LoupeCoordLabel("(8, 8)", 200, 12, 0);
        var narrow = new LoupeCoordLabel("(8, 8)", 100, 12, 0);

        var wideFrame = wide.ComputeFrame(new EsSize(40, 14));
        var narrowFrame = narrow.ComputeFrame(new EsSize(40, 14));

        // 文字の中心が領域の中央に一致する
        Assert.Equal(100.0, wideFrame.TextOrigin.X + 40 / 2);
        Assert.Equal(50.0, narrowFrame.TextOrigin.X + 40 / 2);
        // 上端の余白は表示サイズに依存しない
        Assert.Equal(wideFrame.TextOrigin.Y, narrowFrame.TextOrigin.Y);
    }

    /// <summary>UI 要素へ写す際に、コアの配置と与えられた色がそのまま伝わることを検証する。</summary>
    [Fact]
    public void ToElement_配置と外観を要素の指示へ写す()
    {
        var label = new LoupeCoordLabel("(8, 8)", 100, 12, 2);

        var element = label.ToElement(new EsSize(40, 14), DrawingColor.White, DrawingColor.Black);

        Assert.Equal(new EsRect(26, 2, 48, 16), element.Bounds);
        Assert.Equal(new EsPoint(30, 3), element.TextOrigin);
        Assert.Equal("(8, 8)", element.Text);
        Assert.Equal(12, element.FontSize);
        Assert.Equal(2, element.CornerRadius);
        Assert.Equal(DrawingColor.White, element.TextColor);
        Assert.Equal(DrawingColor.Black, element.BackgroundColor);
    }
}


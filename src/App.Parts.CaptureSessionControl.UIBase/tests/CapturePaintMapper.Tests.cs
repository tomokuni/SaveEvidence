using EsUtil.Feature.Marquee.Setting;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core;

namespace EsUtil.SaveEvidence.Parts.CaptureSession.UIBase.Tests;

/// <summary><see cref="CapturePaintMapper"/> の単体テスト（設定項目と描画の色の対応）。</summary>
/// <remarks>
/// 色は描画にだけ必要な設定であり、描画層（UIBase）の <c>MarqueeAppearance</c> / <c>LoupeAppearance</c> が単一所有する。<br/>
/// 本テストは「どの設定がどの色になるか」の写像を網羅する（写像漏れはビルドが通り、色が無言で効かなくなるため）。<br/>
/// UI 初期化が不要なため、<c>[Fact]</c> で並列検証できる。
/// </remarks>
public class CapturePaintMapperTests
{
    /// <summary>全メンバー non-null の既定設定（ToNonNull 済み）を生成する。</summary>
    /// <returns>既定の設定。</returns>
    private static CaptureSettings CreateSettings() => new CaptureSettings().ToNonNull();

    [Fact]
    public void 変換_選択矩形の色が設定から写像される()
    {
        var settings = CreateSettings() with
        {
            HandleColor = DrawingColor.Red,
            BorderColor = DrawingColor.Green,
            MarkerColor = DrawingColor.Blue,
            SizeLabelColor = DrawingColor.Yellow,
            TextBackgroundColor = DrawingColor.FromArgb(10, 20, 30),
            CoordLabelColor = DrawingColor.Cyan,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(40, 50, 60),
            OverlayColor = DrawingColor.FromArgb(70, 80, 90),
        };

        var paint = CapturePaintMapper.ToMarqueeAppearance(settings);

        Assert.Equal(DrawingColor.Red, paint.HandleColor);
        Assert.Equal(DrawingColor.Green, paint.HandleBorderColor);
        Assert.Equal(DrawingColor.Blue, paint.MarkerColor);
        Assert.Equal(DrawingColor.Yellow, paint.SizeLabelColor);
        Assert.Equal(DrawingColor.FromArgb(10, 20, 30), paint.SizeLabelBackgroundColor);
        Assert.Equal(DrawingColor.Cyan, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(40, 50, 60), paint.CoordLabelBackgroundColor);
        Assert.Equal(DrawingColor.FromArgb(70, 80, 90), paint.OverlayColor);
    }

    /// <summary>選択枠線（ハンドルの枠線）と暗転マスクの外枠が同じ色になることを検証する。</summary>
    /// <remarks>両者は同じ矩形に重なって描画されるため、片方だけ別の色にすると見た目が崩れる。</remarks>
    [Fact]
    public void 変換_外枠とハンドル枠は同じ枠線色になる()
    {
        var settings = CreateSettings() with { BorderColor = DrawingColor.Red };

        var paint = CapturePaintMapper.ToMarqueeAppearance(settings);

        Assert.Equal(DrawingColor.Red, paint.HandleBorderColor);
        Assert.Equal(paint.HandleBorderColor, paint.OuterBorderColor);
    }

    /// <summary>写像しない色が既定値を保つことを検証する。</summary>
    /// <remarks>内側の塗り・内側の枠線の描画可否は <see cref="MarqueeAppearance"/> のフラグが単一所有するため、色は既定の透明のままとする。</remarks>
    [Fact]
    public void 変換_写像しない色は既定値を保つ()
    {
        var paint = CapturePaintMapper.ToMarqueeAppearance(CreateSettings());

        Assert.Equal(MarqueeAppearance.Default.InnerFillColor, paint.InnerFillColor);
        Assert.Equal(MarqueeAppearance.Default.InnerBorderColor, paint.InnerBorderColor);
    }

    /// <summary>枠線の太さと表示可否が設定から写像されることを検証する。</summary>
    /// <remarks>
    /// 枠線の太さと表示可否は描画にだけ必要な設定であり、コアの設定（<c>MarqueeOptions</c>）に含めないため、<br/>
    /// 本写像が外観（<see cref="MarqueeAppearance"/>）へ写す。
    /// </remarks>
    [Fact]
    public void 変換_枠線の太さと表示可否が設定から写像される()
    {
        var settings = CreateSettings() with
        {
            BorderThickness = 3.0,
            SizeLabelVisible = false,
            ShowCoordLabel = false,
        };

        var paint = CapturePaintMapper.ToMarqueeAppearance(settings);

        Assert.Equal(3.0, paint.OuterBorderThickness);
        Assert.False(paint.ShowSizeLabel);
        Assert.False(paint.ShowCoordLabel);
        // 色が不透明なら描画する（色のアルファを「描かない」の代用にしない）
        Assert.True(paint.ShowOverlay);
        Assert.True(paint.ShowOuterBorder);
    }

    /// <summary>透明な色は描画しない扱いになることを検証する。</summary>
    /// <remarks>従来の「アルファ 0 = 描かない」を明示フラグ（表示可否）で表現する。</remarks>
    [Fact]
    public void 変換_透明な色は描画しない()
    {
        var settings = CreateSettings() with
        {
            OverlayColor = DrawingColor.Transparent,
            BorderColor = DrawingColor.Transparent,
        };

        var paint = CapturePaintMapper.ToMarqueeAppearance(settings);

        Assert.False(paint.ShowOverlay);
        Assert.False(paint.ShowOuterBorder);
    }

    [Fact]
    public void 変換_ルーペの色が設定から写像される()
    {
        var settings = CreateSettings() with
        {
            LoupePasteboardColor = DrawingColor.Red,
            LoupeFrameColor = DrawingColor.Blue,
            LoupeCrossColor = DrawingColor.Yellow,
            LoupeCoordLabelColor = DrawingColor.Cyan,
            LoupeCoordLabelBackgroundColor = DrawingColor.FromArgb(11, 22, 33),
        };

        var paint = CapturePaintMapper.ToLoupeAppearance(settings);

        Assert.Equal(DrawingColor.Red, paint.PasteboardColor);
        Assert.Equal(DrawingColor.Blue, paint.BorderColor);
        Assert.Equal(DrawingColor.Yellow, paint.CrossColor);
        Assert.Equal(DrawingColor.Cyan, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(11, 22, 33), paint.CoordLabelBackgroundColor);
    }

    /// <summary>画像外（ペーストボード）の色が設定から与えられることを検証する。</summary>
    /// <remarks>画像外の色は設定（<c>LoupePasteboardColor</c>）が単一所有する（透明を指定すると背後の要素が見える）。</remarks>
    [Fact]
    public void 変換_ルーペの画像外の色は設定から与えられる()
    {
        var settings = CreateSettings() with { LoupePasteboardColor = DrawingColor.FromArgb(255, 12, 34, 56) };

        Assert.Equal(DrawingColor.FromArgb(255, 12, 34, 56), CapturePaintMapper.ToLoupeAppearance(settings).PasteboardColor);
    }

    [Fact]
    public void 変換_設定がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => CapturePaintMapper.ToMarqueeAppearance(null!));
        Assert.Throws<ArgumentNullException>(() => CapturePaintMapper.ToLoupeAppearance(null!));
    }
}

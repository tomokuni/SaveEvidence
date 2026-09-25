using EsUtil.SaveEvidence.Marquee.UIBase;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.CaptureSession.UIBase.Tests;

/// <summary><see cref="CapturePaintMapper"/> の単体テスト（設定項目と描画の色の対応）。</summary>
/// <remarks>
/// 色は描画にだけ必要な設定であり、描画層（UIBase）の <c>MarqueePaint</c> / <c>LoupePaint</c> が単一所有する。<br/>
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

        var paint = CapturePaintMapper.ToMarqueePaint(settings);

        Assert.Equal(DrawingColor.Red, paint.HandleColor);
        Assert.Equal(DrawingColor.Green, paint.HandleBorderColor);
        Assert.Equal(DrawingColor.Blue, paint.MarkerColor);
        Assert.Equal(DrawingColor.Yellow, paint.SizeLabelColor);
        Assert.Equal(DrawingColor.FromArgb(10, 20, 30), paint.SizeLabelBackgroundColor);
        Assert.Equal(DrawingColor.Cyan, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(40, 50, 60), paint.CoordLabelBackgroundColor);
        Assert.Equal(DrawingColor.FromArgb(70, 80, 90), paint.OverlayColor);
    }

    /// <summary>選択枠線（ハンドルの枠線）と暗転マスクの穴あけ枠が同じ色になることを検証する。</summary>
    /// <remarks>両者は同じ矩形に重なって描画されるため、片方だけ別の色にすると見た目が崩れる。</remarks>
    [Fact]
    public void 変換_穴あけ枠とハンドル枠は同じ枠線色になる()
    {
        var settings = CreateSettings() with { BorderColor = DrawingColor.Red };

        var paint = CapturePaintMapper.ToMarqueePaint(settings);

        Assert.Equal(DrawingColor.Red, paint.HandleBorderColor);
        Assert.Equal(paint.HandleBorderColor, paint.OuterBorderColor);
    }

    /// <summary>写像しない色が既定値を保つことを検証する。</summary>
    /// <remarks>内側の塗り・内側の枠線の描画可否は <c>MarqueeDecoOptions</c> のフラグが単一所有するため、色は既定の透明のままとする。</remarks>
    [Fact]
    public void 変換_写像しない色は既定値を保つ()
    {
        var paint = CapturePaintMapper.ToMarqueePaint(CreateSettings());

        Assert.Equal(MarqueePaint.Default.InnerFillColor, paint.InnerFillColor);
        Assert.Equal(MarqueePaint.Default.InnerBorderColor, paint.InnerBorderColor);
    }

    [Fact]
    public void 変換_ルーペの色が設定から写像される()
    {
        var settings = CreateSettings() with
        {
            LoupeCheckerColorA = DrawingColor.Red,
            LoupeCheckerColorB = DrawingColor.Green,
            LoupeFrameColor = DrawingColor.Blue,
            LoupeCrossColor = DrawingColor.Yellow,
            LoupeCoordLabelColor = DrawingColor.Cyan,
            LoupeCoordLabelBackgroundColor = DrawingColor.FromArgb(11, 22, 33),
        };

        var paint = CapturePaintMapper.ToLoupePaint(settings, DrawingColor.Transparent);

        Assert.Equal(DrawingColor.Red, paint.CheckerColorA);
        Assert.Equal(DrawingColor.Green, paint.CheckerColorB);
        Assert.Equal(DrawingColor.Blue, paint.FrameColor);
        Assert.Equal(DrawingColor.Yellow, paint.CrossColor);
        Assert.Equal(DrawingColor.Cyan, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(11, 22, 33), paint.CoordLabelBackgroundColor);
    }

    /// <summary>市松模様の下地色が引数で与えられることを検証する。</summary>
    /// <remarks>下地はホストの状況（撮影オーバーレイは透かす / プレビューの切り出しはテーマ背景色）で決まるため、設定からは写像しない。</remarks>
    [Fact]
    public void 変換_ルーペの下地色は引数で与えられる()
    {
        var settings = CreateSettings();
        var background = DrawingColor.FromArgb(255, 12, 34, 56);

        Assert.Equal(background, CapturePaintMapper.ToLoupePaint(settings, background).BackgroundColor);
        // 透明を渡すと市松模様の背後が透ける（撮影オーバーレイの既定）
        Assert.Equal(DrawingColor.Transparent, CapturePaintMapper.ToLoupePaint(settings, DrawingColor.Transparent).BackgroundColor);
    }

    [Fact]
    public void 変換_設定がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => CapturePaintMapper.ToMarqueePaint(null!));
        Assert.Throws<ArgumentNullException>(() => CapturePaintMapper.ToLoupePaint(null!, DrawingColor.Transparent));
    }
}

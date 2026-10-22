using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Tests.Crop;

/// <summary><see cref="OverlayRectShapes"/> のテスト（描画リストの矩形と色）。</summary>
/// <remarks>
/// 画素ではなく描画リストを検証する。 描画入力（配置・太さ・破線の判定と長さ・色）はコアの<br/>
/// <see cref="OverlayRectLayout"/> が確定するため、本テストは「コアが確定した値がそのとおり矩形になるか」を検証する。
/// </remarks>
public class OverlayRectShapesTests
{
    /// <summary>検証に使う前景色（赤）。</summary>
    private static readonly DrawingColor s_foreground = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>検証に使う背景色（青）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>指定スタイルの矩形情報を生成する。</summary>
    /// <param name="rect">矩形（ビューポート座標）。</param>
    /// <param name="style">枠線スタイル。</param>
    /// <param name="thickness">枠線の太さ。</param>
    /// <returns>矩形情報。</returns>
    private static OverlayRect Create(EsRect rect, OverlayLineStyle style, double thickness = 2)
        => new(rect, style, s_foreground, s_background, thickness);

    /// <summary>矩形情報（ビューポート座標とみなす）をコアの描画入力へ解決する。</summary>
    /// <param name="rect">矩形情報。</param>
    /// <returns>コアが確定した描画入力。</returns>
    private static OverlayRectFrame CreateFrame(OverlayRect rect)
        => OverlayRectLayout.Resolve(
            rect, new EsPoint(rect.Bounds.X, rect.Bounds.Y), new EsPoint(rect.Bounds.Right, rect.Bounds.Bottom));

    /// <summary>実線は枠 4 辺だけを前景色で追加することを検証する。</summary>
    [Fact]
    internal void Add_実線は枠のみを追加する()
    {
        var list = new DrawList();
        var frame = CreateFrame(Create(new EsRect(10, 10, 40, 30), OverlayLineStyle.Solid));

        OverlayRectShapes.Add(list, frame);

        Assert.Equal(4, list.RectCount);
        Assert.All(list.Rects, item => Assert.Equal(s_foreground, item.Color));

        // 最初の矩形は上辺（幅いっぱい・太さの分だけ高い）
        Assert.Equal(10, list.Rects[0].Bounds.X);
        Assert.Equal(10, list.Rects[0].Bounds.Y);
        Assert.Equal(40, list.Rects[0].Bounds.Width);
        Assert.Equal(2, list.Rects[0].Bounds.Height);
    }

    /// <summary>破線は背景色の実線 4 辺と前景色の破線を追加することを検証する。</summary>
    [Fact]
    internal void Add_破線は背景の実線と前景の破線を追加する()
    {
        var list = new DrawList();
        var frame = CreateFrame(Create(new EsRect(0, 0, 60, 40), OverlayLineStyle.Dashed));

        OverlayRectShapes.Add(list, frame);

        // 先頭 4 件は背景色の実線
        Assert.All(list.Rects.Take(4), item => Assert.Equal(s_background, item.Color));

        // 5 件目以降は前景色の破線
        Assert.All(list.Rects.Skip(4), item => Assert.Equal(s_foreground, item.Color));
        Assert.True(list.RectCount > 4);
    }

    /// <summary>描画できない大きさでは何も追加しないことを検証する。</summary>
    [Fact]
    internal void Add_大きさが0の場合は何も追加しない()
    {
        var list = new DrawList();
        var frame = CreateFrame(Create(new EsRect(10, 10, 0, 30), OverlayLineStyle.Solid));

        OverlayRectShapes.Add(list, frame);

        Assert.Equal(0, list.RectCount);
    }

    /// <summary>破線が枠の内側に収まることを検証する。</summary>
    [Fact]
    internal void Add_破線は枠の内側に収まる()
    {
        var list = new DrawList();
        var frame = CreateFrame(Create(new EsRect(100, 100, 50, 30), OverlayLineStyle.Dashed));

        OverlayRectShapes.Add(list, frame);

        foreach (var item in list.Rects.Skip(4))
        {
            Assert.InRange(item.Bounds.X, 100, 150);
            Assert.InRange(item.Bounds.Y, 100, 130);
        }
    }
}

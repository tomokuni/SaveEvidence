using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.AvaloniaUI.Tests;

/// <summary><see cref="AvaloniaCanvas"/> のテスト（Avalonia の描画 API へ写すことの検証）。</summary>
/// <remarks>
/// 描画の手順（何をどの順で描くか）は UI 非依存のレンダラが単一所有し、その画素検証は UI 非依存のテスト<br/>
/// （<c>MarqueeControl.UIBase.Tests</c> / <c>LoupeControl.UIBase.Tests</c>）が担う。<br/>
/// 本テストは「アダプタが例外なく描画 API を呼ぶこと」と「描画面の消去・ブラシ共有の契約」を検証する<br/>
/// （Avalonia ヘッドレスでは <see cref="RenderTargetBitmap"/> 経由の画素を読めないため、画素の検証は行わない）。<br/>
/// Avalonia のヘッドレス初期化が必要なため <see cref="AvaloniaHeadlessCollection"/> 内で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class AvaloniaCanvasTests
{
    /// <summary>検証に使う色（赤・不透明）。</summary>
    private static readonly DrawingColor s_red = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>検証に使う色（青・不透明）。</summary>
    private static readonly DrawingColor s_blue = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>テスト用の描画先（描画コンテキストと描画ポート）を生成する。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。</param>
    /// <returns>描画先。破棄は呼び出し側。</returns>
    private static (RenderTargetBitmap Bitmap, AvaloniaCanvas Canvas) CreateTarget(int size)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        var context = bitmap.CreateDrawingContext();
        return (bitmap, new AvaloniaCanvas(context));
    }

    [AvaloniaFact]
    public void 各描画メソッドが例外なく完了する()
    {
        var (bitmap, canvas) = CreateTarget(64);
        using var _ = bitmap;

        canvas.Clear(new EsRect(0, 0, 64, 64), s_blue);
        canvas.FillRect(new EsRect(4, 4, 20, 20), s_red);
        canvas.FillRects([new EsRect(0, 0, 4, 4), new EsRect(8, 8, 4, 4)], s_red);
        canvas.FillRoundRect(new EsRect(4, 4, 20, 20), cornerRadius: 3, s_blue);
        canvas.FillRoundRect(new EsRect(4, 4, 20, 20), cornerRadius: 0, s_blue);
        canvas.StrokeRect(new EsRect(10, 10, 20, 20), s_red, thickness: 1.5);
        canvas.DrawLine(new EsPoint(0, 0), new EsPoint(20, 20), s_red, thickness: 2);
        canvas.DrawLabel(new EsRect(2, 2, 40, 16), s_blue, "W", new EsPoint(6, 6), 12, s_red, cornerRadius: 0);
    }

    [AvaloniaFact]
    public void DrawImage_EsBitmapを変換して描画する()
    {
        var (bitmap, canvas) = CreateTarget(32);
        using var _ = bitmap;
        using var source = EsBitmap.Create(4, 4, s_blue);

        canvas.DrawImage(source, new EsRect(0, 0, 4, 4), new EsRect(0, 0, 32, 32));
    }

    [AvaloniaFact]
    public void MeasureText_実寸を返す()
    {
        var (bitmap, canvas) = CreateTarget(32);
        using var _ = bitmap;

        var size = canvas.MeasureText("W", 20);

        Assert.True(size.Width > 0, "幅が測れていない");
        Assert.True(size.Height > 0, "行高が測れていない");
    }

    [AvaloniaFact]
    public void ブラシキャッシュ容量超過時も例外なく描画する()
    {
        // 容量（既定 128）を超える色を順次描画し、容量超過時の全クリア経路を実行する
        var (bitmap, canvas) = CreateTarget(64);
        using var _ = bitmap;

        for (var i = 0; i <= CapacityLimitedCache<Color, SolidColorBrush>.DefaultCapacity; i++)
        {
            var color = DrawingColor.FromArgb((byte)(100 + i % 100), (byte)(i * 2), (byte)(255 - i), (byte)(i * 3));
            canvas.FillRect(new EsRect(0, 0, 64, 4), color);
            canvas.StrokeRect(new EsRect(0, 0, 64, 8), color, thickness: 1 + i % 3);
        }
    }

    [AvaloniaFact]
    public void 描画ポートがnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => new AvaloniaCanvas(null!));
}

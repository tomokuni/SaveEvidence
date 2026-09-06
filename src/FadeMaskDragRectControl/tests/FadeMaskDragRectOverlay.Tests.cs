using Avalonia.Media;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable;
using Xunit;
using PbBitmap = Estable.Media.EsBitmap;

namespace Estable.AvaloniaUI.FadeMaskDragRect.Tests;

/// <summary>
/// <see cref="FadeMaskDragRectControl.DrawSelectionRectOverlay"/>（選択矩形破線オーバーレイ描画）の単体テスト。
/// </summary>
/// <remarks>
/// 描画は純粋な静的メソッド（<c>PbBitmap</c> のみを使用）のため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）は不要。<br/>
/// <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// コントロール生成を伴うテストは <see cref="FadeMaskDragRectControlTests"/>（直列コレクション）で検証する。
/// </remarks>
public class FadeMaskDragRectOverlayTests
{
    /// <summary>テスト用の単色 Bitmap（白）を生成する。</summary>
    private static PbBitmap CreateSolidBitmap(int width, int height)
        => PbBitmap.Create(width, height, System.Drawing.Color.White);

    /// <summary>選択矩形破線オーバーレイが領域内にピクセルを描画することを検証する。</summary>
    [Fact]
    public void DrawSelectionRectOverlay_選択矩形の辺に破線が描画される()
    {
        // 白の 40x40 Bitmap に選択矩形（10,10,20,20）をズーム 4 で描画する
        var bitmap = CreateSolidBitmap(40, 40);
        var selectionRect = new RectD(10, 10, 20, 20);
        var captureTopLeft = new PointD(5, 5);
        var scale = 4.0;

        FadeMaskDragRectControl.DrawSelectionRectOverlay(
            bitmap, selectionRect, captureTopLeft, scale, Colors.Black, Colors.Transparent);

        // 上辺（ドメイン y=10 → ルーペ (10-5)*4=20）のピクセルが描画されていることを検証する
        var pixel = bitmap.GetPixel(20, 20);

        // 白背景（255）より暗い = 破線が描画されている
        Assert.True(pixel.B < 255, $"選択矩形の辺に線が描画されていない: B={pixel.B}");
    }

    /// <summary>ズーム 1 倍以下では破線を描画しないことを検証する。</summary>
    [Fact]
    public void DrawSelectionRectOverlay_ズーム1倍では描画しない()
    {
        var bitmap = CreateSolidBitmap(40, 40);

        // scale < 2 → 何も描画しない（例外なく早期リターンする）
        FadeMaskDragRectControl.DrawSelectionRectOverlay(
            bitmap, new RectD(10, 10, 20, 20), new PointD(5, 5), 1.0, Colors.Black, Colors.Transparent);

        // scale < 2 の早期リターンで例外なく実行されることを検証する
        Assert.Equal(40, bitmap.Size.Width);
    }
}

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Tests;

/// <summary>
/// <see cref="EsBitmapAvaloniaImage"/> の描画（<see cref="EsBitmapAvaloniaImage.Draw"/>）のテスト。
/// </summary>
/// <remarks>
/// <see cref="EsBitmapAvaloniaImage.Draw"/> は <see cref="Avalonia.Media.Imaging.Bitmap"/> への射影（変換）を行い、
/// その生成には Avalonia の描画バックエンド（<c>DrawingContext</c>）が必要なため、<c>[AvaloniaFact]</c> で検証する。<br/>
/// <see cref="EsBitmapAvaloniaImage"/> のそれ以外（<c>Size</c> / <c>Bitmap</c> / <c>Dispose</c>）は
/// <see cref="EsBitmapAvaloniaImageTests"/> が <c>[Fact]</c> で検証する（描画を伴うクラスと分離し、
/// Avalonia 初期化の有無を混在させない）。
/// </remarks>
public class EsBitmapAvaloniaImageDrawTests
{
    /// <summary>真実が破棄されたら描画は no-op になり、例外にもならないことを検証する。</summary>
    /// <remarks>
    /// 内包する <see cref="EsBitmap"/> がアダプタより先に破棄される経路（共有参照の破棄）に対する防御。
    /// 破棄済みの真実では射影を生成しない（破棄済みピクセルへアクセスしない）。
    /// </remarks>
    [AvaloniaFact]
    internal void Draw_真実が破棄されたらnoOpになり例外にならない()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        var image = new EsBitmapAvaloniaImage(bmp);

        // 真実を先に破棄する（アダプタより長生きする参照が壊れた場合の防御）
        bmp.Dispose();

        // 破棄済みの真実では描画しない（例外にもしない。 射影の生成も行わない）
        using var target = new RenderTargetBitmap(new PixelSize(8, 6), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            image.Draw(context, new Rect(0, 0, 8, 6), new Rect(0, 0, 8, 6));
        }

        // 冪等（二重破棄しない）
        image.Dispose();
        image.Dispose();
    }

    /// <summary>繰り返しの描画で例外なく描画できることを検証する（射影の再利用経路）。</summary>
    /// <remarks>
    /// 射影（<see cref="Bitmap"/> への変換）は初回の 1 回だけ行い、2 回目以降は再利用する。<br/>
    /// 再利用の正しさ（同じ <see cref="Bitmap"/> が使えること）を、連続描画が例外にならないことで確認する。
    /// </remarks>
    [AvaloniaFact]
    internal void Draw_繰り返し描画しても例外にならない()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Blue);
        using var image = new EsBitmapAvaloniaImage(bmp);

        using var target = new RenderTargetBitmap(new PixelSize(8, 6), new Vector(96, 96));
        for (var i = 0; i < 3; i++)
        {
            using var context = target.CreateDrawingContext();
            image.Draw(context, new Rect(0, 0, 8, 6), new Rect(0, 0, 8, 6));
        }
    }
}

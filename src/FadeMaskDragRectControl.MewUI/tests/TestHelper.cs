using SkiaSharp;

namespace Estable.MewUI.FadeMaskDragRect.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>描画先（キャンバスとその画素を読む手段）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。破棄は呼び出し側。</returns>
    public static RenderTarget CreateTarget(int width, int height) => new(width, height);
}

/// <summary>テスト用の描画先（キャンバスと画素読み取り）。</summary>
/// <remarks>
/// ウィンドウを必要としないため UI 初期化なしで検証できる。<br/>
/// 初期状態は透明（ルーペのキャンバスと同じ前提）。
/// </remarks>
internal sealed class RenderTarget : IDisposable
{
    /// <summary>実際の画素を保持するビットマップ。</summary>
    private readonly SKBitmap _bitmap;

    /// <summary>描画に使用するキャンバス。</summary>
    private readonly SKCanvas _canvas;

    /// <summary>指定サイズの描画先を初期化する（透明で初期化する）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    public RenderTarget(int width, int height)
    {
        _bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        _bitmap.Erase(SKColors.Transparent);
        _canvas = new SKCanvas(_bitmap);
    }

    /// <summary>描画に使用するキャンバス。</summary>
    public SKCanvas Canvas => _canvas;

    /// <summary>指定座標の画素色を取得する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>画素色（非乗算）。</returns>
    public SKColor GetPixel(int x, int y) => _bitmap.GetPixel(x, y);

    /// <summary>指定色の画素が何個あるかを数える（画像全体）。</summary>
    /// <param name="color">数える色。</param>
    /// <returns>一致する画素数。</returns>
    public int CountColor(SKColor color)
    {
        var count = 0;
        for (var y = 0; y < _bitmap.Height; y++)
        {
            for (var x = 0; x < _bitmap.Width; x++)
            {
                if (GetPixel(x, y) == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>描画先を解放する。</summary>
    public void Dispose()
    {
        _canvas.Dispose();
        _bitmap.Dispose();
    }
}

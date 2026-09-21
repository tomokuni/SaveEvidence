using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace Estable.MewUI.Loupe.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>指定サイズの画像を生成する（不透明の単色で塗りつぶし）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像。所有権は呼び出し側。</returns>
    public static EsBitmap CreateBitmap(int size, SKColor color)
    {
        // 所有権は FromSKBitmap へ移るため using を付けない
        var bitmap = new SKBitmap(size, size);
        bitmap.Erase(color);

        return EsBitmap.FromSKBitmap(bitmap);
    }

    /// <summary>元画像の中央を区別できるテスト画像を生成する（中心 1 画素のみ別色）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。奇数を推奨。</param>
    /// <param name="background">背景色。</param>
    /// <param name="center">中心画素の色。</param>
    /// <returns>生成した画像。所有権は呼び出し側。</returns>
    public static EsBitmap CreateBitmapWithCenter(int size, SKColor background, SKColor center)
    {
        // 所有権は FromSKBitmap へ移るため using を付けない
        var bitmap = new SKBitmap(size, size);
        bitmap.Erase(background);
        bitmap.SetPixel(size / 2, size / 2, center);

        return EsBitmap.FromSKBitmap(bitmap);
    }

    /// <summary>描画先（キャンバスとその画素を読む手段）を生成する。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。</param>
    /// <returns>生成した描画先。破棄は呼び出し側。</returns>
    public static LoupeRenderTarget CreateTarget(int size) => new(size);
}

/// <summary>テスト用の描画先（キャンバスと画素読み取り）。</summary>
/// <remarks>
/// ウィンドウを必要としないため UI 初期化なしで検証できる。<br/>
/// 内部は乗算済みアルファで保持されるが、<c>SKBitmap.GetPixel</c> は非乗算の色を返す。
/// </remarks>
internal sealed class LoupeRenderTarget : IDisposable
{
    /// <summary>実際の画素を保持するビットマップ。</summary>
    private readonly SKBitmap _bitmap;

    /// <summary>描画に使用するキャンバス。</summary>
    private readonly SKCanvas _canvas;

    /// <summary>指定サイズの描画先を初期化する（透明で初期化する）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。</param>
    public LoupeRenderTarget(int size)
    {
        _bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
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

    /// <summary>描画先を解放する。</summary>
    public void Dispose()
    {
        _canvas.Dispose();
        _bitmap.Dispose();
    }
}

using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using EsUtil.Platform.Skia;


namespace EsUtil.SaveEvidence.ZoomView.MewUI.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>指定サイズの単色画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    public static EsBitmap CreateBitmap(int width, int height, SKColor color)
    {
        // 所有権は FromSKBitmap へ移るため using を付けない
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);

        return bitmap.ToEsBitmap();
    }

    /// <summary>描画先（キャンバスとその画素を読む手段）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。 破棄は呼び出し側。</returns>
    public static RenderTarget CreateTarget(int width, int height) => new(width, height);
}

/// <summary>テスト用の描画先（キャンバスと画素読み取り）。</summary>
/// <remarks>
/// ウィンドウを必要としないため UI 初期化なしで検証できる。<br/>
/// 内部は乗算済みアルファで保持されるが、<c>SKBitmap.GetPixel</c> は非乗算の色を返す。
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

    /// <summary>描画先を解放する。</summary>
    public void Dispose()
    {
        _canvas.Dispose();
        _bitmap.Dispose();
    }
}


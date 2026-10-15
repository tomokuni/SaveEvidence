using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
/// <remarks>
/// カテゴリごとのテスト（<c>Tests.Crop</c> / <c>Tests.SelectionOverlay</c> / <c>Tests.ZoomView</c>）は<br/>
/// 親名前空間（<c>Tests</c>）の本型をそのまま参照できる（同名クラスの複製を作らない）。
/// </remarks>
internal static class TestHelper
{
    /// <summary>指定サイズの単色画像（読み取りの面）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// テスト画像は生成後に書き換えない（表示・描画の入力）ため、凍結して 0 コピーで読み取り専用の面へ渡す。
    /// </remarks>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, SKColor color)
    {
        // 所有権は ToEsBitmap へ移るため using を付けない
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);

        using var created = bitmap.ToEsBitmap();
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }

    /// <summary>描画先（キャンバスとその画素を読む手段）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。 破棄は呼び出し側。</returns>
    public static RenderTarget CreateTarget(int width, int height) => new(width, height);
}

/// <summary>テスト用の描画先（キャンバスと画素読み取り）。</summary>
/// <remarks>
/// ウィンドウを必要としないため XAML の初期化なしで検証できる。<br/>
/// 初期状態は透明（撮影スタックのキャンバスと同じ前提）。<br/>
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

    /// <summary>指定領域に指定色の画素が何個あるかを数える。</summary>
    /// <param name="x0">左上 X 座標。</param>
    /// <param name="y0">左上 Y 座標。</param>
    /// <param name="x1">右下 X 座標（含まない）。</param>
    /// <param name="y1">右下 Y 座標（含まない）。</param>
    /// <param name="color">数える色。</param>
    /// <returns>一致する画素数。</returns>
    public int CountColor(int x0, int y0, int x1, int y1, SKColor color)
    {
        var count = 0;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
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
